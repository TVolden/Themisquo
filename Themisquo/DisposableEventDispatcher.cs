using System;
using System.Threading;
using System.Threading.Tasks;

namespace Themisquo
{
    internal class DisposableEventDispatcher : IEventDispatcher, IDisposable
    {
        private IEventDispatcher? eventDispatcher;

        public DisposableEventDispatcher(IEventDispatcher eventDispatcher) {
            this.eventDispatcher = eventDispatcher;
        }

        public Task Dispatch(IEvent @event, CancellationToken cancellationToken)
        {
            if (eventDispatcher == null)
                throw new DispatcherExpiredException();
            else
                return eventDispatcher.Dispatch(@event, cancellationToken);
        }

        public void Dispose()
        {
            eventDispatcher = null;
        }
    }
}
