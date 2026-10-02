using System;
using System.Collections.Generic;

namespace Themisquo.AspNetCore
{
    /// <summary>Thrown by <see cref="ThemisquoResourceLinkValidationExtensions.ValidateResourceLinks"/>.</summary>
    public class InvalidResourceLinkException : Exception
    {
        /// <summary>One description per invalid declaration.</summary>
        public IReadOnlyList<string> Errors { get; }

        public InvalidResourceLinkException(IReadOnlyList<string> errors) :
            base($"Found {errors.Count} invalid link declaration(s):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}")
        {
            Errors = errors;
        }
    }
}
