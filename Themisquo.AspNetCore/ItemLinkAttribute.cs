namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Declares an external link on each item of a list query. The URL template's placeholders are filled like a declared
    /// action's, from the item. See <see cref="ItemLinkAttribute{TQuery}"/> for a query.
    /// <code>
    /// [ItemLink("preview", "https://preview.example/cards/{cardId}")]
    /// public record GetProjectCards(Guid ProjectId) : IQuery&lt;IEnumerable&lt;ICard&gt;&gt;;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class ItemLinkAttribute : DeclaredLinkAttribute
    {
        /// <param name="rel">The link's relation.</param>
        /// <param name="urlTemplate">A URL relative to the request's path base, or an absolute http(s) URL.</param>
        public ItemLinkAttribute(string rel, string urlTemplate) : base(rel, urlTemplate)
        {
        }

        private protected ItemLinkAttribute(Type queryType) : base(queryType)
        {
        }
    }

    /// <summary>
    /// Declares a link from each item of a list query to a mapped GET query. The query's route is resolved like a declared
    /// command's, from the item. <typeparamref name="TQuery"/> must be a mapped GET query, which
    /// <see cref="ThemisquoResourceLinkValidationExtensions.ValidateResourceLinks"/> checks at startup.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class ItemLinkAttribute<TQuery> : ItemLinkAttribute where TQuery : class
    {
        public ItemLinkAttribute() : base(typeof(TQuery))
        {
        }
    }
}
