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
    /// result type, the path that returns a given item, the resources it refers to or nests, and the commands that act on
    /// it.
    /// </summary>
    /// <remarks>
    /// The endpoints are read on first use, so the catalog can be resolved before they are mapped, but endpoints mapped
    /// after its first use are not seen.
    /// </remarks>
    public class ResourceCatalog
    {
        private static readonly Regex PlaceholderPattern = new(@"\{[^}]*\}", RegexOptions.Compiled);

        private readonly Lazy<ILookup<Type, QueryEndpointMetadata>> endpointsByResultType;
        private readonly Lazy<ILookup<Type, QueryEndpointMetadata>> endpointsByQueryType;
        private readonly Lazy<ILookup<string, (string Segment, QueryEndpointMetadata Metadata)>> endpointsByParentShape;
        private readonly Lazy<ILookup<string, CommandEndpointMetadata>> commandsByRouteShape;
        private readonly Lazy<ILookup<Type, CommandEndpointMetadata>> commandsByType;
        private readonly Lazy<ILookup<string, CommandEndpointMetadata>> commandsByContext;

        public ResourceCatalog(EndpointDataSource endpointDataSource)
        {
            var queries = new Lazy<IReadOnlyList<QueryEndpointMetadata>>(() => endpointDataSource.Endpoints
                .Select(endpoint => endpoint.Metadata.GetMetadata<QueryEndpointMetadata>())
                .OfType<QueryEndpointMetadata>()
                .Where(metadata => HttpMethods.IsGet(metadata.HttpMethod))
                .ToList());
            endpointsByResultType = new(() => queries.Value.ToLookup(metadata => metadata.ResultType));
            endpointsByQueryType = new(() => queries.Value.ToLookup(metadata => metadata.QueryType));
            endpointsByParentShape = new(() => queries.Value
                .Select(metadata => (Nesting: ParentAndSegment(metadata.Pattern), Metadata: metadata))
                .Where(entry => entry.Nesting is not null)
                .ToLookup(entry => RouteShape(entry.Nesting!.Value.Parent), entry => (entry.Nesting!.Value.Segment, entry.Metadata)));
            var commands = new Lazy<IReadOnlyList<CommandEndpointMetadata>>(() => endpointDataSource.Endpoints
                .Select(endpoint => endpoint.Metadata.GetMetadata<CommandEndpointMetadata>())
                .OfType<CommandEndpointMetadata>()
                .ToList());
            commandsByRouteShape = new(() => commands.Value.ToLookup(metadata => RouteShape(metadata.Pattern)));
            commandsByType = new(() => commands.Value.ToLookup(metadata => metadata.CommandType));
            commandsByContext = new(() => commands.Value
                .Where(metadata => ResourceConventions.GetAction(metadata.CommandType)?.Context is not null)
                .ToLookup(metadata => ResourceConventions.GetAction(metadata.CommandType)!.Context!, StringComparer.Ordinal));
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
        /// <see cref="GetActions(string, string)"/>.
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
                .Select(metadata => CommandAction(metadata, path))
                .ToList();

        /// <summary>
        /// The actions on a resource returned by <paramref name="queryType"/>, or on an item of its list, in order:
        /// <list type="number">
        /// <item>the commands mapped on the resource's route (see <see cref="GetActions(string, string)"/>), unless
        /// <see cref="ResourceAttribute.AutoActions"/> or <see cref="ResourceAttribute.AutoItemActions"/> is <c>false</c>;</item>
        /// <item>the commands declared with <see cref="ResourceActionAttribute{TCommand}"/> or <see cref="ItemActionAttribute{TCommand}"/>;</item>
        /// <item>the commands whose <see cref="ActionAttribute.Context"/> matches <see cref="ResourceAttribute.Context"/> or
        /// <see cref="ResourceAttribute.ItemContext"/>;</item>
        /// <item>the external actions declared with <see cref="ResourceActionAttribute"/> or <see cref="ItemActionAttribute"/>.</item>
        /// </list>
        /// A command appears once.
        /// </summary>
        /// <remarks>
        /// A declared command's route, or an external action's URL template, is filled like a link: from the target's
        /// property of the same name, then from its primary id for the last placeholder only, then from the request's
        /// route values. An action that can't be fully resolved is left out.
        /// </remarks>
        /// <param name="queryType">The query that returns the resource, or the list query for its items.</param>
        /// <param name="forItems">Whether the actions are for an item of the list rather than the resource itself.</param>
        /// <param name="route">The resource's route, for the commands mapped on it; <c>null</c> when it has none.</param>
        /// <param name="target">The resource or item placeholders are filled from; <c>null</c> for none, such as for a list.</param>
        /// <param name="request">The current request, for its path base and route values.</param>
        public IReadOnlyList<ResourceAction> GetActions(Type queryType, bool forItems, ResourceRoute? route, object? target, HttpRequest request)
        {
            var resource = ResourceConventions.GetResource(queryType);
            var automatic = forItems ? resource?.AutoItemActions ?? true : resource?.AutoActions ?? true;
            var context = forItems ? resource?.ItemContext : resource?.Context;
            var declared = ResourceConventions.GetDeclaredActions(queryType, forItems);
            var idProperty = target is null ? null : TargetIdProperty(queryType, forItems, target, resource);

            var actions = new List<ResourceAction>();
            if (automatic && route is not null)
            {
                actions.AddRange(GetActions(route.Pattern, route.Path));
            }

            var commands = declared
                .Where(declaration => declaration.CommandType is not null)
                .Select(declaration => (declaration.CommandType!, declaration.Title))
                .Concat((context is null ? [] : commandsByContext.Value[context])
                    .Select(metadata => (metadata.CommandType, (string?)null)));
            foreach (var (commandType, title) in commands)
            {
                if (actions.Any(action => action.CommandType == commandType))
                {
                    continue;
                }

                foreach (var metadata in commandsByType.Value[commandType])
                {
                    if (ResolveTemplate("/" + metadata.Pattern.TrimStart('/'), target, idProperty, request) is { } path)
                    {
                        var action = CommandAction(metadata, $"{request.PathBase}{path}");
                        actions.Add(title is null ? action : action with { Title = title });
                        break;
                    }
                }
            }

            foreach (var declaration in declared.Where(declaration => declaration.CommandType is null))
            {
                if (ResolveUrl(declaration.UrlTemplate!, target, idProperty, request) is { } url)
                {
                    actions.Add(new ResourceAction(
                        declaration.Name!,
                        declaration.Method!.ToUpperInvariant(),
                        url,
                        null,
                        (declaration.Fields ?? []).Select(field => new ResourceActionField(field, typeof(string))).ToList(),
                        declaration.Title));
                }
            }

            return actions;
        }

        private static ResourceAction CommandAction(CommandEndpointMetadata metadata, string path)
        {
            var action = ResourceConventions.GetAction(metadata.CommandType);
            return new ResourceAction(
                action?.Name ?? ResourceConventions.GetDefaultTypeName(metadata.CommandType),
                metadata.HttpMethod,
                path,
                metadata.CommandType,
                BodyFields(metadata.CommandType, LocationTemplate.GetPlaceholders(metadata.Pattern)),
                action?.Title);
        }

        /// <summary>
        /// The query endpoints nested directly under a resource's route: those whose route is the resource's route plus
        /// one literal segment, such as <c>/projects/{projectId}/cards</c> under <c>/projects/{id}</c>. Routes match by
        /// shape, so placeholder names and constraints are ignored. A route with another placeholder, such as
        /// <c>/projects/{projectId}/cards/{cardId}</c>, is an item rather than a nested resource, so it isn't included.
        /// </summary>
        /// <remarks>
        /// Each link's relation is the nested query's <see cref="ResourceAttribute.Rel"/>, else the segment; its path is
        /// <paramref name="path"/> plus the segment.
        /// </remarks>
        /// <param name="routePattern">The resource's route pattern, such as the one from <see cref="GetItemRoute"/>.</param>
        /// <param name="path">The resource's resolved path, including the request's path base.</param>
        public IReadOnlyList<LinkedResource> GetNestedResources(string routePattern, string path) =>
            endpointsByParentShape.Value[RouteShape(routePattern)]
                .DistinctBy(entry => entry.Segment, StringComparer.OrdinalIgnoreCase)
                .Select(entry => LinkTo(entry.Metadata, ResourceConventions.GetResource(entry.Metadata.QueryType)?.Rel ?? entry.Segment,
                    $"{path.TrimEnd('/')}/{entry.Segment}"))
                .ToList();

        /// <summary>
        /// The links from a resource returned by <paramref name="queryType"/>, or from an item of its list, besides its
        /// <c>self</c> link, in order:
        /// <list type="number">
        /// <item>the resources it refers to (see <see cref="GetRelatedResources"/>) and the queries nested under its route
        /// (see <see cref="GetNestedResources"/>), unless <see cref="ResourceAttribute.AutoLinks"/> or
        /// <see cref="ResourceAttribute.AutoItemLinks"/> is <c>false</c>;</item>
        /// <item>the links declared with <see cref="ResourceLinkAttribute"/> or <see cref="ItemLinkAttribute"/>, and their
        /// generic forms.</item>
        /// </list>
        /// A link with the same relation and path appears once.
        /// </summary>
        /// <remarks>
        /// A declared query's route, or an external link's URL template, is filled like a declared action's. A link that
        /// can't be fully resolved is left out.
        /// </remarks>
        /// <param name="queryType">The query that returns the resource, or the list query for its items.</param>
        /// <param name="forItems">Whether the links are for an item of the list rather than the resource itself.</param>
        /// <param name="route">The resource's route, for the queries nested under it; <c>null</c> when it has none.</param>
        /// <param name="target">
        /// The resource or item that placeholders are filled from, and whose referenced resources are linked; <c>null</c>
        /// for none, such as for a list.
        /// </param>
        /// <param name="request">The current request, for its path base and route values.</param>
        public IReadOnlyList<LinkedResource> GetLinks(Type queryType, bool forItems, ResourceRoute? route, object? target, HttpRequest request)
        {
            var resource = ResourceConventions.GetResource(queryType);
            var automatic = forItems ? resource?.AutoItemLinks ?? true : resource?.AutoLinks ?? true;
            var idProperty = target is null ? null : TargetIdProperty(queryType, forItems, target, resource);

            var links = new List<LinkedResource>();
            if (automatic && target is not null)
            {
                links.AddRange(GetRelatedResources(TargetType(queryType, forItems, target), target, request)
                    .Select(related => new LinkedResource(related.TypeName, related.Path, related.TypeName)));
            }

            if (automatic && route is not null)
            {
                links.AddRange(GetNestedResources(route.Pattern, route.Path));
            }

            foreach (var declaration in ResourceConventions.GetDeclaredLinks(queryType, forItems))
            {
                var link = declaration.QueryType is { } linkedQueryType
                    ? endpointsByQueryType.Value[linkedQueryType]
                        .Select(metadata => ResolveTemplate("/" + metadata.Pattern.TrimStart('/'), target, idProperty, request) is { } path
                            ? LinkTo(metadata, declaration.Rel ?? ResourceConventions.GetResource(linkedQueryType)?.Rel, $"{request.PathBase}{path}")
                            : null)
                        .FirstOrDefault(link => link is not null)
                    : ResolveUrl(declaration.UrlTemplate!, target, idProperty, request) is { } url
                        ? new LinkedResource(declaration.Rel!, url)
                        : null;
                if (link is not null && !links.Any(existing => existing.Rel == link.Rel && existing.Path == link.Path))
                {
                    links.Add(link);
                }
            }

            return links;
        }

        // A link to a query endpoint, typed by its result. Without a relation, the resource type name is used.
        private LinkedResource LinkTo(QueryEndpointMetadata metadata, string? rel, string path)
        {
            if (ResourceConventions.GetCollectionElementType(metadata.ResultType) is Type elementType)
            {
                var elementTypeName = GetTypeName(elementType, metadata.QueryType);
                return new LinkedResource(rel ?? elementTypeName, path, elementTypeName, IsCollection: true);
            }

            var typeName = GetTypeName(metadata.ResultType, metadata.QueryType);
            return IsSingleResource(metadata.ResultType)
                ? new LinkedResource(rel ?? typeName, path, typeName)
                : new LinkedResource(rel ?? typeName, path);
        }

        // The pattern's parent route and last segment, when that segment is a literal: /projects/{id}/cards gives
        // (/projects/{id}, cards), and /projects gives ("", projects).
        private static (string Parent, string Segment)? ParentAndSegment(string pattern)
        {
            var route = "/" + pattern.Trim('/');
            var lastSlash = route.LastIndexOf('/');
            var segment = route[(lastSlash + 1)..];
            return segment.Length > 0 && !segment.Contains('{') ? (route[..lastSlash], segment) : null;
        }

        // The declared type of the target: the query's result type, or its element type for an item.
        private static Type TargetType(Type queryType, bool forItems, object target)
        {
            var resultType = ResourceConventions.GetQueryResultType(queryType);
            return (forItems && resultType is not null ? ResourceConventions.GetCollectionElementType(resultType) : resultType)
                ?? target.GetType();
        }

        // The target's primary id: named by the query's [Resource(Id)] for a single result, else by the one on a
        // single-item query for the target's type, else its Id property.
        private PropertyInfo? TargetIdProperty(Type queryType, bool forItems, object target, ResourceAttribute? resource)
        {
            var idResource = !forItems && resource?.Id is not null
                ? resource
                : endpointsByResultType.Value[TargetType(queryType, forItems, target)]
                    .Select(metadata => ResourceConventions.GetResource(metadata.QueryType))
                    .FirstOrDefault(r => r?.Id is not null);
            return ResourceConventions.GetIdProperty(target.GetType(), idResource);
        }

        // An external URL template, filled like a route: a relative URL gets the request's path base in front, and an
        // absolute http(s) URL is used as is.
        private static string? ResolveUrl(string template, object? target, PropertyInfo? idProperty, HttpRequest request)
        {
            var isAbsolute = template.StartsWith(Uri.UriSchemeHttp + "://", StringComparison.OrdinalIgnoreCase)
                || template.StartsWith(Uri.UriSchemeHttps + "://", StringComparison.OrdinalIgnoreCase);
            var url = ResolveTemplate(isAbsolute ? template : "/" + template.TrimStart('/'), target, idProperty, request);
            return url is null || isAbsolute ? url : $"{request.PathBase}{url}";
        }

        private static string? ResolveTemplate(string template, object? target, PropertyInfo? idProperty, HttpRequest request)
        {
            var lastPlaceholder = LocationTemplate.GetPlaceholders(template).LastOrDefault();
            return LocationTemplate.TryResolve(template, name =>
                (target is null ? null : PropertyValue(target, name))
                ?? (target is not null && string.Equals(name, lastPlaceholder, StringComparison.OrdinalIgnoreCase) ? idProperty?.GetValue(target) : null)
                ?? request.RouteValues[name]);
        }

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
