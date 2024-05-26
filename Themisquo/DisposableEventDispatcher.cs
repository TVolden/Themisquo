using System;
using System.Threading.Tasks;

namespace Themisquo
{
    internal class DisposableEventDispatcher : IEventDispatcher, IDisposable
    {
        private IEventDispatcher? eventDispatcher;

        public DisposableEventDispatcher(IEventDispatcher eventDispatcher) {
            this.eventDispatcher = eventDispatcher;
        }

        public Task Dispatch(IEvent @event)
        {
            if (eventDispatcher == null)
                throw new DispatcherExpiredException();
            else
                return eventDispatcher.Dispatch(@event);
        }

        public void Dispose()
        {
            eventDispatcher = null;
        }
    }
}
