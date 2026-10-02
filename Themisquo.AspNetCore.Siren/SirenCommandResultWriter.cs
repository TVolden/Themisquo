using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Themisquo.AspNetCore.Siren
{
    /// <summary>
    /// Writes command responses. When the command has a Location, the query mapped on the Location's route is dispatched
    /// and the endpoint responds 201 Created with the created resource as a Siren entity, the same entity a GET of the
    /// Location returns. Otherwise it responds as without Siren: 201 Created with only the Location header when no query
    /// route matches, or when the query fails or returns <c>null</c>, and 200 OK when there's no Location.
    /// </summary>
    /// <remarks>
    /// The query runs right after the command, in the same request. A read model that is updated asynchronously may
    /// not have caught up yet, which is why a failing query falls back instead of failing the request.
    /// </remarks>
    public class SirenCommandResultWriter : ICommandResultWriter
    {
        private readonly JsonSerializerOptions jsonOptions;
        private readonly ResourceCatalog resources;
        private readonly SirenEntityBuilder builder;
        private readonly ILogger<SirenCommandResultWriter> logger;

        public SirenCommandResultWriter(IOptions<HttpJsonOptions> jsonOptions, ResourceCatalog resources, ILogger<SirenCommandResultWriter> logger)
        {
            this.jsonOptions = jsonOptions.Value.SerializerOptions;
            this.resources = resources;
            this.logger = logger;
            builder = new SirenEntityBuilder(this.jsonOptions, resources);
        }

        public async Task<IResult> Write<TCommand>(HttpContext context, TCommand command, string? location, CancellationToken cancellationToken)
            where TCommand : ICommand
        {
            if (location is null)
            {
                return Results.Ok();
            }

            if (resources.FindQuery(location) is not { } resourceQuery)
            {
                return Results.Created(location, null);
            }

            object? result;
            try
            {
                result = await resourceQuery.DispatchAsync(context.RequestServices.GetRequiredService<IQueryDispatcher>(), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Command {CommandType} succeeded, but query {QueryType} for its location {Location} failed; responding without the created resource.",
                    typeof(TCommand).Name, resourceQuery.Endpoint.QueryType.Name, location);
                return Results.Created(location, null);
            }

            if (result is null)
            {
                return Results.Created(location, null);
            }

            var request = context.Request;
            var path = $"{request.PathBase}{location}";
            var entity = builder.Build(resourceQuery.Endpoint.QueryType, resourceQuery.Endpoint.ResultType, result, request,
                self: path, routePattern: resourceQuery.Endpoint.Pattern, path: path);
            context.Response.Headers.Location = location;
            return Results.Json(entity, jsonOptions, SirenQueryResultWriter.ContentType, StatusCodes.Status201Created);
        }
    }
}
