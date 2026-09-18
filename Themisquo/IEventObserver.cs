using System.Threading;
using System.Threading.Tasks;

namespace Themisquo
{
    public interface IEventObserver<TEvent> where TEvent : IEvent
    {
        /// <param name="event">The event to react to.</param>
        /// <param name="cancellationToken">
        /// Signals that the caller is no longer interested in the result. Themisquo never cancels an observer
        /// on your behalf between events, so treat this as advisory: fine to honor for non-critical work (e.g.
        /// forwarding it to an outbound call), but if this observer commits state that must not be left
        /// half-written, don't let it abort that commit — pass <see cref="CancellationToken.None"/> to it instead.
        /// </param>
        Task Invoke(TEvent @event, CancellationToken cancellationToken);
    }
}
