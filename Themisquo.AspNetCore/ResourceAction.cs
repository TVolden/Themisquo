namespace Themisquo.AspNetCore
{
    /// <summary>
    /// A command, or an external action, that can be sent to a resource, found by <see cref="ResourceCatalog.GetActions(Type, bool, ResourceRoute, object, Microsoft.AspNetCore.Http.HttpRequest)"/>.
    /// </summary>
    /// <param name="Name">
    /// The command's <see cref="ActionAttribute.Name"/>, or else its type name in camelCase (see
    /// <see cref="ResourceConventions.GetDefaultTypeName"/>); for an external action, its declared name.
    /// </param>
    /// <param name="HttpMethod">The HTTP method the command endpoint is mapped to, or the external action's method.</param>
    /// <param name="Path">
    /// The path to send the command to, including the request's path base; for an external action, an absolute URL when
    /// it was declared with one.
    /// </param>
    /// <param name="CommandType">The command type; <c>null</c> for an external action.</param>
    /// <param name="Fields">
    /// The command properties bound from the request body; route values are excluded. For an external action, its
    /// declared fields.
    /// </param>
    /// <param name="Title">The action's title, if any.</param>
    public sealed record ResourceAction(string Name, string HttpMethod, string Path, Type? CommandType, IReadOnlyList<ResourceActionField> Fields, string? Title = null);

    /// <summary>A command property bound from the request body, or a field of an external action.</summary>
    /// <param name="Name">The property name.</param>
    /// <param name="Type">The property type, with <see cref="Nullable{T}"/> unwrapped.</param>
    public sealed record ResourceActionField(string Name, Type Type);

    /// <summary>A resource's route pattern and its resolved path, found by <see cref="ResourceCatalog.GetItemRoute"/>.</summary>
    public sealed record ResourceRoute(string Pattern, string Path);
}
