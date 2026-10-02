namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Describes a command as an action on a resource, for hypermedia formats such as Siren or HAL-FORMS. It is the
    /// command-side counterpart of <see cref="ResourceAttribute"/>:
    /// <code>
    /// [Action(Name = "rename", Title = "Rename card")]
    /// public record RenameCardCommand(Guid CardId, string Title) : ICommand { ... }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class ActionAttribute : Attribute
    {
        /// <summary>The action's name. Without it, the command type's name in camelCase is used.</summary>
        public string? Name { get; set; }

        /// <summary>A human-readable title for the action.</summary>
        public string? Title { get; set; }
    }
}
