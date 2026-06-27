using System;
using System.Reflection;

namespace Themisquo
{
    public interface IHandlerMethodResolver
    {
        MethodInfo Resolve(Type handlerType, Type openGenericInterface, Type fallback);
    }
}
