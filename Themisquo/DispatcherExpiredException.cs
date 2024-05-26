using System;
using System.Runtime.Serialization;

namespace Themisquo
{
    [Serializable]
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

        protected DispatcherExpiredException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}