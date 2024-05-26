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
        Task Handle(TCommand command, IEventDispatcher eventDispatcher);
    }
}
