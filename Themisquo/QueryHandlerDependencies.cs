using System;
using System.Collections.Generic;
using System.Linq;

namespace Themisquo
{
    /// <summary>
    /// Finds constructor dependencies that would let a query handler change state: <see cref="IDispatcher"/>,
    /// <see cref="IEventDispatcher"/> or <see cref="ICommandHandler{TCommand}"/>, including when wrapped in another
    /// generic type such as <c>Lazy&lt;IDispatcher&gt;</c> or <c>IEnumerable&lt;ICommandHandler&lt;T&gt;&gt;</c>.
    /// The Themisquo.Analyzers rule THQ001 applies the same rules at compile time.
    /// </summary>
    internal static class QueryHandlerDependencies
    {
        public static IEnumerable<Type> FindForbidden(Type handlerType) =>
            handlerType.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Where(IsForbidden)
                .Distinct();

        public static void EnsureAllowed(Type handlerType)
        {
            var forbidden = FindForbidden(handlerType).Select(dependency => (handlerType, dependency)).ToList();
            if (forbidden.Count > 0)
            {
                throw new QueryHandlerDependencyException(forbidden);
            }
        }

        private static bool IsForbidden(Type type)
        {
            if (typeof(IDispatcher).IsAssignableFrom(type) || typeof(IEventDispatcher).IsAssignableFrom(type))
            {
                return true;
            }

            if (IsCommandHandler(type) || type.GetInterfaces().Any(IsCommandHandler))
            {
                return true;
            }

            if (type.HasElementType)
            {
                return IsForbidden(type.GetElementType()!);
            }

            return type.IsGenericType && type.GetGenericArguments().Any(IsForbidden);
        }

        private static bool IsCommandHandler(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandHandler<>);
    }
}
