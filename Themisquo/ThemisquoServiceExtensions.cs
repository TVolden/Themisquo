using Microsoft.Extensions.DependencyInjection;

namespace Themisquo
{
    public static class ThemisquoServiceExtensions
    {
        public static IServiceCollection AddThemisquo(this IServiceCollection services)
        {
            services.AddSingleton<IHandlerMethodResolver, DefaultHandlerMethodResolver>();
            services.AddScoped<Dispatcher>();
            services.AddScoped<IDispatcher>(sp => sp.GetRequiredService<Dispatcher>());
            services.AddScoped<IQueryDispatcher>(sp => new QueryOnlyDispatcher(sp.GetRequiredService<Dispatcher>()));
            services.AddScoped<IEventDispatcher, ServiceProviderEventDispatcher>();
            return services;
        }

        public static IServiceCollection AddCachingDispatch(this IServiceCollection services)
        {
            services.AddSingleton<IHandlerMethodResolver>(sp =>
                new CachingHandlerMethodResolver(new DefaultHandlerMethodResolver()));
            return services;
        }

        public static IServiceCollection AddCommandHandler<THandler, TCommand>(this IServiceCollection services)
            where THandler : class, ICommandHandler<TCommand>
            where TCommand : ICommand
        {
            services.AddScoped<ICommandHandler<TCommand>, THandler>();
            return services;
        }

        /// <exception cref="QueryHandlerDependencyException">
        /// Thrown when <typeparamref name="THandler"/> depends on <see cref="IDispatcher"/>, <see cref="IEventDispatcher"/>
        /// or <see cref="ICommandHandler{TCommand}"/>, as that would allow a query to change state.
        /// </exception>
        public static IServiceCollection AddQueryHandler<THandler, TQuery, TResult>(this IServiceCollection services)
            where THandler : class, IQueryHandler<TQuery, TResult>
            where TQuery : IQuery<TResult>
        {
            QueryHandlerDependencies.EnsureAllowed(typeof(THandler));
            services.AddScoped<IQueryHandler<TQuery, TResult>, THandler>();
            return services;
        }

    }
}
