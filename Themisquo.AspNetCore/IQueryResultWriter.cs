using Microsoft.AspNetCore.Http;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Turns the result of a dispatched query into the response written by
    /// <see cref="ThemisquoEndpointExtensions.MapQuery{TQuery, TResult}"/>. When no writer is registered, the endpoint
    /// responds 200 OK with the result serialized as plain JSON.
    /// </summary>
    public interface IQueryResultWriter
    {
        IResult Write<TQuery, TResult>(HttpContext context, TQuery query, TResult result) where TQuery : IQuery<TResult>;
    }
}
