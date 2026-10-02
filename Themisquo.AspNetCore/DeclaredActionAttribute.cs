namespace Themisquo.AspNetCore
{
    /// <summary>
    /// An action declared on a query, for hypermedia formats: either a mapped command (<see cref="CommandType"/>), or an
    /// external action at a URL template (<see cref="Name"/>, <see cref="Method"/> and <see cref="UrlTemplate"/>). See
    /// <see cref="ResourceActionAttribute"/> and <see cref="ItemActionAttribute"/>.
    /// </summary>
    public abstract class DeclaredActionAttribute : Attribute
    {
        private protected DeclaredActionAttribute(string name, string method, string urlTemplate)
        {
            Name = name;
            Method = method;
            UrlTemplate = urlTemplate;
        }

        private protected DeclaredActionAttribute(Type commandType)
        {
            CommandType = commandType;
        }

        /// <summary>The command to send, or <c>null</c> for an external action.</summary>
        public Type? CommandType { get; }

        /// <summary>The external action's name; <c>null</c> for a command, which is named by its <see cref="ActionAttribute"/>.</summary>
        public string? Name { get; }

        /// <summary>The external action's HTTP method; <c>null</c> for a command, which uses its endpoint's method.</summary>
        public string? Method { get; }

        /// <summary>
        /// The external action's URL template, relative to the request's path base or an absolute http(s) URL; <c>null</c>
        /// for a command, which uses its endpoint's route.
        /// </summary>
        public string? UrlTemplate { get; }

        /// <summary>A human-readable title. For a command, it replaces the one from its <see cref="ActionAttribute"/>.</summary>
        public string? Title { get; set; }

        /// <summary>
        /// The body fields of an external action, sent as text. A command's fields come from its properties, so setting
        /// this on a command action is rejected at startup.
        /// </summary>
        public string[]? Fields { get; set; }
    }
}
