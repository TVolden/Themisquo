using System;
using System.Collections.Generic;
using System.Linq;

namespace Themisquo.AspNetCore
{
    public class InvalidLocationTemplateException : Exception
    {
        public IReadOnlyList<(Type EventType, string UrlTemplate, IReadOnlyList<string> UnresolvedPlaceholders)> InvalidTemplates { get; }

        public InvalidLocationTemplateException(
            IReadOnlyList<(Type EventType, string UrlTemplate, IReadOnlyList<string> UnresolvedPlaceholders)> invalidTemplates) :
            base(BuildMessage(invalidTemplates))
        {
            InvalidTemplates = invalidTemplates;
        }

        private static string BuildMessage(
            IReadOnlyList<(Type EventType, string UrlTemplate, IReadOnlyList<string> UnresolvedPlaceholders)> invalidTemplates)
        {
            var details = invalidTemplates.Select(t =>
                $"Event '{t.EventType.Name}' has [Location(\"{t.UrlTemplate}\")] with unresolved placeholder(s): {string.Join(", ", t.UnresolvedPlaceholders)}.");
            return $"Found {invalidTemplates.Count} invalid location template(s):{Environment.NewLine}{string.Join(Environment.NewLine, details)}";
        }
    }
}
