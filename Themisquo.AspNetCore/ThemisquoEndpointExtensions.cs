using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Themisquo;

namespace Themisquo.AspNetCore;

public static class ThemisquoEndpointExtensions
{
    public static IEndpointRouteBuilder MapCommand<TCommand>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string httpMethod = "POST")
        where TCommand : ICommand
    {
        endpoints.MapMethods(pattern, [httpMethod], async ([FromBody] TCommand command, IDispatcher dispatcher) =>
        {
            await dispatcher.Dispatch(command);
            return Results.Ok();
        });
        return endpoints;
    }

    public static IEndpointRouteBuilder MapQuery<TQuery, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TQuery : IQuery<TResult>
    {
        endpoints.MapGet(pattern, async ([AsParameters] TQuery query, IQueryDispatcher dispatcher) =>
        {
            var result = await dispatcher.Dispatch<TResult>(query);
            return Results.Ok(result);
        });
        return endpoints;
    }
}
