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
    /// the related resource's type name as the link's <c>rel</c>.
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

            if (result is null)
            {
                return new SirenEntity([resources.GetTypeName(resultType, queryType)], null, null, [self]);
            }

            if (ResourceConventions.GetCollectionElementType(resultType) is Type elementType)
            {
                var className = resources.GetTypeName(elementType, queryType);
                var entities = ((IEnumerable)result).Cast<object?>()
                    .Select(item => new SirenSubEntity(["item"], [className], Properties(item, elementType), null, ItemLinks(elementType, item, request)))
                    .ToArray();
                return new SirenEntity([className, "collection"], null, entities, [self]);
            }

            var node = JsonSerializer.SerializeToNode(result, resultType, jsonOptions);
            return node is JsonObject properties
                ? new SirenEntity([resources.GetTypeName(resultType, queryType)], properties, null, [self, .. RelatedLinks(resultType, result, request)])
                : new SirenEntity(null, new JsonObject { ["value"] = node }, null, [self]);
        }

        private SirenLink[]? ItemLinks(Type itemType, object? item, HttpRequest request)
        {
            if (item is null)
            {
                return null;
            }

            SirenLink[] links = resources.GetItemPath(itemType, item, request) is string href
                ? [new SirenLink(["self"], href), .. RelatedLinks(itemType, item, request)]
                : [.. RelatedLinks(itemType, item, request)];
            return links.Length > 0 ? links : null;
        }

        private IEnumerable<SirenLink> RelatedLinks(Type itemType, object item, HttpRequest request) =>
            resources.GetRelatedResources(itemType, item, request).Select(related => new SirenLink([related.TypeName], related.Path));

        private JsonObject? Properties(object? item, Type itemType)
        {
            var node = JsonSerializer.SerializeToNode(item, itemType, jsonOptions);
            return node is null or JsonObject ? (JsonObject?)node : new JsonObject { ["value"] = node };
        }
    }
}
