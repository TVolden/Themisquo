using System;
using System.Collections.Generic;
using System.Linq;

namespace Themisquo
{
    public class MissingHandlersException : Exception
    {
        public IReadOnlyList<(Type ExpectedType, Type DataType)> MissingHandlers { get; }

        public MissingHandlersException(IReadOnlyList<(Type ExpectedType, Type DataType)> missingHandlers) :
            base(BuildMessage(missingHandlers))
        {
            MissingHandlers = missingHandlers;
        }

        private static string BuildMessage(IReadOnlyList<(Type ExpectedType, Type DataType)> missingHandlers)
        {
            var details = missingHandlers.Select(m => $"Missing handler ({m.ExpectedType.Name}) for {m.DataType}.");
            return $"Found {missingHandlers.Count} missing handler registration(s):{Environment.NewLine}{string.Join(Environment.NewLine, details)}";
        }
    }
}
