namespace Themisquo.AspNetCore
{
    /// <summary>
    /// A link declared on a query, for hypermedia formats: either to a mapped GET query (<see cref="QueryType"/>), or to
    /// an external URL template (<see cref="UrlTemplate"/>). See <see cref="ResourceLinkAttribute"/> and
    /// <see cref="ItemLinkAttribute"/>.
    /// </summary>
    public abstract class DeclaredLinkAttribute : Attribute
    {
        private protected DeclaredLinkAttribute(string rel, string urlTemplate)
        {
            Rel = rel;
            UrlTemplate = urlTemplate;
        }

        private protected DeclaredLinkAttribute(Type queryType)
        {
            QueryType = queryType;
        }

        /// <summary>The query whose route to link to, or <c>null</c> for an external link.</summary>
        public Type? QueryType { get; }

        /// <summary>
        /// The external link's URL template, relative to the request's path base or an absolute http(s) URL; <c>null</c>
        /// for a query link, which uses the query's route.
        /// </summary>
        public string? UrlTemplate { get; }

        /// <summary>
        /// The link's relation. For a query link, it defaults to the query's <see cref="ResourceAttribute.Rel"/>, else to
        /// its resource type name.
        /// </summary>
        public string? Rel { get; set; }
    }
}
