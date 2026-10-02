using System;
using System.Collections.Generic;

namespace Themisquo.AspNetCore
{
    /// <summary>Thrown by <see cref="ThemisquoResourceActionValidationExtensions.ValidateResourceActions"/>.</summary>
    public class InvalidResourceActionException : Exception
    {
        /// <summary>One description per invalid declaration.</summary>
        public IReadOnlyList<string> Errors { get; }

        public InvalidResourceActionException(IReadOnlyList<string> errors) :
            base($"Found {errors.Count} invalid action declaration(s):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}")
        {
            Errors = errors;
        }
    }
}
