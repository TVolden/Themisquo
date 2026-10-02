namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Describes the resource a query returns, for hypermedia formats such as Siren, HAL or JSON:API. It goes on the
    /// query rather than the result type, so the result type needs no dependency on Themisquo.
    /// <code>
    /// [Endpoint("/projects/{projectId}/cards/{cardId}")]
    /// [Resource(Type = "card", Id = nameof(ICard.Id))]
    /// public record GetCardQuery(Guid ProjectId, Guid CardId) : IQuery&lt;ICard&gt;;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class ResourceAttribute : Attribute
    {
        /// <summary>
        /// The resource type name of the result, or of its elements for a list query, such as a Siren <c>class</c> or a
        /// JSON:API <c>type</c>. Without it, the name from a single-item query for the same result type is used, or
        /// else the result type's name in camelCase.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// The result property that holds the resource's primary id. On a single-item query, it fills the route's last
        /// placeholder when linking to the resource. Without it, the result's <c>Id</c> property is used.
        /// </summary>
        public string? Id { get; set; }
    }
}
