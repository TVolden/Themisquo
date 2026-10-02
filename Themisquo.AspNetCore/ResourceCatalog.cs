using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// The resources served by the GET query endpoints mapped with
    /// <see cref="ThemisquoEndpointExtensions.MapQuery{TQuery, TResult}"/>, for hypermedia formats: what to call a
    /// result type, and the path that returns a given item.
    /// </summary>
    /// <remarks>
    /// The endpoints are read on first use, so the catalog can be resolved before they are mapped, but endpoints mapped
    /// after its first use are not seen.
    /// </remarks>
    public class ResourceCatalog
    {
        private readonly Lazy<ILookup<Type, QueryEndpointMetadata>> endpointsByResultType;

        public ResourceCatalog(EndpointDataSource endpointDataSource)
        {
            endpointsByResultType = new(() => endpointDataSource.Endpoints
                .Select(endpoint => endpoint.Metadata.GetMetadata<QueryEndpointMetadata>())
                .OfType<QueryEndpointMetadata>()
                .Where(metadata => HttpMethods.IsGet(metadata.HttpMethod))
                .ToLookup(metadata => metadata.ResultType));
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
        public string? GetItemPath(Type itemType, object item, HttpRequest request)
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
                    return $"{request.PathBase}{path}";
                }
            }

            return null;
        }

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
