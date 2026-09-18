using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Themisquo;

namespace Themisquo.AspNetCore;

public static class ThemisquoEndpointExtensions
{
    private static readonly JsonNodeOptions RouteBindingNodeOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions RouteBindingSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IEndpointRouteBuilder MapCommand<TCommand>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string httpMethod = "POST")
        where TCommand : ICommand
    {
        endpoints.MapMethods(pattern, [httpMethod], async (HttpContext context, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = await BindCommandAsync<TCommand>(context.Request);
            await dispatcher.Dispatch(command, cancellationToken);

            var recordedEvents = context.RequestServices.GetService<RecordedEvents>();
            var locatedEvent = recordedEvents?.Events.FirstOrDefault(e => e.GetType().IsDefined(typeof(LocationAttribute)));
            if (locatedEvent is not null)
            {
                var locationAttribute = locatedEvent.GetType().GetCustomAttribute<LocationAttribute>()!;
                var location = LocationTemplate.Resolve(locationAttribute.UrlTemplate, locatedEvent);
                return Results.Created(location, null);
            }

            return Results.Ok();
        });
        return endpoints;
    }

    private static async Task<TCommand> BindCommandAsync<TCommand>(HttpRequest request)
    {
        var json = request.HasJsonContentType()
            ? await JsonNode.ParseAsync(request.Body, RouteBindingNodeOptions) as JsonObject
                ?? new JsonObject(RouteBindingNodeOptions)
            : new JsonObject(RouteBindingNodeOptions);

        foreach (var (key, value) in request.RouteValues)
        {
            if (value is not null)
            {
                json[key] = JsonValue.Create(value.ToString());
            }
        }

        return json.Deserialize<TCommand>(RouteBindingSerializerOptions)
            ?? throw new BadHttpRequestException("Request body could not be bound.");
    }

    public static IEndpointRouteBuilder MapCommand(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Type commandType,
        string httpMethod = "POST")
    {
        var methodInfo = typeof(ThemisquoEndpointExtensions).GetMethod(nameof(MapCommand), [typeof(IEndpointRouteBuilder), typeof(string), typeof(string)])
            ?? throw new InvalidOperationException($"Could not resolve the generic {nameof(MapCommand)} method via reflection.");
        MethodInfo genericMethod = methodInfo.MakeGenericMethod(commandType);
        genericMethod.Invoke(obj: null, [endpoints, pattern, httpMethod]);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapQuery<TQuery, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string httpMethod = "GET")
        where TQuery : IQuery<TResult>
    {
        endpoints.MapMethods(pattern, [httpMethod], async ([AsParameters] TQuery query, IQueryDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(query, cancellationToken);
            return Results.Ok(result);
        });
        return endpoints;
    }
    public static IEndpointRouteBuilder MapQuery(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Type queryType,
        Type resultType,
        string httpMethod = "GET")
    {
        var methodInfo = typeof(ThemisquoEndpointExtensions).GetMethod(nameof(MapQuery), [typeof(IEndpointRouteBuilder), typeof(string), typeof(string)])
            ?? throw new InvalidOperationException($"Could not resolve the generic {nameof(MapQuery)} method via reflection.");
        MethodInfo genericMethod = methodInfo.MakeGenericMethod(queryType, resultType);
        genericMethod.Invoke(obj: null, [endpoints, pattern, httpMethod]);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapCQEndpoints(
        this IEndpointRouteBuilder endpoints,
        Assembly assembly,
        string baseUrl = "")
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(EndpointAttribute), false).FirstOrDefault() is EndpointAttribute endpointAttribute)
            {
                var url = $"{baseUrl}/{endpointAttribute.Url.TrimStart('/')}";
                if (typeof(ICommand).IsAssignableFrom(type))
                {
                    endpoints.MapCommand(url, type, endpointAttribute.Method ?? "POST");
                }
                else
                {
                    Type? queryInterface = type.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
                    if (queryInterface != null)
                    {
                        var resultType = queryInterface.GetGenericArguments()[0];
                        endpoints.MapQuery(url, type, resultType, endpointAttribute.Method ?? "GET");
                    }
                }
            }
        }
        return endpoints;
    }
}
