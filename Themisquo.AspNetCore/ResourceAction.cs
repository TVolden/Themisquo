namespace Themisquo.AspNetCore
{
    /// <summary>A command that can be sent to a resource, found by <see cref="ResourceCatalog.GetActions"/>.</summary>
    /// <param name="Name">
    /// The command's <see cref="ActionAttribute.Name"/>, or else its type name in camelCase (see
    /// <see cref="ResourceConventions.GetDefaultTypeName"/>).
    /// </param>
    /// <param name="HttpMethod">The HTTP method the command endpoint is mapped to.</param>
    /// <param name="Path">The path to send the command to, including the request's path base.</param>
    /// <param name="CommandType">The command type.</param>
    /// <param name="Fields">The command properties bound from the request body; route values are excluded.</param>
    /// <param name="Title">The command's <see cref="ActionAttribute.Title"/>, if any.</param>
    public sealed record ResourceAction(string Name, string HttpMethod, string Path, Type CommandType, IReadOnlyList<ResourceActionField> Fields, string? Title = null);

    /// <summary>A command property bound from the request body.</summary>
    /// <param name="Name">The property name.</param>
    /// <param name="Type">The property type, with <see cref="Nullable{T}"/> unwrapped.</param>
    public sealed record ResourceActionField(string Name, Type Type);

    /// <summary>A resource's route pattern and its resolved path, found by <see cref="ResourceCatalog.GetItemRoute"/>.</summary>
    public sealed record ResourceRoute(string Pattern, string Path);
}
