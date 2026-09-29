using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Themisquo.Test
{
    [TestClass]
    public class TestQueryHandlerDependencies
    {
        [TestMethod]
        public void AddQueryHandler_HandlerDependsOnQueryDispatcher_Registers()
        {
            // Given
            var services = new ServiceCollection();

            // When
            services.AddQueryHandler<QueryDispatcherHandler, QueryStub, int>();

            // Then
            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(IQueryHandler<QueryStub, int>)));
        }

        [TestMethod]
        public void AddQueryHandler_HandlerDependsOnDispatcher_ThrowsQueryHandlerDependencyException()
        {
            // Given
            var services = new ServiceCollection();

            // When
            var exception = Assert.ThrowsException<QueryHandlerDependencyException>(() =>
            {
                services.AddQueryHandler<DispatcherHandler, QueryStub, int>();
            });

            // Then
            Assert.IsTrue(exception.Violations.Contains((typeof(DispatcherHandler), typeof(IDispatcher))));
        }

        [TestMethod]
        public void AddQueryHandler_HandlerDependsOnConcreteDispatcher_ThrowsQueryHandlerDependencyException()
        {
            // Given
            var services = new ServiceCollection();

            // When
            var exception = Assert.ThrowsException<QueryHandlerDependencyException>(() =>
            {
                services.AddQueryHandler<ConcreteDispatcherHandler, QueryStub, int>();
            });

            // Then
            Assert.IsTrue(exception.Violations.Contains((typeof(ConcreteDispatcherHandler), typeof(Dispatcher))));
        }

        [TestMethod]
        public void AddQueryHandler_HandlerDependsOnEventDispatcher_ThrowsQueryHandlerDependencyException()
        {
            // Given
            var services = new ServiceCollection();

            // When
            var exception = Assert.ThrowsException<QueryHandlerDependencyException>(() =>
            {
                services.AddQueryHandler<EventDispatcherHandler, QueryStub, int>();
            });

            // Then
            Assert.IsTrue(exception.Violations.Contains((typeof(EventDispatcherHandler), typeof(IEventDispatcher))));
        }

        [TestMethod]
        public void AddQueryHandler_HandlerDependsOnCommandHandler_ThrowsQueryHandlerDependencyException()
        {
            // Given
            var services = new ServiceCollection();

            // When
            var exception = Assert.ThrowsException<QueryHandlerDependencyException>(() =>
            {
                services.AddQueryHandler<CommandHandlerHandler, QueryStub, int>();
            });

            // Then
            Assert.IsTrue(exception.Violations.Contains((typeof(CommandHandlerHandler), typeof(ICommandHandler<CommandStub>))));
        }

        [TestMethod]
        public void AddQueryHandler_HandlerDependsOnWrappedDispatcher_ThrowsQueryHandlerDependencyException()
        {
            // Given
            var services = new ServiceCollection();

            // When
            var exception = Assert.ThrowsException<QueryHandlerDependencyException>(() =>
            {
                services.AddQueryHandler<LazyDispatcherHandler, QueryStub, int>();
            });

            // Then
            Assert.IsTrue(exception.Violations.Contains((typeof(LazyDispatcherHandler), typeof(Lazy<IDispatcher>))));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_QueryHandlerDependsOnDispatcher_ThrowsQueryHandlerDependencyException()
        {
            // Given: registered directly, bypassing the check in AddQueryHandler
            var services = new ServiceCollection();
            services.AddThemisquo();
            services.AddScoped<IQueryHandler<QueryStub, int>, DispatcherHandler>();
            var provider = services.BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<QueryHandlerDependencyException>(() =>
            {
                provider.ValidateHandlersRegistered([typeof(QueryStub)]);
            });

            // Then
            Assert.IsTrue(exception.Violations.Contains((typeof(DispatcherHandler), typeof(IDispatcher))));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_QueryHandlerDependsOnQueryDispatcher_ReturnsProvider()
        {
            // Given
            var services = new ServiceCollection();
            services.AddThemisquo();
            services.AddScoped<IQueryHandler<QueryStub, int>, QueryDispatcherHandler>();
            var provider = services.BuildServiceProvider();

            // When
            var result = provider.ValidateHandlersRegistered([typeof(QueryStub)]);

            // Then
            Assert.AreSame(provider, result);
        }

        [TestMethod]
        public void AddThemisquo_ResolvedQueryDispatcher_CannotBeCastToDispatcher()
        {
            // Given
            var services = new ServiceCollection();
            services.AddThemisquo();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            // When
            var queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

            // Then
            Assert.IsNotInstanceOfType(queryDispatcher, typeof(IDispatcher));
        }

        [TestMethod]
        public async Task QueryOnlyDispatcher_Dispatch_DelegatesToInnerDispatcher()
        {
            // Given
            var inner = Substitute.For<IQueryDispatcher>();
            var query = new QueryStub();
            inner.Dispatch(query, Arg.Any<CancellationToken>()).Returns(42);
            var dispatcher = new QueryOnlyDispatcher(inner);

            // When
            var result = await dispatcher.Dispatch(query, CancellationToken.None);

            // Then
            Assert.AreEqual(42, result);
        }

        // Dummy classes for test
        public class QueryStub : IQuery<int> { }

        public class CommandStub : ICommand
        {
            public Guid Instance => throw new NotImplementedException();
        }

        public abstract class QueryStubHandlerBase : IQueryHandler<QueryStub, int>
        {
            public Task<int> Handle(QueryStub query, CancellationToken cancellationToken) => throw new NotImplementedException();
        }

        public class QueryDispatcherHandler(IQueryDispatcher dispatcher) : QueryStubHandlerBase
        {
            public IQueryDispatcher Dispatcher { get; } = dispatcher;
        }

        public class DispatcherHandler(IDispatcher dispatcher) : QueryStubHandlerBase
        {
            public IDispatcher Dispatcher { get; } = dispatcher;
        }

        public class ConcreteDispatcherHandler(Dispatcher dispatcher) : QueryStubHandlerBase
        {
            public Dispatcher Dispatcher { get; } = dispatcher;
        }

        public class EventDispatcherHandler(IEventDispatcher dispatcher) : QueryStubHandlerBase
        {
            public IEventDispatcher Dispatcher { get; } = dispatcher;
        }

        public class CommandHandlerHandler(ICommandHandler<CommandStub> handler) : QueryStubHandlerBase
        {
            public ICommandHandler<CommandStub> Handler { get; } = handler;
        }

        public class LazyDispatcherHandler(Lazy<IDispatcher> dispatcher) : QueryStubHandlerBase
        {
            public Lazy<IDispatcher> Dispatcher { get; } = dispatcher;
        }
    }
}
