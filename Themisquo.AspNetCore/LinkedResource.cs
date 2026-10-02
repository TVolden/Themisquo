namespace Themisquo.AspNetCore
{
    /// <summary>A link from a resource, found by <see cref="ResourceCatalog.GetLinks"/>.</summary>
    /// <param name="Rel">The link's relation.</param>
    /// <param name="Path">
    /// The linked path, including the request's path base; for an external link, an absolute URL when it was declared
    /// with one.
    /// </param>
    /// <param name="TypeName">
    /// The linked resource's type name (see <see cref="ResourceCatalog.GetTypeName"/>), or of its elements when it is a
    /// list; <c>null</c> for a scalar or an external link.
    /// </param>
    /// <param name="IsCollection">Whether the linked resource is a list.</param>
    public sealed record LinkedResource(string Rel, string Path, string? TypeName = null, bool IsCollection = false);
}
