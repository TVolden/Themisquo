using System;

namespace Themisquo
{
    public class DispatcherExpiredException : Exception
    {
        public DispatcherExpiredException()
        {
        }

        public DispatcherExpiredException(string message) : base(message)
        {
        }

        public DispatcherExpiredException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
