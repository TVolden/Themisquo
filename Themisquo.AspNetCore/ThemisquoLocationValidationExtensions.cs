using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Scans event types for <see cref="LocationAttribute"/> and verifies every "{placeholder}" in the template
    /// resolves against a public property, so a typo in the template is caught at startup instead of when a
    /// command endpoint tries to build the Location header at runtime.
    /// </summary>
    public static class ThemisquoLocationValidationExtensions
    {
        /// <exception cref="InvalidLocationTemplateException">Thrown when one or more location templates are invalid.</exception>
        public static IServiceProvider ValidateLocationTemplatesRegistered(this IServiceProvider provider, params Assembly[] assemblies)
        {
            var assembliesToScan = assemblies.Length > 0 ? assemblies : GetAssembliesReferencingThemisquo();
            return provider.ValidateLocationTemplatesRegistered(assembliesToScan.SelectMany(assembly => assembly.GetTypes()));
        }

        /// <exception cref="InvalidLocationTemplateException">Thrown when one or more location templates are invalid.</exception>
        public static IServiceProvider ValidateLocationTemplatesRegistered(this IServiceProvider provider, IEnumerable<Type> types)
        {
            var invalidTemplates = new List<(Type EventType, string UrlTemplate, IReadOnlyList<string> UnresolvedPlaceholders)>();

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface || !typeof(IEvent).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.GetCustomAttribute<LocationAttribute>() is not { } locationAttribute)
                {
                    continue;
                }

                var unresolved = LocationTemplate.GetUnresolvedPlaceholders(locationAttribute.UrlTemplate, type).ToList();
                if (unresolved.Count > 0)
                {
                    invalidTemplates.Add((type, locationAttribute.UrlTemplate, unresolved));
                }
            }

            if (invalidTemplates.Count > 0)
            {
                throw new InvalidLocationTemplateException(invalidTemplates);
            }

            return provider;
        }

        private static IEnumerable<Assembly> GetAssembliesReferencingThemisquo()
        {
            var coreAssemblyName = typeof(ICommand).Assembly.GetName().Name;
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name == coreAssemblyName
                    || assembly.GetReferencedAssemblies().Any(reference => reference.Name == coreAssemblyName));
        }
    }
}
