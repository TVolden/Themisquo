using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Themisquo
{
    public static class ThemisquoValidationExtensions
    {
        /// <summary>
        /// Scans the given assemblies for commands, queries and events, and verifies that a corresponding handler
        /// (<see cref="ICommandHandler{TCommand}"/>, <see cref="IQueryHandler{TQuery, TResult}"/> or
        /// <see cref="IEventObserver{TEvent}"/>) can be resolved from the service provider. This lets missing
        /// registrations be caught at startup instead of when the command/query/event is dispatched at runtime.
        /// </summary>
        /// <exception cref="MissingHandlersException">Thrown when one or more handlers are missing.</exception>
        public static IServiceProvider ValidateHandlersRegistered(this IServiceProvider provider, params Assembly[] assemblies)
        {
            return provider.ValidateHandlersRegistered(assemblies.SelectMany(assembly => assembly.GetTypes()));
        }

        /// <summary>
        /// Verifies that a corresponding handler can be resolved for every command, query and event among the given
        /// types. Useful when the set of types to check has already been narrowed down by other means than a full
        /// assembly scan.
        /// </summary>
        /// <exception cref="MissingHandlersException">Thrown when one or more handlers are missing.</exception>
        public static IServiceProvider ValidateHandlersRegistered(this IServiceProvider provider, IEnumerable<Type> types)
        {
            using var scope = provider.CreateScope();
            var missingHandlers = FindMissingHandlers(scope.ServiceProvider, types);
            if (missingHandlers.Count > 0)
            {
                throw new MissingHandlersException(missingHandlers);
            }
            return provider;
        }

        private static List<(Type ExpectedType, Type DataType)> FindMissingHandlers(IServiceProvider provider, IEnumerable<Type> types)
        {
            var queryOpenType = typeof(IQuery<>);
            var missingHandlers = new List<(Type ExpectedType, Type DataType)>();

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (typeof(ICommand).IsAssignableFrom(type))
                {
                    AddIfMissing(typeof(ICommandHandler<>).MakeGenericType(type), type);
                }

                if (typeof(IEvent).IsAssignableFrom(type))
                {
                    AddIfMissing(typeof(IEventObserver<>).MakeGenericType(type), type);
                }

                var queryInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == queryOpenType);
                if (queryInterface != null)
                {
                    var resultType = queryInterface.GetGenericArguments()[0];
                    AddIfMissing(typeof(IQueryHandler<,>).MakeGenericType(type, resultType), type);
                }
            }

            return missingHandlers;

            void AddIfMissing(Type expectedHandlerType, Type dataType)
            {
                if (provider.GetService(expectedHandlerType) is null)
                {
                    missingHandlers.Add((expectedHandlerType, dataType));
                }
            }
        }
    }
}
