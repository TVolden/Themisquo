using System.Reflection;
using System.Text.Json;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// How hypermedia formats identify a query's result as a resource: its type name and its primary id, as described
    /// by <see cref="ResourceAttribute"/> on the query, or by convention.
    /// </summary>
    public static class ResourceConventions
    {
        /// <summary>The <see cref="ResourceAttribute"/> on the query type, if any.</summary>
        public static ResourceAttribute? GetResource(Type queryType) =>
            queryType.GetCustomAttribute<ResourceAttribute>();

        /// <summary>The <see cref="ActionAttribute"/> on the command type, if any.</summary>
        public static ActionAttribute? GetAction(Type commandType) =>
            commandType.GetCustomAttribute<ActionAttribute>();

        /// <summary>
        /// The type name in camelCase without generic arity, so <c>ICard</c> becomes <c>iCard</c> and <c>CardDto</c>
        /// becomes <c>cardDto</c>. Used when no <see cref="ResourceAttribute.Type"/> applies.
        /// </summary>
        public static string GetDefaultTypeName(Type resultType)
        {
            var name = resultType.Name;
            var arity = name.IndexOf('`');
            return JsonNamingPolicy.CamelCase.ConvertName(arity >= 0 ? name[..arity] : name);
        }

        /// <summary>
        /// The result property named by <see cref="ResourceAttribute.Id"/>, or else the property named <c>Id</c>.
        /// </summary>
        public static PropertyInfo? GetIdProperty(Type resultType, ResourceAttribute? resource) =>
            resultType.GetProperty(resource?.Id ?? "Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        /// <summary>
        /// The element type when <paramref name="resultType"/> is a list (an <see cref="IEnumerable{T}"/>), or
        /// <c>null</c> when it is a single resource. Strings and dictionaries are single resources.
        /// </summary>
        public static Type? GetCollectionElementType(Type resultType)
        {
            if (resultType == typeof(string) || IsDictionary(resultType))
            {
                return null;
            }

            return SelfAndInterfaces(resultType)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
        }

        private static bool IsDictionary(Type type) =>
            SelfAndInterfaces(type).Any(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

        private static IEnumerable<Type> SelfAndInterfaces(Type type) =>
            type.IsInterface ? type.GetInterfaces().Prepend(type) : type.GetInterfaces();
    }
}
