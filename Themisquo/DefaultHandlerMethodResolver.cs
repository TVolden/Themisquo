using System;
using System.Reflection;

namespace Themisquo
{
    public class DefaultHandlerMethodResolver : IHandlerMethodResolver
    {
        public MethodInfo Resolve(Type handlerType, Type openGenericInterface, Type fallback)
        {
            var iface = Array.Find(handlerType.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface)
                ?? fallback;
            return iface.GetMethod("Handle")!;
        }
    }
}
