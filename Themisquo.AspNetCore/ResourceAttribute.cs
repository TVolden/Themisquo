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

        /// <summary>
        /// Adds the commands whose <see cref="ActionAttribute.Context"/> matches as actions on the entity the query
        /// returns; for a list query, on the collection.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// Adds the commands whose <see cref="ActionAttribute.Context"/> matches as actions on each item of a list query.
        /// </summary>
        public string? ItemContext { get; set; }

        /// <summary>
        /// Whether the commands mapped on the entity's route become its actions. Set it to <c>false</c> to get only the
        /// declared actions, or none. Defaults to <c>true</c>.
        /// </summary>
        public bool AutoActions { get; set; } = true;

        /// <summary>
        /// Whether the commands mapped on each list item's route become its actions. Set it to <c>false</c> to get only
        /// the declared item actions, or none. Defaults to <c>true</c>.
        /// </summary>
        public bool AutoItemActions { get; set; } = true;

        /// <summary>
        /// The relation of a link to this query's route, from a parent entity or a declared link. Without it, a link to
        /// a nested route uses the route's last segment, and a declared link uses the resource type name.
        /// </summary>
        public string? Rel { get; set; }

        /// <summary>
        /// Whether the entity the query returns gets automatic links: to the resources it refers to, and to the queries
        /// nested under its route. Its <c>self</c> link always stays. Defaults to <c>true</c>.
        /// </summary>
        public bool AutoLinks { get; set; } = true;

        /// <summary>
        /// Whether each item of a list query gets automatic links: to the resources it refers to, and to the queries
        /// nested under its route. Its <c>self</c> link always stays. Defaults to <c>true</c>.
        /// </summary>
        public bool AutoItemLinks { get; set; } = true;
    }
}
