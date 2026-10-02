using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Themisquo.AspNetCore.Siren
{
    /// <summary>
    /// Writes query results as Siren entities, with a <c>self</c> link to the request and the actions mapped on the
    /// query's route. See <see cref="SirenEntityBuilder"/> for how the entity is built.
    /// </summary>
    public class SirenQueryResultWriter : IQueryResultWriter
    {
        public const string ContentType = "application/vnd.siren+json";

        private readonly JsonSerializerOptions jsonOptions;
        private readonly SirenEntityBuilder builder;

        public SirenQueryResultWriter(IOptions<HttpJsonOptions> jsonOptions, ResourceCatalog resources)
        {
            this.jsonOptions = jsonOptions.Value.SerializerOptions;
            builder = new SirenEntityBuilder(this.jsonOptions, resources);
        }

        public string MediaType => ContentType;

        public IResult Write<TQuery, TResult>(HttpContext context, TQuery query, TResult result) where TQuery : IQuery<TResult>
        {
            var request = context.Request;
            var entity = builder.Build(typeof(TQuery), typeof(TResult), result, request,
                self: $"{request.PathBase}{request.Path}{request.QueryString}",
                routePattern: context.GetEndpoint()?.Metadata.GetMetadata<QueryEndpointMetadata>()?.Pattern,
                path: $"{request.PathBase}{request.Path}");
            return Results.Json(entity, jsonOptions, ContentType);
        }
    }
}
