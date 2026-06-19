using Microsoft.Extensions.DependencyInjection;

namespace Themisquo
{
    public static class ThemisquoServiceExtensions
    {
        public static IServiceCollection AddThemisquo(this IServiceCollection services)
        {
            services.AddScoped<Dispatcher>();
            services.AddScoped<IDispatcher>(sp => sp.GetRequiredService<Dispatcher>());
            services.AddScoped<IQueryDispatcher>(sp => sp.GetRequiredService<Dispatcher>());
            services.AddScoped<IEventDispatcher, ServiceProviderEventDispatcher>();
            return services;
        }

        public static IServiceCollection AddCommandHandler<THandler, TCommand>(this IServiceCollection services)
            where THandler : class, ICommandHandler<TCommand>
            where TCommand : ICommand
        {
            services.AddScoped<ICommandHandler<TCommand>, THandler>();
            return services;
        }

        public static IServiceCollection AddQueryHandler<THandler, TQuery, TResult>(this IServiceCollection services)
            where THandler : class, IQueryHandler<TQuery, TResult>
            where TQuery : IQuery<TResult>
        {
            services.AddScoped<IQueryHandler<TQuery, TResult>, THandler>();
            return services;
        }

    }
}
