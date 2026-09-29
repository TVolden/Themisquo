using System;
using System.Collections.Generic;
using System.Linq;

namespace Themisquo
{
    /// <summary>
    /// Thrown when a query handler depends on something that can change state (<see cref="IDispatcher"/>,
    /// <see cref="IEventDispatcher"/> or <see cref="ICommandHandler{TCommand}"/>), which would breach CQRS.
    /// Query handlers that need to run other queries should depend on <see cref="IQueryDispatcher"/> instead.
    /// </summary>
    public class QueryHandlerDependencyException : Exception
    {
        public IReadOnlyList<(Type HandlerType, Type DependencyType)> Violations { get; }

        public QueryHandlerDependencyException(IReadOnlyList<(Type HandlerType, Type DependencyType)> violations) :
            base(BuildMessage(violations))
        {
            Violations = violations;
        }

        private static string BuildMessage(IReadOnlyList<(Type HandlerType, Type DependencyType)> violations)
        {
            var details = violations.Select(v => $"Query handler {v.HandlerType} depends on {v.DependencyType}.");
            return $"Found {violations.Count} forbidden query handler dependency violation(s). Query handlers must not depend on " +
                $"{nameof(IDispatcher)}, {nameof(IEventDispatcher)} or ICommandHandler<T>; use {nameof(IQueryDispatcher)} instead:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, details)}";
        }
    }
}
