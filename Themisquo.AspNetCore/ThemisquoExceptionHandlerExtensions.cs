using Microsoft.Extensions.DependencyInjection;

namespace Themisquo.AspNetCore;

public static class ThemisquoExceptionHandlerExtensions
{
    public static IServiceCollection AddThemisquoExceptionHandling(
        this IServiceCollection services,
        Action<ThemisquoExceptionHandlerOptions>? configure = null)
    {
        services.AddExceptionHandler<ThemisquoExceptionHandler>();
        services.AddProblemDetails();
        if (configure is not null)
            services.Configure(configure);
        return services;
    }
}
