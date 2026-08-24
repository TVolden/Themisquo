using Microsoft.Extensions.DependencyInjection;

namespace Themisquo.AspNetCore
{
    public static class ThemisquoCommandLocationExtensions
    {
        /// <summary>
        /// Enables Location header support for command endpoints: wraps the <see cref="ServiceProviderEventDispatcher"/>
        /// registered by <c>AddThemisquo()</c> so that events dispatched while handling a command are recorded. If one
        /// of them is decorated with <see cref="LocationAttribute"/>,
        /// <see cref="ThemisquoEndpointExtensions.MapCommand{TCommand}"/> responds 201 Created with a Location header
        /// resolved from that event instead of 200 OK.
        /// </summary>
        /// <remarks>Call this after <c>AddThemisquo()</c>, since it replaces the <see cref="IEventDispatcher"/> registration.</remarks>
        public static IServiceCollection AddThemisquoCommandLocations(this IServiceCollection services)
        {
            services.AddScoped<RecordedEvents>();
            services.AddScoped<ServiceProviderEventDispatcher>();
            services.AddScoped<IEventDispatcher>(sp => new RecordingEventDispatcher(
                sp.GetRequiredService<ServiceProviderEventDispatcher>(),
                sp.GetRequiredService<RecordedEvents>()));
            return services;
        }
    }
}
