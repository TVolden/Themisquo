using System;
using System.Threading.Tasks;
using FluentValidation;
using Themisquo;

namespace Themisquo.FluentValidation
{
    public sealed class ValidatingQueryHandlerDecorator<TQuery, TResult, TValidator> : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
        where TValidator : IValidator<TQuery>
    {
        private readonly IQueryHandler<TQuery, TResult> inner;
        private readonly TValidator validator;

        public ValidatingQueryHandlerDecorator(IQueryHandler<TQuery, TResult> inner, TValidator validator)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public async Task<TResult> Handle(TQuery query)
        {
            var result = await validator.ValidateAsync(query);
            if (!result.IsValid)
                throw new ValidationException(result.Errors);
            return await inner.Handle(query);
        }
    }
}
