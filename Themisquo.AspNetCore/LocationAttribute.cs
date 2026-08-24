using System;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Marks an <see cref="IEvent"/> as identifying the location of the resource it concerns. When a command raises
    /// an event carrying this attribute, <see cref="ThemisquoEndpointExtensions.MapCommand{TCommand}"/> resolves
    /// <paramref name="urlTemplate"/>'s "{placeholder}" segments against the event's public properties
    /// (case-insensitively) and responds 201 Created with the result as the Location header, instead of 200 OK.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LocationAttribute(string urlTemplate) : Attribute
    {
        public string UrlTemplate { get; } = urlTemplate ?? throw new ArgumentNullException(nameof(urlTemplate));
    }
}
