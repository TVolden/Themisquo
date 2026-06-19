using System;

namespace Themisquo
{
    public class HandlerMissingException : Exception
    {
        public HandlerMissingException(Type expectedType, Type dataType) : 
            base ($"Missing handler ({expectedType.Name}) for {dataType}.")
        {
        }
    }
}