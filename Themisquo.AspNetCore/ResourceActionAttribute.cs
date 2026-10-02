namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Declares an external action on the entity a query returns; for a list query, on the collection. The URL template's
    /// placeholders are filled like a link's. See <see cref="ResourceActionAttribute{TCommand}"/> for a command.
    /// <code>
    /// [ResourceAction("export", "POST", "https://export.example/projects/{projectId}", Title = "Export project")]
    /// public record GetProjectQuery(Guid ProjectId) : IQuery&lt;IProject&gt;;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class ResourceActionAttribute : DeclaredActionAttribute
    {
        /// <param name="name">The action's name.</param>
        /// <param name="method">The HTTP method, such as <c>POST</c>.</param>
        /// <param name="urlTemplate">A URL relative to the request's path base, or an absolute http(s) URL.</param>
        public ResourceActionAttribute(string name, string method, string urlTemplate) : base(name, method, urlTemplate)
        {
        }

        private protected ResourceActionAttribute(Type commandType) : base(commandType)
        {
        }
    }

    /// <summary>
    /// Declares a mapped command as an action on the entity a query returns; for a list query, on the collection. The
    /// command's route is resolved like a link's, and its name, title and fields come from it as for automatic actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class ResourceActionAttribute<TCommand> : ResourceActionAttribute where TCommand : ICommand
    {
        public ResourceActionAttribute() : base(typeof(TCommand))
        {
        }
    }
}
