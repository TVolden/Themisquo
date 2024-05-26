using NSubstitute;

namespace Themisquo.Test
{
    [TestClass]
    public class TestServiceProviderEventDispatcher
    {
        [TestMethod]
        public void Dispatch_SpecificEvent_CallsGetServiceWithSpecificObserver()
        {
            // Given
            var eventStub = new EventStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var sut = new ServiceProviderEventDispatcher(providerMock);
            providerMock.GetService(Arg.Any<Type>()).Returns(Substitute.For<IEventObserver<IEvent>>());

            // When
            sut.Dispatch(eventStub);

            // Then
            providerMock.Received().GetService(typeof(IEventObserver<EventStub>));
        }

        [TestMethod]
        public void Dispatch_KnownEvent_CallsInvokeOnObserverWithEvent()
        {
            // Given
            var eventStub = new EventStub();
            var providerMock = Substitute.For<IServiceProvider>();
            var eventObserverMock = Substitute.For<IEventObserver<IEvent>>();
            var sut = new ServiceProviderEventDispatcher(providerMock);
            providerMock.GetService(Arg.Any<Type>()).Returns(eventObserverMock);

            // When
            sut.Dispatch(eventStub);

            // Then
            eventObserverMock.Received().Invoke(eventStub);
        }

        public class EventStub : IEvent
        {
            public DateTime EventTime => throw new NotImplementedException();
            public int Version => throw new NotImplementedException();
            public Guid ProcessId => throw new NotImplementedException();
        }
    }
}
