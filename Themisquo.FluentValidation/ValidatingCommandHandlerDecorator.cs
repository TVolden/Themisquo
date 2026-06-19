using System;
using System.Threading.Tasks;
using FluentValidation;
using Themisquo;

namespace Themisquo.FluentValidation
{
    public sealed class ValidatingCommandHandlerDecorator<TCommand, TValidator> : ICommandHandler<TCommand>
        where TCommand : ICommand
        where TValidator : IValidator<TCommand>
    {
        private readonly ICommandHandler<TCommand> inner;
        private readonly TValidator validator;

        public ValidatingCommandHandlerDecorator(ICommandHandler<TCommand> inner, TValidator validator)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public async Task Handle(TCommand command, IEventDispatcher dispatcher)
        {
            var result = await validator.ValidateAsync(command);
            if (!result.IsValid)
                throw new ValidationException(result.Errors);
            await inner.Handle(command, dispatcher);
        }
    }
}
