using System;
using System.Threading;
using System.Threading.Tasks;

namespace Themisquo
{
    /// <summary>
    /// Exposes only the query side of a dispatcher. Registered as <see cref="IQueryDispatcher"/> so that consumers
    /// (query handlers in particular) cannot cast it back to <see cref="IDispatcher"/> and dispatch commands.
    /// </summary>
    public sealed class QueryOnlyDispatcher : IQueryDispatcher
    {
        private readonly IQueryDispatcher inner;

        public QueryOnlyDispatcher(IQueryDispatcher inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Task<T> Dispatch<T>(IQuery<T> query, CancellationToken cancellationToken) =>
            inner.Dispatch(query, cancellationToken);
    }
}
