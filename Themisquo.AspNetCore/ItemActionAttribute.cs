namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Declares an external action on each item of a list query. The URL template's placeholders are filled like a
    /// link's, from the item. See <see cref="ItemActionAttribute{TCommand}"/> for a command.
    /// <code>
    /// [ItemAction("print", "POST", "https://print.example/cards/{cardId}", Title = "Print card")]
    /// public record GetCollectionCards(Guid ProjectId, Guid CollectionId) : IQuery&lt;IEnumerable&lt;ICard&gt;&gt;;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class ItemActionAttribute : DeclaredActionAttribute
    {
        /// <param name="name">The action's name.</param>
        /// <param name="method">The HTTP method, such as <c>POST</c>.</param>
        /// <param name="urlTemplate">A URL relative to the request's path base, or an absolute http(s) URL.</param>
        public ItemActionAttribute(string name, string method, string urlTemplate) : base(name, method, urlTemplate)
        {
        }

        private protected ItemActionAttribute(Type commandType) : base(commandType)
        {
        }
    }

    /// <summary>
    /// Declares a mapped command as an action on each item of a list query. The command's route is resolved like a
    /// link's, from the item, and its name, title and fields come from it as for automatic actions.
    /// <code>
    /// [Resource(AutoItemActions = false)]
    /// [ItemAction&lt;RemoveCardFromCollectionCommand&gt;]
    /// public record GetCollectionCards(Guid ProjectId, Guid CollectionId) : IQuery&lt;IEnumerable&lt;ICard&gt;&gt;;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class ItemActionAttribute<TCommand> : ItemActionAttribute where TCommand : ICommand
    {
        public ItemActionAttribute() : base(typeof(TCommand))
        {
        }
    }
}
