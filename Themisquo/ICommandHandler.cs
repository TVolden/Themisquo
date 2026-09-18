using System.Threading;
using System.Threading.Tasks;

namespace Themisquo
{
    public interface ICommandHandler<TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// Customize command handler for specific command. The event dispatcher is used to append events to the event source log.
        /// </summary>
        /// <param name="command">The parameters for the command being executed.</param>
        /// <param name="eventDispatcher">The event source dispatcher to commit new changes.</param>
        /// <param name="cancellationToken">
        /// Signals that the caller is no longer interested in the result. Honor it for work that hasn't
        /// committed yet; once you start a persistence operation that must not be left half-done, prefer
        /// completing it (or pass <see cref="CancellationToken.None"/> to that call) rather than aborting mid-way.
        /// </param>
        Task Handle(TCommand command, IEventDispatcher eventDispatcher, CancellationToken cancellationToken);
    }
}
