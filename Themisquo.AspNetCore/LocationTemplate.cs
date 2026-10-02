using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Resolves URL templates such as <c>/projects/{projectId}/cards/{cardId}</c> by filling each placeholder with a
    /// value. Route constraints and defaults (<c>{id:int}</c>, <c>{id?}</c>) are accepted and dropped.
    /// </summary>
    public static class LocationTemplate
    {
        private static readonly Regex PlaceholderPattern = new(@"\{(\w+)[^}]*\}", RegexOptions.Compiled);

        /// <summary>The placeholder names in the template, in order of appearance.</summary>
        public static IReadOnlyList<string> GetPlaceholders(string urlTemplate) =>
            PlaceholderPattern.Matches(urlTemplate).Select(match => match.Groups[1].Value).ToList();

        /// <summary>
        /// Fills each placeholder with the value <paramref name="valueFor"/> returns for its name, URL-escaped.
        /// Returns <c>null</c> when any placeholder has no value.
        /// </summary>
        public static string? TryResolve(string urlTemplate, Func<string, object?> valueFor)
        {
            var resolved = true;
            var url = PlaceholderPattern.Replace(urlTemplate, match =>
            {
                var value = valueFor(match.Groups[1].Value);
                if (value is null)
                {
                    resolved = false;
                    return "";
                }

                return Uri.EscapeDataString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
            });
            return resolved ? url : null;
        }

        internal static string Resolve(string urlTemplate, IEvent @event)
        {
            var eventType = @event.GetType();
            return TryResolve(urlTemplate, propertyName =>
            {
                var property = FindProperty(eventType, propertyName)
                    ?? throw new InvalidOperationException(
                        $"Location template '{urlTemplate}' on event '{eventType.Name}' references placeholder '{{{propertyName}}}', which has no matching public property.");
                return property.GetValue(@event) ?? "";
            })!;
        }

        internal static IEnumerable<string> GetUnresolvedPlaceholders(string urlTemplate, Type eventType) =>
            GetPlaceholders(urlTemplate).Where(propertyName => FindProperty(eventType, propertyName) is null);

        private static PropertyInfo? FindProperty(Type eventType, string propertyName) =>
            eventType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    }
}
