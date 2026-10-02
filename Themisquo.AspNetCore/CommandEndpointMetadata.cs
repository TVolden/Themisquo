namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Endpoint metadata attached by <see cref="ThemisquoEndpointExtensions.MapCommand{TCommand}"/>, so other
    /// components can find the commands that act on a route.
    /// </summary>
    public sealed record CommandEndpointMetadata(Type CommandType, string Pattern, string HttpMethod);
}
