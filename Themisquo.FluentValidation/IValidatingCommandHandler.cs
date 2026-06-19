using FluentValidation;
using Themisquo;

namespace Themisquo.FluentValidation
{
    public interface IValidatingCommandHandler<TCommand, TValidator> : ICommandHandler<TCommand>
        where TCommand : ICommand
        where TValidator : IValidator<TCommand>
    {
    }
}
