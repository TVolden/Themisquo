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
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IValidator).IsAssignableFrom(t))
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

        public static IServiceCollection AddValidatingDispatcher(this IServiceCollection services)
        {
            services.AddScoped<IDispatcher>(sp =>
                new ValidatingDispatcher(sp.GetRequiredService<Dispatcher>(), sp));
            services.AddScoped<IQueryDispatcher>(sp => sp.GetRequiredService<IDispatcher>());
            return services;
        }
    }
}
