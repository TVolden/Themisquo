using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Themisquo.AspNetCore.Siren
{
    public static class ThemisquoSirenExtensions
    {
        /// <summary>
        /// Lets query endpoints mapped by <see cref="ThemisquoEndpointExtensions.MapQuery{TQuery, TResult}"/> respond
        /// with Siren documents (<c>application/vnd.siren+json</c>) instead of plain JSON, and command endpoints that
        /// respond 201 Created include the created resource as a Siren document. A request gets Siren when its Accept
        /// header asks for it.
        /// </summary>
        /// <param name="asDefault">
        /// Also use Siren when the Accept header is missing, is a wildcard such as <c>*/*</c>, or asks for nothing
        /// supported. A request can still get plain JSON with <c>Accept: application/json</c>.
        /// </param>
        public static IServiceCollection AddThemisquoSiren(this IServiceCollection services, bool asDefault = false)
        {
            services.TryAddSingleton<ResourceCatalog>();
            services.AddSingleton<IQueryResultWriter, SirenQueryResultWriter>();
            services.AddSingleton<ICommandResultWriter, SirenCommandResultWriter>();
            if (asDefault)
            {
                services.Configure<ThemisquoResultWriterOptions>(options => options.DefaultMediaType = SirenQueryResultWriter.ContentType);
            }

            return services;
        }
    }
}
