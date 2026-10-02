using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Themisquo.AspNetCore
{
    /// <summary>Picks the registered writer for a request by its Accept header (see <see cref="ThemisquoResultWriterOptions"/>).</summary>
    internal static class ResultWriterSelection
    {
        private static readonly MediaTypeHeaderValue PlainJson = new("application/json");

        /// <summary>The writer to use, or <c>null</c> for plain JSON. Adds <c>Vary: Accept</c> when writers are registered.</summary>
        public static TWriter? Select<TWriter>(HttpContext context) where TWriter : class, IResultWriter
        {
            var writers = context.RequestServices.GetServices<TWriter>().ToList();
            if (writers.Count == 0)
            {
                return null;
            }

            context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.Accept);

            var defaultMediaType = context.RequestServices.GetService<IOptions<ThemisquoResultWriterOptions>>()?.Value.DefaultMediaType;
            var defaultWriter = writers.FirstOrDefault(writer => IsMediaType(writer, defaultMediaType));

            // The default comes first, so a wildcard picks it.
            var candidates = new List<(MediaTypeHeaderValue MediaType, TWriter? Writer)>();
            candidates.Add(defaultWriter is not null ? (new MediaTypeHeaderValue(defaultWriter.MediaType), defaultWriter) : (PlainJson, null));
            if (defaultWriter is not null)
            {
                candidates.Add((PlainJson, null));
            }
            candidates.AddRange(writers.Where(writer => writer != defaultWriter).Select(writer => (new MediaTypeHeaderValue(writer.MediaType), (TWriter?)writer)));

            var accepted = context.Request.GetTypedHeaders().Accept
                .Where(mediaType => mediaType.Quality is not 0)
                .OrderByDescending(mediaType => mediaType.Quality ?? 1);
            foreach (var mediaType in accepted)
            {
                foreach (var candidate in candidates)
                {
                    if (Matches(candidate.MediaType, mediaType))
                    {
                        return candidate.Writer;
                    }
                }
            }

            return defaultWriter;
        }

        // An exact media type must match exactly: IsSubsetOf would count application/vnd.siren+json as
        // application/json because of its +json suffix. Wildcards (*/*, application/*, application/*+json) use it.
        private static bool Matches(MediaTypeHeaderValue candidate, MediaTypeHeaderValue accepted) =>
            accepted.MatchesAllTypes || accepted.MatchesAllSubTypes || accepted.MatchesAllSubTypesWithoutSuffix
                ? candidate.IsSubsetOf(accepted)
                : accepted.MediaType.Equals(candidate.MediaType, StringComparison.OrdinalIgnoreCase);

        private static bool IsMediaType(IResultWriter writer, string? mediaType) =>
            mediaType is not null && string.Equals(writer.MediaType, mediaType, StringComparison.OrdinalIgnoreCase);
    }
}
