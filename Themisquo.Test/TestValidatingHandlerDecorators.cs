using FluentValidation;
using NSubstitute;
using Themisquo.FluentValidation;

namespace Themisquo.Test
{
    [TestClass]
    public class TestValidatingDispatcher
    {
        [TestMethod]
        public async Task Dispatch_ValidCommand_CallsInnerDispatcher()
        {
            // Given
            var command = new CommandStub();
            var innerDispatcher = Substitute.For<IDispatcher>();
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IValidator<CommandStub>)).Returns(new PassingCommandValidator());
            var sut = new ValidatingDispatcher(innerDispatcher, provider);

            // When
            await sut.Dispatch(command);

            // Then
            await innerDispatcher.Received().Dispatch(command);
        }

        [TestMethod]
        public async Task Dispatch_InvalidCommand_ThrowsValidationException()
        {
            // Given
            var command = new CommandStub();
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IValidator<CommandStub>)).Returns(new FailingCommandValidator());
            var sut = new ValidatingDispatcher(Substitute.For<IDispatcher>(), provider);

            // When / Then
            await Assert.ThrowsExceptionAsync<ValidationException>(async () =>
                await sut.Dispatch(command));
        }

        [TestMethod]
        public async Task Dispatch_InvalidCommand_InnerDispatcherNotCalled()
        {
            // Given
            var command = new CommandStub();
            var innerDispatcher = Substitute.For<IDispatcher>();
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IValidator<CommandStub>)).Returns(new FailingCommandValidator());
            var sut = new ValidatingDispatcher(innerDispatcher, provider);

            // When
            try { await sut.Dispatch(command); } catch (ValidationException) { }

            // Then
            await innerDispatcher.DidNotReceive().Dispatch(Arg.Any<ICommand>());
        }

        [TestMethod]
        public async Task Dispatch_NoValidatorForCommand_CallsInnerDispatcher()
        {
            // Given
            var command = new CommandStub();
            var innerDispatcher = Substitute.For<IDispatcher>();
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IValidator<CommandStub>)).Returns(null);
            var sut = new ValidatingDispatcher(innerDispatcher, provider);

            // When
            await sut.Dispatch(command);

            // Then
            await innerDispatcher.Received().Dispatch(command);
        }

        [TestMethod]
        public async Task Dispatch_InvalidQuery_ThrowsValidationException()
        {
            // Given
            var query = new QueryStub();
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IValidator<QueryStub>)).Returns(new FailingQueryValidator());
            var sut = new ValidatingDispatcher(Substitute.For<IDispatcher>(), provider);

            // When / Then
            await Assert.ThrowsExceptionAsync<ValidationException>(async () =>
                await sut.Dispatch(query));
        }

        [TestMethod]
        public async Task Dispatch_ValidQuery_ReturnsResultFromInnerDispatcher()
        {
            // Given
            var expected = 42;
            var query = new QueryStub();
            var innerDispatcher = Substitute.For<IDispatcher>();
            innerDispatcher.Dispatch(query).Returns(expected);
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IValidator<QueryStub>)).Returns(new PassingQueryValidator());
            var sut = new ValidatingDispatcher(innerDispatcher, provider);

            // When
            var result = await sut.Dispatch(query);

            // Then
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public async Task Dispatch_NoValidatorForQuery_ReturnsResultFromInnerDispatcher()
        {
            // Given
            var expected = 42;
            var query = new QueryStub();
            var innerDispatcher = Substitute.For<IDispatcher>();
            innerDispatcher.Dispatch(query).Returns(expected);
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(IValidator<QueryStub>)).Returns(null);
            var sut = new ValidatingDispatcher(innerDispatcher, provider);

            // When
            var result = await sut.Dispatch(query);

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
