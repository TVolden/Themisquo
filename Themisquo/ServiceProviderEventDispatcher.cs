using System;
using System.Threading.Tasks;

namespace Themisquo
{
    public sealed class ServiceProviderEventDispatcher : IEventDispatcher
    {
        private readonly IServiceProvider provider;
        private readonly IHandlerMethodResolver resolver;

        public ServiceProviderEventDispatcher(IServiceProvider provider)
            : this(provider, new DefaultHandlerMethodResolver()) { }

        public ServiceProviderEventDispatcher(IServiceProvider provider, IHandlerMethodResolver methodResolver)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            resolver = methodResolver ?? throw new ArgumentNullException(nameof(methodResolver));
        }

        public async Task Dispatch(IEvent @event)
        {
            // Identify event observer
            Type genericObserverType = typeof(IEventObserver<>).MakeGenericType(@event.GetType());

            object observer = provider.GetService(genericObserverType) ?? throw new HandlerMissingException(genericObserverType, @event.GetType());

            // Invoke event observer
            var invokeMethod = resolver.Resolve(observer.GetType(), typeof(IEventObserver<>), genericObserverType, "Invoke");
            await (Task)invokeMethod.Invoke(observer, [@event]);
        }
    }
}
