using System;
using System.Threading;
using System.Threading.Tasks;

namespace Themisquo.AspNetCore
{
    internal sealed class RecordingEventDispatcher : IEventDispatcher
    {
        private readonly IEventDispatcher inner;
        private readonly RecordedEvents recordedEvents;

        public RecordingEventDispatcher(IEventDispatcher inner, RecordedEvents recordedEvents)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.recordedEvents = recordedEvents ?? throw new ArgumentNullException(nameof(recordedEvents));
        }

        public async Task Dispatch(IEvent @event, CancellationToken cancellationToken)
        {
            await inner.Dispatch(@event, cancellationToken);
            recordedEvents.Record(@event);
        }
    }
}
