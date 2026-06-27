using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Themisquo
{
    public class CachingHandlerMethodResolver : IHandlerMethodResolver
    {
        private readonly ConcurrentDictionary<(Type, Type), MethodInfo> _cache = new();
        private readonly IHandlerMethodResolver _inner;

        public CachingHandlerMethodResolver(IHandlerMethodResolver inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public MethodInfo Resolve(Type handlerType, Type openGenericInterface, Type fallback) =>
            _cache.GetOrAdd((handlerType, openGenericInterface),
                _ => _inner.Resolve(handlerType, openGenericInterface, fallback));
    }
}
