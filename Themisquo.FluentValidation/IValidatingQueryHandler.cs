using FluentValidation;
using Themisquo;

namespace Themisquo.FluentValidation
{
    public interface IValidatingQueryHandler<TQuery, TResult, TValidator> : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
        where TValidator : IValidator<TQuery>
    {
    }
}
