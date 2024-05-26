using System;
using System.Threading.Tasks;

namespace Themisquo
{
    public sealed class ServiceProviderEventDispatcher : IEventDispatcher
    {
        private readonly IServiceProvider provider;

        public ServiceProviderEventDispatcher(IServiceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task Dispatch(IEvent @event)
        {
            // Identify event observer
            Type eventObserverType = typeof(IEventObserver<>);
            Type[] eventType = { @event.GetType() };
            Type genericObserverType = eventObserverType.MakeGenericType(eventType);

            // Invoke event observer
            dynamic observer = provider.GetService(genericObserverType);
            await observer.Invoke((dynamic)@event);
        }
    }
}
