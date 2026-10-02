namespace Themisquo.AspNetCore
{
    /// <summary>A resource that an item refers to, found by <see cref="ResourceCatalog.GetRelatedResources"/>.</summary>
    /// <param name="TypeName">The related resource's type name (see <see cref="ResourceCatalog.GetTypeName"/>).</param>
    /// <param name="Property">The item property that holds the related resource's id.</param>
    /// <param name="Path">The path of the related resource, including the request's path base.</param>
    public sealed record RelatedResource(string TypeName, string Property, string Path);
}
