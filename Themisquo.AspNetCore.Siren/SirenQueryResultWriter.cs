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
    /// Every entity gets a <c>self</c> link to the request.
    /// </summary>
    public class SirenQueryResultWriter : IQueryResultWriter
    {
        public const string ContentType = "application/vnd.siren+json";

        private readonly JsonSerializerOptions jsonOptions;

        public SirenQueryResultWriter(IOptions<HttpJsonOptions> jsonOptions) =>
            this.jsonOptions = jsonOptions.Value.SerializerOptions;

        public IResult Write<TQuery, TResult>(HttpContext context, TQuery query, TResult result) where TQuery : IQuery<TResult>
        {
            var request = context.Request;
            SirenLink[] links = [new(["self"], $"{request.PathBase}{request.Path}{request.QueryString}")];

            return Results.Json(BuildEntity(typeof(TResult), result, links), jsonOptions, ContentType);
        }

        private SirenEntity BuildEntity(Type resultType, object? result, SirenLink[] links)
        {
            if (result is null)
            {
                return new SirenEntity([ClassName(resultType)], null, null, links);
            }

            if (CollectionElementType(resultType) is Type elementType)
            {
                var entities = ((IEnumerable)result).Cast<object?>()
                    .Select(item => new SirenSubEntity(["item"], [ClassName(elementType)], Properties(item, elementType), null, null))
                    .ToArray();
                return new SirenEntity([ClassName(elementType), "collection"], null, entities, links);
            }

            var node = JsonSerializer.SerializeToNode(result, resultType, jsonOptions);
            return node is JsonObject properties
                ? new SirenEntity([ClassName(resultType)], properties, null, links)
                : new SirenEntity(null, new JsonObject { ["value"] = node }, null, links);
        }

        private JsonObject? Properties(object? item, Type itemType)
        {
            var node = JsonSerializer.SerializeToNode(item, itemType, jsonOptions);
            return node is null or JsonObject ? (JsonObject?)node : new JsonObject { ["value"] = node };
        }

        private static Type? CollectionElementType(Type type)
        {
            if (type == typeof(string) || IsDictionary(type))
            {
                return null;
            }

            return EnumerableInterfaces(type).FirstOrDefault()?.GetGenericArguments()[0];
        }

        private static IEnumerable<Type> EnumerableInterfaces(Type type) =>
            (type.IsInterface ? type.GetInterfaces().Prepend(type) : type.GetInterfaces())
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        private static bool IsDictionary(Type type) =>
            (type.IsInterface ? type.GetInterfaces().Prepend(type) : type.GetInterfaces())
                .Any(i => i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

        /// <summary>The type name in camelCase, without generic arity or the leading <c>I</c> of an interface.</summary>
        internal static string ClassName(Type type)
        {
            var name = type.Name;
            var arity = name.IndexOf('`');
            if (arity >= 0)
            {
                name = name[..arity];
            }

            if (type.IsInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            {
                name = name[1..];
            }

            return JsonNamingPolicy.CamelCase.ConvertName(name);
        }
    }
}
