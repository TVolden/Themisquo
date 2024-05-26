using System;
using System.Threading.Tasks;

namespace Themisquo
{
    // This class is based on the Message class from https://github.com/vkhorikov/CqrsInPractice
    public sealed class Dispatcher : IDispatcher
    {
        private readonly IServiceProvider provider;
        private readonly IEventDispatcher dispatcher;

        public Dispatcher(IServiceProvider serviceProvider, IEventDispatcher eventDispatcher)
        {
            provider = serviceProvider ?? throw new ArgumentNullException(nameof(IServiceProvider));
            dispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        }

        public async Task Dispatch(ICommand command)
        {
            // Identify command handler
            Type commandHandlerType = typeof(ICommandHandler<>);
            Type[] commandType = { command.GetType() };
            Type genericHandlerType = commandHandlerType.MakeGenericType(commandType);

            // Engage command handler
            dynamic handler = provider.GetService(genericHandlerType) ?? throw new HandlerMissingException(genericHandlerType, command.GetType());

            // Prevent event dispatcher from accidently being persisted
            using var temp = new DisposableEventDispatcher(dispatcher);
            await handler.Handle((dynamic)command, temp);
        }

        public async Task<T> Dispatch<T>(IQuery<T> query)
        {
            // Identify query handler
            Type queryHandlerType = typeof(IQueryHandler<,>);
            Type[] queryType = { query.GetType(), typeof(T) };
            Type genericHandlerType = queryHandlerType.MakeGenericType(queryType);

            // Engage query handler
            dynamic handler = provider.GetService(genericHandlerType);
            T result = await handler.Handle((dynamic)query);
            return result;
        }
    }
}
