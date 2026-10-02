namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Declares an external link on the entity a query returns; for a list query, on the collection. The URL template's
    /// placeholders are filled like a declared action's. See <see cref="ResourceLinkAttribute{TQuery}"/> for a query.
    /// <code>
    /// [ResourceLink("docs", "https://docs.example/projects/{projectId}")]
    /// public record GetProjectQuery(Guid ProjectId) : IQuery&lt;IProject&gt;;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class ResourceLinkAttribute : DeclaredLinkAttribute
    {
        /// <param name="rel">The link's relation.</param>
        /// <param name="urlTemplate">A URL relative to the request's path base, or an absolute http(s) URL.</param>
        public ResourceLinkAttribute(string rel, string urlTemplate) : base(rel, urlTemplate)
        {
        }

        private protected ResourceLinkAttribute(Type queryType) : base(queryType)
        {
        }
    }

    /// <summary>
    /// Declares a link from the entity a query returns (for a list query, from the collection) to a mapped GET query. The
    /// query's route is resolved like a declared command's. <typeparamref name="TQuery"/> must be a mapped GET query,
    /// which <see cref="ThemisquoResourceLinkValidationExtensions.ValidateResourceLinks"/> checks at startup.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class ResourceLinkAttribute<TQuery> : ResourceLinkAttribute where TQuery : class
    {
        public ResourceLinkAttribute() : base(typeof(TQuery))
        {
        }
    }
}
