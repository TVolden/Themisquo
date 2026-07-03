using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using System.ComponentModel;
using System.Reflection;
using Themisquo;

namespace Themisquo.AspNetCore;

public static class ThemisquoEndpointExtensions
{
    public static IEndpointRouteBuilder MapCommand<TCommand>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string httpMethod = "POST")
        where TCommand : ICommand, new()
    {
        endpoints.MapMethods(pattern, [httpMethod], async (HttpContext context, IDispatcher dispatcher) =>
        {
            var command = context.Request.HasJsonContentType()
                ? await context.Request.ReadFromJsonAsync<TCommand>()
                    ?? throw new BadHttpRequestException("Request body is required.")
                : new TCommand();

            BindRouteValues(command, context.Request.RouteValues);

            await dispatcher.Dispatch(command);
            return Results.Ok();
        });
        return endpoints;
    }

    private static void BindRouteValues(object target, RouteValueDictionary routeValues)
    {
        var properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var (key, value) in routeValues)
        {
            var property = properties.FirstOrDefault(p =>
                string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

            if (property is not { CanWrite: true } || value is null)
                continue;

            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            if (converter.CanConvertFrom(typeof(string)))
            {
                property.SetValue(target, converter.ConvertFromString(value.ToString()!));
            }
        }
    }

    public static IEndpointRouteBuilder MapCommand(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Type commandType,
        string httpMethod = "POST")
    {
        var methodInfo = typeof(ThemisquoEndpointExtensions).GetMethod(nameof(MapCommand), [typeof(IEndpointRouteBuilder), typeof(string), typeof(string)]);
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
        endpoints.MapMethods(pattern, [httpMethod], async ([AsParameters] TQuery query, IQueryDispatcher dispatcher) =>
        {
            var result = await dispatcher.Dispatch(query);
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
        var methodInfo = typeof(ThemisquoEndpointExtensions).GetMethod(nameof(MapQuery), [typeof(IEndpointRouteBuilder), typeof(string), typeof(string)]);
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
