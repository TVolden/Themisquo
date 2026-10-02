using Microsoft.AspNetCore.Http;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Writes the response of a command endpoint mapped by <see cref="ThemisquoEndpointExtensions.MapCommand{TCommand}"/>,
    /// after the command has been dispatched. When no writer is registered, the endpoint responds 201 Created with a
    /// Location header if <paramref name="location"/> is set, and 200 OK otherwise.
    /// </summary>
    public interface ICommandResultWriter
    {
        /// <param name="location">
        /// The location resolved from a <see cref="LocationAttribute"/> event the command raised, or <c>null</c> if it
        /// raised none (or <c>AddThemisquoCommandLocations()</c> isn't enabled).
        /// </param>
        Task<IResult> Write<TCommand>(HttpContext context, TCommand command, string? location, CancellationToken cancellationToken)
            where TCommand : ICommand;
    }
}
