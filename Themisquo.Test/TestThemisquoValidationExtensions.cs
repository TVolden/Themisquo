using Microsoft.Extensions.DependencyInjection;

namespace Themisquo.Test
{
    [TestClass]
    public class TestThemisquoValidationExtensions
    {
        private static readonly Type[] StubTypes = [typeof(CommandStub), typeof(QueryStub), typeof(EventStub)];

        [TestMethod]
        public void ValidateHandlersRegistered_AllHandlersRegistered_ReturnsProvider()
        {
            // Given
            var services = new ServiceCollection();
            services.AddScoped<ICommandHandler<CommandStub>, CommandStubHandler>();
            services.AddScoped<IQueryHandler<QueryStub, int>, QueryStubHandler>();
            services.AddScoped<IEventObserver<EventStub>, EventStubObserver>();
            var provider = services.BuildServiceProvider();

            // When
            var result = provider.ValidateHandlersRegistered(StubTypes);

            // Then
            Assert.AreSame(provider, result);
        }

        [TestMethod]
        public void ValidateHandlersRegistered_MissingCommandHandler_ThrowsMissingHandlersException()
        {
            // Given
            var services = new ServiceCollection();
            services.AddScoped<IQueryHandler<QueryStub, int>, QueryStubHandler>();
            services.AddScoped<IEventObserver<EventStub>, EventStubObserver>();
            var provider = services.BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<MissingHandlersException>(() =>
            {
                provider.ValidateHandlersRegistered(StubTypes);
            });

            // Then
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(ICommandHandler<CommandStub>)));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_MissingQueryHandler_ThrowsMissingHandlersException()
        {
            // Given
            var services = new ServiceCollection();
            services.AddScoped<ICommandHandler<CommandStub>, CommandStubHandler>();
            services.AddScoped<IEventObserver<EventStub>, EventStubObserver>();
            var provider = services.BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<MissingHandlersException>(() =>
            {
                provider.ValidateHandlersRegistered(StubTypes);
            });

            // Then
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IQueryHandler<QueryStub, int>)));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_MissingEventObserver_ThrowsMissingHandlersException()
        {
            // Given
            var services = new ServiceCollection();
            services.AddScoped<ICommandHandler<CommandStub>, CommandStubHandler>();
            services.AddScoped<IQueryHandler<QueryStub, int>, QueryStubHandler>();
            var provider = services.BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<MissingHandlersException>(() =>
            {
                provider.ValidateHandlersRegistered(StubTypes);
            });

            // Then
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IEventObserver<EventStub>)));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_NoHandlersRegistered_ReportsAllMissingHandlers()
        {
            // Given
            var provider = new ServiceCollection().BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<MissingHandlersException>(() =>
            {
                provider.ValidateHandlersRegistered(StubTypes);
            });

            // Then
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(ICommandHandler<CommandStub>)));
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IQueryHandler<QueryStub, int>)));
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IEventObserver<EventStub>)));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_FromAssembly_ScansAllTypesInAssembly()
        {
            // Given
            var services = new ServiceCollection();
            services.AddScoped<ICommandHandler<CommandStub>, CommandStubHandler>();
            services.AddScoped<IQueryHandler<QueryStub, int>, QueryStubHandler>();
            services.AddScoped<IEventObserver<EventStub>, EventStubObserver>();
            var provider = services.BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<MissingHandlersException>(() =>
            {
                // Other stub types declared in this test assembly (e.g. TestDispatcher.CommandStub) have no
                // handlers registered, so scanning the whole assembly is expected to still find gaps.
                provider.ValidateHandlersRegistered(typeof(TestThemisquoValidationExtensions).Assembly);
            });

            // Then
            Assert.IsFalse(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(ICommandHandler<CommandStub>)));
            Assert.IsFalse(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IQueryHandler<QueryStub, int>)));
            Assert.IsFalse(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IEventObserver<EventStub>)));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_NoAssembliesGiven_DiscoversAssembliesReferencingThemisquo()
        {
            // Given: no handlers registered, and no assembly passed in. Themisquo.Test references Themisquo, so
            // it should be discovered automatically, the same way it would be if it were passed explicitly.
            var provider = new ServiceCollection().BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<MissingHandlersException>(() =>
            {
                provider.ValidateHandlersRegistered();
            });

            // Then
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(ICommandHandler<CommandStub>)));
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IQueryHandler<QueryStub, int>)));
            Assert.IsTrue(exception.MissingHandlers.Any(m => m.ExpectedType == typeof(IEventObserver<EventStub>)));
        }

        [TestMethod]
        public void ValidateHandlersRegistered_HandlerRegisteredAsOpenGeneric_DoesNotReportMissing()
        {
            // Given: a cross-cutting handler registered for every command via an open generic registration,
            // rather than one closed registration per command type. A collection-based check that only looks
            // for an exact ServiceType match would miss this, so the check must resolve through the provider.
            var services = new ServiceCollection();
            services.AddScoped(typeof(ICommandHandler<>), typeof(GenericCommandHandler<>));
            var provider = services.BuildServiceProvider();

            // When
            var result = provider.ValidateHandlersRegistered([typeof(CommandStub)]);

            // Then
            Assert.AreSame(provider, result);
        }

        // Dummy classes for test
        public class CommandStub : ICommand
        {
            public Guid Instance => throw new NotImplementedException();
        }

        public class CommandStubHandler : ICommandHandler<CommandStub>
        {
            public Task Handle(CommandStub command, IEventDispatcher eventDispatcher) => throw new NotImplementedException();
        }

        public class GenericCommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
        {
            public Task Handle(TCommand command, IEventDispatcher eventDispatcher) => throw new NotImplementedException();
        }

        public class QueryStub : IQuery<int> { }

        public class QueryStubHandler : IQueryHandler<QueryStub, int>
        {
            public Task<int> Handle(QueryStub query) => throw new NotImplementedException();
        }

        public class EventStub : IEvent
        {
            public DateTime EventTime => throw new NotImplementedException();
            public int Version => throw new NotImplementedException();
            public Guid ProcessId => throw new NotImplementedException();
        }

        public class EventStubObserver : IEventObserver<EventStub>
        {
            public Task Invoke(EventStub @event) => throw new NotImplementedException();
        }
    }
}
