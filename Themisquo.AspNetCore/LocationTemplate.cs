using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Themisquo.AspNetCore
{
    internal static class LocationTemplate
    {
        private static readonly Regex PlaceholderPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

        public static string Resolve(string urlTemplate, IEvent @event)
        {
            var eventType = @event.GetType();
            return PlaceholderPattern.Replace(urlTemplate, match =>
            {
                var propertyName = match.Groups[1].Value;
                var property = FindProperty(eventType, propertyName)
                    ?? throw new InvalidOperationException(
                        $"Location template '{urlTemplate}' on event '{eventType.Name}' references placeholder '{{{propertyName}}}', which has no matching public property.");
                var value = property.GetValue(@event);
                return Uri.EscapeDataString(value?.ToString() ?? "");
            });
        }

        public static IEnumerable<string> GetUnresolvedPlaceholders(string urlTemplate, Type eventType)
        {
            foreach (Match match in PlaceholderPattern.Matches(urlTemplate))
            {
                var propertyName = match.Groups[1].Value;
                if (FindProperty(eventType, propertyName) is null)
                {
                    yield return propertyName;
                }
            }
        }

        private static PropertyInfo? FindProperty(Type eventType, string propertyName) =>
            eventType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    }
}
