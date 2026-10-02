using Microsoft.Extensions.DependencyInjection;

namespace Themisquo.AspNetCore.Siren
{
    public static class ThemisquoSirenExtensions
    {
        /// <summary>
        /// Makes query endpoints mapped by <see cref="ThemisquoEndpointExtensions.MapQuery{TQuery, TResult}"/> respond
        /// with Siren documents (<c>application/vnd.siren+json</c>) instead of plain JSON.
        /// </summary>
        public static IServiceCollection AddThemisquoSiren(this IServiceCollection services)
        {
            services.AddSingleton<IQueryResultWriter, SirenQueryResultWriter>();
            return services;
        }
    }
}
