using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Themisquo.AspNetCore;

namespace Themisquo.Test
{
    [TestClass]
    public class TestThemisquoLocationValidationExtensions
    {
        [TestMethod]
        public void ValidateLocationTemplatesRegistered_AllPlaceholdersResolve_ReturnsProvider()
        {
            // Given
            var provider = new ServiceCollection().BuildServiceProvider();

            // When
            var result = provider.ValidateLocationTemplatesRegistered([typeof(ValidLocatedEvent)]);

            // Then
            Assert.AreSame(provider, result);
        }

        [TestMethod]
        public void ValidateLocationTemplatesRegistered_PlaceholderHasNoMatchingProperty_ThrowsInvalidLocationTemplateException()
        {
            // Given
            var provider = new ServiceCollection().BuildServiceProvider();

            // When
            var exception = Assert.ThrowsException<InvalidLocationTemplateException>(() =>
            {
                provider.ValidateLocationTemplatesRegistered([typeof(InvalidLocatedEvent)]);
            });

            // Then
            var invalid = exception.InvalidTemplates.Single(t => t.EventType == typeof(InvalidLocatedEvent));
            Assert.IsTrue(invalid.UnresolvedPlaceholders.Contains("CardId"));
        }

        [TestMethod]
        public void ValidateLocationTemplatesRegistered_EventWithoutLocationAttribute_IsIgnored()
        {
            // Given
            var provider = new ServiceCollection().BuildServiceProvider();

            // When
            var result = provider.ValidateLocationTemplatesRegistered([typeof(UnlocatedEvent)]);

            // Then
            Assert.AreSame(provider, result);
        }

        [TestMethod]
        public void ValidateLocationTemplatesRegistered_NonEventType_IsIgnored()
        {
            // Given
            var provider = new ServiceCollection().BuildServiceProvider();

            // When
            var result = provider.ValidateLocationTemplatesRegistered([typeof(string)]);

            // Then
            Assert.AreSame(provider, result);
        }

        // Dummy classes for test
        [Location("/projects/{ProjectId}")]
        public class ValidLocatedEvent : IEvent
        {
            public int Version => throw new NotImplementedException();
            public DateTime EventTime => throw new NotImplementedException();
            public Guid ProcessId => throw new NotImplementedException();
            public Guid ProjectId => throw new NotImplementedException();
        }

        [Location("/projects/{ProjectId}/cards/{CardId}")]
        public class InvalidLocatedEvent : IEvent
        {
            public int Version => throw new NotImplementedException();
            public DateTime EventTime => throw new NotImplementedException();
            public Guid ProcessId => throw new NotImplementedException();
            public Guid ProjectId => throw new NotImplementedException();
        }

        public class UnlocatedEvent : IEvent
        {
            public int Version => throw new NotImplementedException();
            public DateTime EventTime => throw new NotImplementedException();
            public Guid ProcessId => throw new NotImplementedException();
        }
    }
}
