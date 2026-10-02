using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Themisquo.AspNetCore.Siren
{
    /// <summary>
    /// Writes query results as Siren entities: a single result becomes the entity's <c>properties</c>, a list becomes a
    /// <c>collection</c> with one <c>item</c> sub-entity per element, and a scalar is wrapped as <c>properties.value</c>.
    /// Every entity gets a <c>self</c> link to the request, and each collection item gets a <c>self</c> link to the
    /// GET query route that returns a single item of the collection's element type, when one is mapped. A single result
    /// or item also links to the resources it refers to (see <see cref="ResourceCatalog.GetRelatedResources"/>), with
    /// the related resource's type name as the link's <c>rel</c>. Commands mapped on an entity's route become its
    /// <c>actions</c> (see <see cref="ResourceCatalog.GetActions"/>).
    /// </summary>
    public class SirenQueryResultWriter : IQueryResultWriter
    {
        public const string ContentType = "application/vnd.siren+json";

        private readonly JsonSerializerOptions jsonOptions;
        private readonly ResourceCatalog resources;

        public SirenQueryResultWriter(IOptions<HttpJsonOptions> jsonOptions, ResourceCatalog resources)
        {
            this.jsonOptions = jsonOptions.Value.SerializerOptions;
            this.resources = resources;
        }

        public IResult Write<TQuery, TResult>(HttpContext context, TQuery query, TResult result) where TQuery : IQuery<TResult>
        {
            return Results.Json(BuildEntity(typeof(TQuery), typeof(TResult), result, context.Request), jsonOptions, ContentType);
        }

        private SirenEntity BuildEntity(Type queryType, Type resultType, object? result, HttpRequest request)
        {
            var self = new SirenLink(["self"], $"{request.PathBase}{request.Path}{request.QueryString}");
            var actions = request.HttpContext.GetEndpoint()?.Metadata.GetMetadata<QueryEndpointMetadata>() is { } endpoint
                ? Actions(endpoint.Pattern, $"{request.PathBase}{request.Path}")
                : null;

            if (result is null)
            {
                return new SirenEntity([resources.GetTypeName(resultType, queryType)], null, null, [self], actions);
            }

            if (ResourceConventions.GetCollectionElementType(resultType) is Type elementType)
            {
                var className = resources.GetTypeName(elementType, queryType);
                var entities = ((IEnumerable)result).Cast<object?>()
                    .Select(item => ItemEntity(className, elementType, item, request))
                    .ToArray();
                return new SirenEntity([className, "collection"], null, entities, [self], actions);
            }

            var node = JsonSerializer.SerializeToNode(result, resultType, jsonOptions);
            return node is JsonObject properties
                ? new SirenEntity([resources.GetTypeName(resultType, queryType)], properties, null, [self, .. RelatedLinks(resultType, result, request)], actions)
                : new SirenEntity(null, new JsonObject { ["value"] = node }, null, [self], actions);
        }

        private SirenSubEntity ItemEntity(string className, Type itemType, object? item, HttpRequest request)
        {
            if (item is null)
            {
                return new SirenSubEntity(["item"], [className], null, null, null);
            }

            var route = resources.GetItemRoute(itemType, item, request);
            SirenLink[] links = route is not null
                ? [new SirenLink(["self"], route.Path), .. RelatedLinks(itemType, item, request)]
                : [.. RelatedLinks(itemType, item, request)];
            return new SirenSubEntity(["item"], [className], Properties(item, itemType), null,
                links.Length > 0 ? links : null,
                route is not null ? Actions(route.Pattern, route.Path) : null);
        }

        private IEnumerable<SirenLink> RelatedLinks(Type itemType, object item, HttpRequest request) =>
            resources.GetRelatedResources(itemType, item, request).Select(related => new SirenLink([related.TypeName], related.Path));

        private SirenAction[]? Actions(string routePattern, string path)
        {
            var actions = resources.GetActions(routePattern, path)
                .Select(action =>
                {
                    var fields = action.Fields
                        .Select(field => new SirenField(jsonOptions.PropertyNamingPolicy?.ConvertName(field.Name) ?? field.Name, InputType(field.Type)))
                        .ToArray();
                    return fields.Length > 0
                        ? new SirenAction(action.Name, action.HttpMethod, action.Path, "application/json", fields, action.Title)
                        : new SirenAction(action.Name, action.HttpMethod, action.Path, null, null, action.Title);
                })
                .ToArray();
            return actions.Length > 0 ? actions : null;
        }

        // The HTML input type Siren uses for a field of the given type.
        private static string InputType(Type type) => Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "checkbox",
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal when !type.IsEnum => "number",
            TypeCode.DateTime => "datetime-local",
            _ when type == typeof(DateOnly) => "date",
            _ when type == typeof(TimeOnly) => "time",
            _ => "text",
        };

        private JsonObject? Properties(object? item, Type itemType)
        {
            var node = JsonSerializer.SerializeToNode(item, itemType, jsonOptions);
            return node is null or JsonObject ? (JsonObject?)node : new JsonObject { ["value"] = node };
        }
    }
}
