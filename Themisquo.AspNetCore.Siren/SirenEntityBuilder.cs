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
    /// the GET query route that returns a single item of the collection's element type, when one is mapped. Entities and
    /// items also link to the resources they refer to, to the queries nested under their route, and to the links declared
    /// on their query (see <see cref="ResourceCatalog.GetLinks"/>), with the linked resource's class. Commands mapped on an entity's route, and the
    /// actions declared on its query, become its <c>actions</c> (see <see cref="ResourceCatalog.GetActions(Type, bool, ResourceRoute, object, Microsoft.AspNetCore.Http.HttpRequest)"/>).
    /// </summary>
    internal class SirenEntityBuilder(JsonSerializerOptions jsonOptions, ResourceCatalog resources)
    {
        /// <param name="self">The entity's <c>self</c> link.</param>
        /// <param name="routePattern">The route pattern the entity is served at, to find the commands mapped on it; <c>null</c> for none.</param>
        /// <param name="path">The entity's path, including the path base, for its actions' <c>href</c>.</param>
        public SirenEntity Build(Type queryType, Type resultType, object? result, HttpRequest request, string self, string? routePattern, string path)
        {
            SirenLink selfLink = new(["self"], self);
            var route = routePattern is not null ? new ResourceRoute(routePattern, path) : null;

            if (result is null)
            {
                return new SirenEntity([resources.GetTypeName(resultType, queryType)], null, null,
                    [selfLink, .. Links(queryType, forItems: false, route, null, request)],
                    Actions(queryType, forItems: false, route, null, request));
            }

            if (ResourceConventions.GetCollectionElementType(resultType) is Type elementType)
            {
                var className = resources.GetTypeName(elementType, queryType);
                var entities = ((IEnumerable)result).Cast<object?>()
                    .Select(item => ItemEntity(queryType, className, elementType, item, request))
                    .ToArray();
                return new SirenEntity([className, "collection"], null, entities,
                    [selfLink, .. Links(queryType, forItems: false, route, null, request)],
                    Actions(queryType, forItems: false, route, null, request));
            }

            var node = JsonSerializer.SerializeToNode(result, resultType, jsonOptions);
            return node is JsonObject properties
                ? new SirenEntity([resources.GetTypeName(resultType, queryType)], properties, null,
                    [selfLink, .. Links(queryType, forItems: false, route, result, request)],
                    Actions(queryType, forItems: false, route, result, request))
                : new SirenEntity(null, new JsonObject { ["value"] = node }, null,
                    [selfLink, .. Links(queryType, forItems: false, route, null, request)],
                    Actions(queryType, forItems: false, route, null, request));
        }

        private SirenSubEntity ItemEntity(Type listQueryType, string className, Type itemType, object? item, HttpRequest request)
        {
            if (item is null)
            {
                return new SirenSubEntity(["item"], [className], null, null, null);
            }

            var route = resources.GetItemRoute(itemType, item, request);
            var links = Links(listQueryType, forItems: true, route, item, request);
            if (route is not null)
            {
                links = [new SirenLink(["self"], route.Path), .. links];
            }

            return new SirenSubEntity(["item"], [className], Properties(item, itemType), null,
                links.Length > 0 ? links : null,
                Actions(listQueryType, forItems: true, route, item, request));
        }

        private SirenLink[] Links(Type queryType, bool forItems, ResourceRoute? route, object? target, HttpRequest request) =>
            resources.GetLinks(queryType, forItems, route, target, request)
                .Select(link => new SirenLink([link.Rel], link.Path,
                    link.TypeName is null ? null : link.IsCollection ? [link.TypeName, "collection"] : [link.TypeName]))
                .ToArray();

        private SirenAction[]? Actions(Type queryType, bool forItems, ResourceRoute? route, object? target, HttpRequest request)
        {
            var actions = resources.GetActions(queryType, forItems, route, target, request)
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
