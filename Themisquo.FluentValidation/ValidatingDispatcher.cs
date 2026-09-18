using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Themisquo;

namespace Themisquo.FluentValidation
{
    public sealed class ValidatingDispatcher : IDispatcher
    {
        private readonly IDispatcher inner;
        private readonly IServiceProvider provider;

        public ValidatingDispatcher(IDispatcher inner, IServiceProvider provider)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task Dispatch(ICommand command, CancellationToken cancellationToken)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(command.GetType());
            if (provider.GetService(validatorType) is IValidator validator)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(command), cancellationToken);
                if (!result.IsValid)
                    throw new ValidationException(result.Errors);
            }
            await inner.Dispatch(command, cancellationToken);
        }

        public async Task<T> Dispatch<T>(IQuery<T> query, CancellationToken cancellationToken)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(query.GetType());
            if (provider.GetService(validatorType) is IValidator validator)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(query), cancellationToken);
                if (!result.IsValid)
                    throw new ValidationException(result.Errors);
            }
            return await inner.Dispatch(query, cancellationToken);
        }
    }
}
