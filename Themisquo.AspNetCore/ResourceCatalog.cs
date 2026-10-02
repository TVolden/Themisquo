using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// The resources served by the GET query endpoints mapped with
    /// <see cref="ThemisquoEndpointExtensions.MapQuery{TQuery, TResult}"/>, for hypermedia formats: what to call a
    /// result type, the path that returns a given item, the resources it refers to, and the commands that act on it.
    /// </summary>
    /// <remarks>
    /// The endpoints are read on first use, so the catalog can be resolved before they are mapped, but endpoints mapped
    /// after its first use are not seen.
    /// </remarks>
    public class ResourceCatalog
    {
        private static readonly Regex PlaceholderPattern = new(@"\{[^}]*\}", RegexOptions.Compiled);

        private readonly Lazy<ILookup<Type, QueryEndpointMetadata>> endpointsByResultType;
        private readonly Lazy<ILookup<string, CommandEndpointMetadata>> commandsByRouteShape;

        public ResourceCatalog(EndpointDataSource endpointDataSource)
        {
            endpointsByResultType = new(() => endpointDataSource.Endpoints
                .Select(endpoint => endpoint.Metadata.GetMetadata<QueryEndpointMetadata>())
                .OfType<QueryEndpointMetadata>()
                .Where(metadata => HttpMethods.IsGet(metadata.HttpMethod))
                .ToLookup(metadata => metadata.ResultType));
            commandsByRouteShape = new(() => endpointDataSource.Endpoints
                .Select(endpoint => endpoint.Metadata.GetMetadata<CommandEndpointMetadata>())
                .OfType<CommandEndpointMetadata>()
                .ToLookup(metadata => RouteShape(metadata.Pattern)));
        }

        /// <summary>
        /// The resource type name for <paramref name="resultType"/>, as returned by <paramref name="queryType"/>: the
        /// query's <see cref="ResourceAttribute.Type"/>, else the one on a single-item query endpoint for the same result
        /// type, else <see cref="ResourceConventions.GetDefaultTypeName"/>. For a list query, pass the element type.
        /// </summary>
        public string GetTypeName(Type resultType, Type? queryType = null) =>
            (queryType is null ? null : ResourceConventions.GetResource(queryType)?.Type)
            ?? endpointsByResultType.Value[resultType]
                .Select(metadata => ResourceConventions.GetResource(metadata.QueryType)?.Type)
                .FirstOrDefault(name => name is not null)
            ?? ResourceConventions.GetDefaultTypeName(resultType);

        /// <summary>
        /// The path, including the request's path base, of the first single-item query endpoint for
        /// <paramref name="itemType"/> whose route can be fully resolved for <paramref name="item"/>; <c>null</c> if none.
        /// </summary>
        /// <remarks>
        /// A placeholder is filled from the item's property of the same name, then, for the route's last placeholder,
        /// from the item's primary id (see <see cref="ResourceConventions.GetIdProperty"/>), and finally from the
        /// current request's route values. An explicit <see cref="ResourceAttribute.Id"/> on the endpoint's query fills
        /// the last placeholder before anything else.
        /// </remarks>
        public string? GetItemPath(Type itemType, object item, HttpRequest request) =>
            GetItemRoute(itemType, item, request)?.Path;

        /// <summary>
        /// Like <see cref="GetItemPath"/>, but also returns the route pattern the path was resolved from, for
        /// <see cref="GetActions"/>.
        /// </summary>
        public ResourceRoute? GetItemRoute(Type itemType, object item, HttpRequest request)
        {
            foreach (var metadata in endpointsByResultType.Value[itemType])
            {
                var pattern = "/" + metadata.Pattern.TrimStart('/');
                var lastPlaceholder = LocationTemplate.GetPlaceholders(pattern).LastOrDefault();
                var resource = ResourceConventions.GetResource(metadata.QueryType);
                var idProperty = ResourceConventions.GetIdProperty(item.GetType(), resource);
                var path = LocationTemplate.TryResolve(pattern, name =>
                {
                    if (!string.Equals(name, lastPlaceholder, StringComparison.OrdinalIgnoreCase))
                    {
                        return PropertyValue(item, name) ?? request.RouteValues[name];
                    }

                    // An explicit [Resource(Id = ...)] wins over a property that happens to share the placeholder's name.
                    var id = resource?.Id is not null
                        ? idProperty?.GetValue(item)
                        : PropertyValue(item, name) ?? idProperty?.GetValue(item);
                    return id ?? request.RouteValues[name];
                });
                if (path is not null)
                {
                    return new ResourceRoute(metadata.Pattern, $"{request.PathBase}{path}");
                }
            }

            return null;
        }

        /// <summary>
        /// The single-item query endpoint whose route matches <paramref name="path"/>, such as a command's Location,
        /// with its query bound from the path's route values the way command route values are bound; <c>null</c> if no
        /// route matches or the query can't be bound. Route constraints aren't checked.
        /// </summary>
        /// <param name="path">A path without the request's path base, or an absolute http(s) URL.</param>
        public ResourceQuery? FindQuery(string path)
        {
            path = Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri.AbsolutePath
                : path.Split('?', '#')[0];

            foreach (var metadata in endpointsByResultType.Value.SelectMany(endpoints => endpoints).Where(m => IsSingleResource(m.ResultType)))
            {
                var routeValues = new RouteValueDictionary();
                var matcher = new TemplateMatcher(TemplateParser.Parse(metadata.Pattern.TrimStart('/')), new RouteValueDictionary());
                if (!matcher.TryMatch("/" + path.TrimStart('/'), routeValues))
                {
                    continue;
                }

                try
                {
                    if (ThemisquoEndpointExtensions.BindFromRouteValues(metadata.QueryType, routeValues) is { } query)
                    {
                        return new ResourceQuery(metadata, query);
                    }
                }
                catch (JsonException)
                {
                    // A route value that doesn't fit the query property's type, so this route isn't the one.
                }
            }

            return null;
        }

        /// <summary>
        /// The commands mapped with <see cref="ThemisquoEndpointExtensions.MapCommand{TCommand}"/> on the same route as
        /// a resource, which can be sent to it at <paramref name="path"/>. Routes match by shape, so placeholder names and
        /// constraints are ignored: <c>/cards/{id}</c> matches <c>/cards/{cardId:guid}</c>.
        /// </summary>
        /// <param name="routePattern">The resource's route pattern, such as the one from <see cref="GetItemRoute"/>.</param>
        /// <param name="path">The resource's resolved path, including the request's path base.</param>
        public IReadOnlyList<ResourceAction> GetActions(string routePattern, string path) =>
            commandsByRouteShape.Value[RouteShape(routePattern)]
                .Select(metadata =>
                {
                    var action = ResourceConventions.GetAction(metadata.CommandType);
                    return new ResourceAction(
                        action?.Name ?? ResourceConventions.GetDefaultTypeName(metadata.CommandType),
                        metadata.HttpMethod,
                        path,
                        metadata.CommandType,
                        BodyFields(metadata.CommandType, LocationTemplate.GetPlaceholders(metadata.Pattern)),
                        action?.Title);
                })
                .ToList();

        // The properties the request body can bind: writable, or set through a constructor parameter (positional
        // records), except those bound from the route.
        private static IReadOnlyList<ResourceActionField> BodyFields(Type commandType, IReadOnlyList<string> routePlaceholders)
        {
            var constructorParameters = commandType.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod?.IsPublic == true || constructorParameters.Contains(property.Name))
                .Where(property => !routePlaceholders.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                .Select(property => new ResourceActionField(property.Name, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType))
                .ToList();
        }

        private static string RouteShape(string pattern) =>
            PlaceholderPattern.Replace("/" + pattern.Trim('/'), "{}").ToLowerInvariant();

        /// <summary>
        /// The resources <paramref name="item"/> refers to: one for each single-item query endpoint of another result
        /// type whose route's last placeholder matches a property of the item, such as <c>ProjectId</c> for
        /// <c>/projects/{projectId}</c>.
        /// </summary>
        /// <remarks>
        /// The route's other placeholders are filled from the item's properties of the same name, then from the current
        /// request's route values. Routes that can't be fully resolved are skipped.
        /// </remarks>
        public IReadOnlyList<RelatedResource> GetRelatedResources(Type itemType, object item, HttpRequest request)
        {
            var related = new List<RelatedResource>();
            foreach (var metadata in endpointsByResultType.Value.SelectMany(endpoints => endpoints))
            {
                if (metadata.ResultType == itemType || !IsSingleResource(metadata.ResultType))
                {
                    continue;
                }

                var pattern = "/" + metadata.Pattern.TrimStart('/');
                if (LocationTemplate.GetPlaceholders(pattern).LastOrDefault() is not string lastPlaceholder
                    || FindProperty(item, lastPlaceholder) is not PropertyInfo idProperty
                    || idProperty.GetValue(item) is not object id)
                {
                    continue;
                }

                var path = LocationTemplate.TryResolve(pattern, name =>
                    string.Equals(name, lastPlaceholder, StringComparison.OrdinalIgnoreCase)
                        ? id
                        : PropertyValue(item, name) ?? request.RouteValues[name]);
                if (path is not null)
                {
                    related.Add(new RelatedResource(GetTypeName(metadata.ResultType, metadata.QueryType), idProperty.Name, $"{request.PathBase}{path}"));
                }
            }

            return related;
        }

        // A query returning a list or a scalar (such as /cards/{cardId}/title) is not a resource to link to.
        private static bool IsSingleResource(Type resultType) =>
            ResourceConventions.GetCollectionElementType(resultType) is null
            && !resultType.IsPrimitive && !resultType.IsEnum
            && resultType != typeof(string) && resultType != typeof(decimal) && resultType != typeof(Guid)
            && resultType != typeof(DateTime) && resultType != typeof(DateTimeOffset) && resultType != typeof(TimeSpan)
            && resultType != typeof(DateOnly) && resultType != typeof(TimeOnly);

        private static PropertyInfo? FindProperty(object item, string name) =>
            item.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        private static object? PropertyValue(object item, string name) =>
            FindProperty(item, name)?.GetValue(item);
    }
}
