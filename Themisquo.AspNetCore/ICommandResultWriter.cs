using Microsoft.AspNetCore.Http;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Writes the response of a command endpoint mapped by <see cref="ThemisquoEndpointExtensions.MapCommand{TCommand}"/>,
    /// after the command has been dispatched. When no writer is registered, or the request doesn't select one (see
    /// <see cref="ThemisquoResultWriterOptions"/>), the endpoint responds 201 Created with a Location header if the
    /// command raised a located event, and 200 OK otherwise.
    /// </summary>
    public interface ICommandResultWriter : IResultWriter
    {
        /// <param name="location">
        /// The location resolved from a <see cref="LocationAttribute"/> event the command raised, or <c>null</c> if it
        /// raised none (or <c>AddThemisquoCommandLocations()</c> isn't enabled).
        /// </param>
        Task<IResult> Write<TCommand>(HttpContext context, TCommand command, string? location, CancellationToken cancellationToken)
            where TCommand : ICommand;
    }
}
