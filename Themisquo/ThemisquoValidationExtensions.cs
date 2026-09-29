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
        /// Scans the given assemblies (or, if none are given, every loaded assembly that references this library)
        /// for commands, queries and events, and verifies that a corresponding handler
        /// (<see cref="ICommandHandler{TCommand}"/>, <see cref="IQueryHandler{TQuery, TResult}"/> or
        /// <see cref="IEventObserver{TEvent}"/>) can be resolved from the service provider. This lets missing
        /// registrations be caught at startup instead of when the command/query/event is dispatched at runtime.
        /// </summary>
        /// <exception cref="MissingHandlersException">Thrown when one or more handlers are missing.</exception>
        public static IServiceProvider ValidateHandlersRegistered(this IServiceProvider provider, params Assembly[] assemblies)
        {
            var assembliesToScan = assemblies.Length > 0 ? assemblies : GetAssembliesReferencingThemisquo();
            return provider.ValidateHandlersRegistered(assembliesToScan.SelectMany(assembly => assembly.GetTypes()));
        }

        private static IEnumerable<Assembly> GetAssembliesReferencingThemisquo()
        {
            var coreAssemblyName = typeof(ICommand).Assembly.GetName().Name;
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name == coreAssemblyName
                    || assembly.GetReferencedAssemblies().Any(reference => reference.Name == coreAssemblyName));
        }

        /// <summary>
        /// Verifies that a corresponding handler can be resolved for every command, query and event among the given
        /// types. Useful when the set of types to check has already been narrowed down by other means than a full
        /// assembly scan. Resolved query handlers are also checked for dependencies that would let them change state
        /// (<see cref="IDispatcher"/>, <see cref="IEventDispatcher"/> or <see cref="ICommandHandler{TCommand}"/>).
        /// </summary>
        /// <exception cref="MissingHandlersException">Thrown when one or more handlers are missing.</exception>
        /// <exception cref="QueryHandlerDependencyException">Thrown when one or more query handlers have forbidden dependencies.</exception>
        public static IServiceProvider ValidateHandlersRegistered(this IServiceProvider provider, IEnumerable<Type> types)
        {
            using var scope = provider.CreateScope();
            var (missingHandlers, dependencyViolations) = InspectHandlers(scope.ServiceProvider, types);
            if (missingHandlers.Count > 0)
            {
                throw new MissingHandlersException(missingHandlers);
            }
            if (dependencyViolations.Count > 0)
            {
                throw new QueryHandlerDependencyException(dependencyViolations);
            }
            return provider;
        }

        private static (List<(Type ExpectedType, Type DataType)> MissingHandlers, List<(Type HandlerType, Type DependencyType)> DependencyViolations)
            InspectHandlers(IServiceProvider provider, IEnumerable<Type> types)
        {
            var queryOpenType = typeof(IQuery<>);
            var missingHandlers = new List<(Type ExpectedType, Type DataType)>();
            var dependencyViolations = new List<(Type HandlerType, Type DependencyType)>();

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
                    var handler = AddIfMissing(typeof(IQueryHandler<,>).MakeGenericType(type, resultType), type);
                    if (handler != null)
                    {
                        var handlerType = handler.GetType();
                        dependencyViolations.AddRange(QueryHandlerDependencies.FindForbidden(handlerType)
                            .Select(dependency => (handlerType, dependency))
                            .Where(violation => !dependencyViolations.Contains(violation))
                            .ToList());
                    }
                }
            }

            return (missingHandlers, dependencyViolations);

            object? AddIfMissing(Type expectedHandlerType, Type dataType)
            {
                var handler = provider.GetService(expectedHandlerType);
                if (handler is null)
                {
                    missingHandlers.Add((expectedHandlerType, dataType));
                }
                return handler;
            }
        }
    }
}
