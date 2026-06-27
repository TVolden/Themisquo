using System;
using System.Linq;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Themisquo;

namespace Themisquo.FluentValidation
{
    public static class ThemisquoFluentValidationExtensions
    {
        public static IServiceCollection AddValidatorsFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            var commandType = typeof(ICommand);
            var queryOpenType = typeof(IQuery<>);
            var validatorOpenType = typeof(IValidator<>);

            var registrations = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorOpenType)
                    .Select(i => (ValidatorType: t, ValidatedType: i.GetGenericArguments()[0], Interface: i)))
                .Where(x =>
                    commandType.IsAssignableFrom(x.ValidatedType) ||
                    x.ValidatedType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == queryOpenType));

            foreach (var (validatorType, _, validatorInterface) in registrations)
            {
                services.TryAddScoped(validatorType);
                services.TryAddScoped(validatorInterface, validatorType);
            }

            return services;
        }

        public static IServiceCollection AddValidatingCommandHandler<THandler, TCommand, TValidator>(this IServiceCollection services)
            where THandler : class, IValidatingCommandHandler<TCommand, TValidator>
            where TCommand : ICommand
            where TValidator : class, IValidator<TCommand>
        {
            services.AddScoped<THandler>();
            services.TryAddScoped<TValidator>();
            services.AddScoped<ICommandHandler<TCommand>>(sp =>
                new ValidatingCommandHandlerDecorator<TCommand, TValidator>(
                    sp.GetRequiredService<THandler>(),
                    sp.GetRequiredService<TValidator>()));
            return services;
        }

        public static IServiceCollection AddValidatingQueryHandler<THandler, TQuery, TResult, TValidator>(this IServiceCollection services)
            where THandler : class, IValidatingQueryHandler<TQuery, TResult, TValidator>
            where TQuery : IQuery<TResult>
            where TValidator : class, IValidator<TQuery>
        {
            services.AddScoped<THandler>();
            services.TryAddScoped<TValidator>();
            services.AddScoped<IQueryHandler<TQuery, TResult>>(sp =>
                new ValidatingQueryHandlerDecorator<TQuery, TResult, TValidator>(
                    sp.GetRequiredService<THandler>(),
                    sp.GetRequiredService<TValidator>()));
            return services;
        }
    }
}
