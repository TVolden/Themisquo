namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Endpoint metadata attached by <see cref="ThemisquoEndpointExtensions.MapQuery{TQuery, TResult}"/>, so other
    /// components can find the route that serves a given query or result type.
    /// </summary>
    public sealed record QueryEndpointMetadata(Type QueryType, Type ResultType, string Pattern, string HttpMethod);
}
