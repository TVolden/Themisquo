using Microsoft.CSharp.RuntimeBinder;
using NSubstitute;

namespace Themisquo.Test
{
    [TestClass]
    public class TestDispatcher
    {

        [TestMethod]
        public void Instantiate_MissingServiceProvider_ThrowsArgumentNullException()
        {
            // Given
            IServiceProvider service = null;

            // When
            Assert.ThrowsException<ArgumentNullException>(() => {
                new Dispatcher(service, Substitute.For<IEventDispatcher>());
                });
        }

        [TestMethod]
        public void Instantiate_MissingEventDispatcher_ThrowsArgumentNullException()
        {
            // Given
            IEventDispatcher dispatcher = null;

            // When
            Assert.ThrowsException<ArgumentNullException>(() => {
                new Dispatcher(Substitute.For<IServiceProvider>(), dispatcher);
            });
        }

        [TestMethod]
        public async Task Dispatch_NoCommandHandler_ThrowsHandlerMissingException()
        {
            // Given
            var providerStub = Substitute.For<IServiceProvider>();
            var sut = new Dispatcher(providerStub, Substitute.For<IEventDispatcher>());
            var someCommandStub = Substitute.For<ICommand>();

            // When
            await Assert.ThrowsExceptionAsync<HandlerMissingException>(async () =>
            {
                await sut.Dispatch(someCommandStub);
            });
        }

        [TestMethod]
        public async Task Dispatch_AnyCommand_CallsGetService()
        {
            // Given
            var providerMock = Substitute.For<IServiceProvider>();
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());
            
            providerMock.GetService(Arg.Any<Type>()).Returns(Substitute.For<ICommandHandler<ICommand>>());

            // When
            await sut.Dispatch(Substitute.For<ICommand>());

            // Then
            providerMock.Received().GetService(Arg.Any<Type>());
        }

        [TestMethod]
        public async Task Dispatch_SpecificCommand_CallsGetServiceWithSpecificHandler()
        {
            // Given
            var commandStub = new CommandStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());

            providerMock.GetService(Arg.Any<Type>()).Returns(Substitute.For<ICommandHandler<ICommand>>());

            // When
            await sut.Dispatch(commandStub);

            // Then
            providerMock.Received().GetService(typeof(ICommandHandler<CommandStub>));
        }

        [TestMethod]
        public async Task Dispatch_KnownCommand_CallsHandleOnCommandHandlerWithCommand()
        {
            // Given
            var commandStub = new CommandStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var commandHandlerMock = Substitute.For<ICommandHandler<ICommand>>();
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());

            providerMock.GetService(Arg.Any<Type>()).Returns(commandHandlerMock);

            // When
            await sut.Dispatch(commandStub);

            // Then
            await commandHandlerMock.Received().Handle(commandStub, Arg.Any<IEventDispatcher>());
        }

        [TestMethod]
        public async Task EventDispatcherDispatch_InScope_EventIsDispatched()
        {
            // Given
            var commandStub = new CommandStub();
            var providerStub = Substitute.For<IServiceProvider>();
            var commandHandlerStub = Substitute.For<ICommandHandler<ICommand>>();
            providerStub.GetService(Arg.Any<Type>()).Returns(commandHandlerStub);
            var eventDispatcherMock = Substitute.For<IEventDispatcher>();
            var sut = new Dispatcher(providerStub, eventDispatcherMock);

            // When
            await commandHandlerStub.Handle(commandStub, Arg.Do<IEventDispatcher>(ed => ed.Dispatch(Substitute.For<IEvent>())));
            await sut.Dispatch(commandStub);

            // Then
            await eventDispatcherMock.Received().Dispatch(Arg.Any<IEvent>());
        }

        [TestMethod]
        public async Task EventDispatcherDispatch_OutOfScope_ThrowsException()
        {
            // Given
            IEventDispatcher persistedEventDispatcher = null;
            var commandStub = new CommandStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var commandHandlerMock = Substitute.For<ICommandHandler<ICommand>>();
            await commandHandlerMock.Handle(commandStub, Arg.Do<IEventDispatcher>(ed => persistedEventDispatcher = ed));
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());
            providerMock.GetService(Arg.Any<Type>()).Returns(commandHandlerMock);
            await sut.Dispatch(commandStub);

            // When
            await Assert.ThrowsExceptionAsync<DispatcherExpiredException>(async () =>
            {
                await persistedEventDispatcher.Dispatch(Substitute.For<IEvent>());
            });
        }

        [TestMethod]
        public async Task Dispatch_NoQueryHandler_ThrowsRuntimeBinderException()
        {
            // Given
            var queryStub = Substitute.For<IQuery<int>>();
            var sut = new Dispatcher(Substitute.For<IServiceProvider>(), Substitute.For<IEventDispatcher>());

            // When
            await Assert.ThrowsExceptionAsync<RuntimeBinderException>(async () =>
            {
                await sut.Dispatch(queryStub);
            });
        }

        [TestMethod]
        public async Task Dispatch_AnyQuery_CallsGetService()
        {
            // Given
            var providerMock = Substitute.For<IServiceProvider>();
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());

            providerMock.GetService(Arg.Any<Type>()).Returns(Substitute.For<IQueryHandler<IQuery<int>, int>>());

            // When
            await sut.Dispatch(Substitute.For<IQuery<int>>());

            // Then
            providerMock.Received().GetService(Arg.Any<Type>());
        }

        [TestMethod]
        public async Task Dispatch_SpecificQuery_CallsGetServiceWithSpecificHandler()
        {
            // Given
            var queryStub = new QueryStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());

            providerMock.GetService(typeof(IQueryHandler<QueryStub, int>)).Returns(Substitute.For< IQueryHandler<IQuery<int>, int> >());

            // When
            await sut.Dispatch(queryStub);

            // Then
            providerMock.Received().GetService(typeof(IQueryHandler<QueryStub, int>));
        }

        [TestMethod]
        public async Task Dispatch_KnownQuery_CallsHandleOnQueryHandlerWithQuery()
        {
            // Given
            var queryStub = new QueryStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var queryHandler = Substitute.For<IQueryHandler<IQuery<int>, int>>();
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());

            providerMock.GetService(typeof(IQueryHandler<QueryStub, int>)).Returns(queryHandler);

            // When
            await sut.Dispatch(queryStub);

            // Then
            await queryHandler.Received().Handle(queryStub);
        }

        [TestMethod]
        public async Task Dispatch_KnownQuery_ReturnsValueFromQueryHandler()
        {
            // Given
            var expected = 42;
            var queryStub = new QueryStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var queryHandler = Substitute.For<IQueryHandler<IQuery<int>, int>>();
            var sut = new Dispatcher(providerMock, Substitute.For<IEventDispatcher>());

            providerMock.GetService(typeof(IQueryHandler<QueryStub, int>)).Returns(queryHandler);
            queryHandler.Handle(queryStub).Returns(expected);

            // When
            var result = await sut.Dispatch(queryStub);

            // Then
            Assert.AreEqual(expected, result);
        }

        // Dummy classes for test
        public class CommandStub : ICommand
        {
            public Guid ProcessId => throw new NotImplementedException();

            public Guid Instance => throw new NotImplementedException();
        }

        public class QueryStub : IQuery<int> { }

        public class EventStub : IEvent
        {
            public DateTime EventTime => throw new NotImplementedException();
            public int Version => throw new NotImplementedException();
            public Guid ProcessId => throw new NotImplementedException();
        }
    }
}
