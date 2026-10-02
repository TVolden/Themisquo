using System.Reflection;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// The query that returns the resource at a path, bound from the path's route values; found by
    /// <see cref="ResourceCatalog.FindQuery"/>.
    /// </summary>
    /// <param name="Endpoint">The query endpoint whose route matches the path.</param>
    /// <param name="Query">The query, an instance of <see cref="QueryEndpointMetadata.QueryType"/>.</param>
    public sealed record ResourceQuery(QueryEndpointMetadata Endpoint, object Query)
    {
        private static readonly MethodInfo DispatchTypedMethod =
            typeof(ResourceQuery).GetMethod(nameof(DispatchTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>Dispatches <see cref="Query"/> and returns its result.</summary>
        public Task<object?> DispatchAsync(IQueryDispatcher dispatcher, CancellationToken cancellationToken) =>
            (Task<object?>)DispatchTypedMethod.MakeGenericMethod(Endpoint.ResultType).Invoke(null, [dispatcher, Query, cancellationToken])!;

        private static async Task<object?> DispatchTyped<TResult>(IQueryDispatcher dispatcher, object query, CancellationToken cancellationToken) =>
            await dispatcher.Dispatch((IQuery<TResult>)query, cancellationToken);
    }
}
