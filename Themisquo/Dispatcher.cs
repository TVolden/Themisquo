using System;
using System.Threading.Tasks;

namespace Themisquo
{
    // This class is based on the Message class from https://github.com/vkhorikov/CqrsInPractice
    public sealed class Dispatcher : IDispatcher
    {
        private readonly IServiceProvider provider;
        private readonly IEventDispatcher dispatcher;
        private readonly IHandlerMethodResolver resolver;

        public Dispatcher(IServiceProvider serviceProvider, IEventDispatcher eventDispatcher)
            : this(serviceProvider, eventDispatcher, new DefaultHandlerMethodResolver()) { }

        public Dispatcher(IServiceProvider serviceProvider, IEventDispatcher eventDispatcher, IHandlerMethodResolver methodResolver)
        {
            provider = serviceProvider ?? throw new ArgumentNullException(nameof(IServiceProvider));
            dispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            resolver = methodResolver ?? throw new ArgumentNullException(nameof(methodResolver));
        }

        public async Task Dispatch(ICommand command)
        {
            Type genericHandlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());

            object handler = provider.GetService(genericHandlerType) ?? throw new HandlerMissingException(genericHandlerType, command.GetType());

            using var temp = new DisposableEventDispatcher(dispatcher);
            var handleMethod = resolver.Resolve(handler.GetType(), typeof(ICommandHandler<>), genericHandlerType);
            await (Task)handleMethod.Invoke(handler, [command, temp]);
        }

        public async Task<T> Dispatch<T>(IQuery<T> query)
        {
            Type genericHandlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(T));

            object handler = provider.GetService(genericHandlerType) ?? throw new HandlerMissingException(genericHandlerType, query.GetType());

            var handleMethod = resolver.Resolve(handler.GetType(), typeof(IQueryHandler<,>), genericHandlerType);
            return await (Task<T>)handleMethod.Invoke(handler, [query]);
        }
    }
}
