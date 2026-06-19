using FluentValidation;
using NSubstitute;
using Themisquo.FluentValidation;

namespace Themisquo.Test
{
    [TestClass]
    public class TestValidatingHandlerDecorators
    {
        [TestMethod]
        public async Task Handle_ValidCommand_CallsInnerHandler()
        {
            // Given
            var command = new CommandStub();
            var innerHandler = Substitute.For<ICommandHandler<CommandStub>>();
            var sut = new ValidatingCommandHandlerDecorator<CommandStub, PassingCommandValidator>(
                innerHandler, new PassingCommandValidator());

            // When
            await sut.Handle(command, Substitute.For<IEventDispatcher>());

            // Then
            await innerHandler.Received().Handle(command, Arg.Any<IEventDispatcher>());
        }

        [TestMethod]
        public async Task Handle_InvalidCommand_ThrowsValidationException()
        {
            // Given
            var command = new CommandStub();
            var sut = new ValidatingCommandHandlerDecorator<CommandStub, FailingCommandValidator>(
                Substitute.For<ICommandHandler<CommandStub>>(), new FailingCommandValidator());

            // When / Then
            await Assert.ThrowsExceptionAsync<ValidationException>(async () =>
                await sut.Handle(command, Substitute.For<IEventDispatcher>()));
        }

        [TestMethod]
        public async Task Handle_InvalidCommand_InnerHandlerNotCalled()
        {
            // Given
            var command = new CommandStub();
            var innerHandler = Substitute.For<ICommandHandler<CommandStub>>();
            var sut = new ValidatingCommandHandlerDecorator<CommandStub, FailingCommandValidator>(
                innerHandler, new FailingCommandValidator());

            // When
            try { await sut.Handle(command, Substitute.For<IEventDispatcher>()); } catch (ValidationException) { }

            // Then
            await innerHandler.DidNotReceive().Handle(Arg.Any<CommandStub>(), Arg.Any<IEventDispatcher>());
        }

        [TestMethod]
        public async Task Handle_ValidCommand_PassesCommandToInnerHandler()
        {
            // Given
            var command = new CommandStub();
            var innerHandler = Substitute.For<ICommandHandler<CommandStub>>();
            var sut = new ValidatingCommandHandlerDecorator<CommandStub, PassingCommandValidator>(
                innerHandler, new PassingCommandValidator());

            // When
            await sut.Handle(command, Substitute.For<IEventDispatcher>());

            // Then
            await innerHandler.Received().Handle(command, Arg.Any<IEventDispatcher>());
        }

        [TestMethod]
        public async Task Handle_ValidCommand_PassesEventDispatcherToInnerHandler()
        {
            // Given
            var command = new CommandStub();
            var innerHandler = Substitute.For<ICommandHandler<CommandStub>>();
            var eventDispatcher = Substitute.For<IEventDispatcher>();
            var sut = new ValidatingCommandHandlerDecorator<CommandStub, PassingCommandValidator>(
                innerHandler, new PassingCommandValidator());

            // When
            await sut.Handle(command, eventDispatcher);

            // Then
            await innerHandler.Received().Handle(Arg.Any<CommandStub>(), eventDispatcher);
        }

        [TestMethod]
        public async Task Handle_InvalidQuery_ThrowsValidationException()
        {
            // Given
            var query = new QueryStub();
            var sut = new ValidatingQueryHandlerDecorator<QueryStub, int, FailingQueryValidator>(
                Substitute.For<IQueryHandler<QueryStub, int>>(), new FailingQueryValidator());

            // When / Then
            await Assert.ThrowsExceptionAsync<ValidationException>(async () =>
                await sut.Handle(query));
        }

        [TestMethod]
        public async Task Handle_ValidQuery_ReturnsResultFromInnerHandler()
        {
            // Given
            var expected = 42;
            var query = new QueryStub();
            var innerHandler = Substitute.For<IQueryHandler<QueryStub, int>>();
            innerHandler.Handle(query).Returns(expected);
            var sut = new ValidatingQueryHandlerDecorator<QueryStub, int, PassingQueryValidator>(
                innerHandler, new PassingQueryValidator());

            // When
            var result = await sut.Handle(query);

            // Then
            Assert.AreEqual(expected, result);
        }

        // Stubs
        public class CommandStub : ICommand
        {
            public Guid ProcessId { get; } = Guid.NewGuid();
            public Guid Instance { get; } = Guid.NewGuid();
        }

        public class QueryStub : IQuery<int> { }

        public class PassingCommandValidator : AbstractValidator<CommandStub> { }
        public class FailingCommandValidator : AbstractValidator<CommandStub>
        {
            public FailingCommandValidator() =>
                RuleFor(c => c.ProcessId).Must(_ => false).WithMessage("Always fails.");
        }

        public class PassingQueryValidator : AbstractValidator<QueryStub> { }
        public class FailingQueryValidator : AbstractValidator<QueryStub>
        {
            public FailingQueryValidator() =>
                RuleFor(q => q).Must(_ => false).WithMessage("Always fails.");
        }
    }
}
