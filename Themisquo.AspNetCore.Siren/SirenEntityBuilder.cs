using Microsoft.AspNetCore.Http;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Themisquo.AspNetCore.Siren
{
    /// <summary>
    /// Builds the Siren entity for a query result: a single result becomes the entity's <c>properties</c>, a list becomes
    /// a <c>collection</c> with one <c>item</c> sub-entity per element, and a scalar is wrapped as
    /// <c>properties.value</c>. Every entity gets a <c>self</c> link, and each collection item gets a <c>self</c> link to
    /// the GET query route that returns a single item of the collection's element type, when one is mapped. A single
    /// result or item also links to the resources it refers to (see <see cref="ResourceCatalog.GetRelatedResources"/>),
    /// with the related resource's type name as the link's <c>rel</c>. Commands mapped on an entity's route become its
    /// <c>actions</c> (see <see cref="ResourceCatalog.GetActions"/>).
    /// </summary>
    internal class SirenEntityBuilder(JsonSerializerOptions jsonOptions, ResourceCatalog resources)
    {
        /// <param name="self">The entity's <c>self</c> link.</param>
        /// <param name="routePattern">The route pattern the entity is served at, to find its actions; <c>null</c> for none.</param>
        /// <param name="path">The entity's path, including the path base, for its actions' <c>href</c>.</param>
        public SirenEntity Build(Type queryType, Type resultType, object? result, HttpRequest request, string self, string? routePattern, string path)
        {
            SirenLink selfLink = new(["self"], self);
            var actions = routePattern is not null ? Actions(routePattern, path) : null;

            if (result is null)
            {
                return new SirenEntity([resources.GetTypeName(resultType, queryType)], null, null, [selfLink], actions);
            }

            if (ResourceConventions.GetCollectionElementType(resultType) is Type elementType)
            {
                var className = resources.GetTypeName(elementType, queryType);
                var entities = ((IEnumerable)result).Cast<object?>()
                    .Select(item => ItemEntity(className, elementType, item, request))
                    .ToArray();
                return new SirenEntity([className, "collection"], null, entities, [selfLink], actions);
            }

            var node = JsonSerializer.SerializeToNode(result, resultType, jsonOptions);
            return node is JsonObject properties
                ? new SirenEntity([resources.GetTypeName(resultType, queryType)], properties, null, [selfLink, .. RelatedLinks(resultType, result, request)], actions)
                : new SirenEntity(null, new JsonObject { ["value"] = node }, null, [selfLink], actions);
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
