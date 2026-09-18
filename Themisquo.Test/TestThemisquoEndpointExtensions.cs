using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net.Http.Json;
using Themisquo.AspNetCore;

namespace Themisquo.Test
{
    [TestClass]
    public class TestThemisquoEndpointExtensions
    {
        [TestMethod]
        public async Task MapCommand_DeleteWithNoBody_BindsIdFromRouteValue()
        {
            // Given
            var dispatcherMock = Substitute.For<IDispatcher>();
            await using var app = await StartAppAsync(dispatcherMock, endpoints =>
                endpoints.MapCommand<DeleteMyTypeCommand>("/mytypes/{id}", "DELETE"));
            using var client = app.GetTestClient();

            // When
            var response = await client.DeleteAsync("/mytypes/123");

            // Then
            response.EnsureSuccessStatusCode();
            await dispatcherMock.Received().Dispatch(Arg.Is<ICommand>(c => ((DeleteMyTypeCommand)c).Id == 123), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public async Task MapCommand_PutWithRouteIdAndBody_BindsIdFromRouteAndRestFromBody()
        {
            // Given
            var dispatcherMock = Substitute.For<IDispatcher>();
            await using var app = await StartAppAsync(dispatcherMock, endpoints =>
                endpoints.MapCommand<UpdateMyTypeCommand>("/mytypes/{id}", "PUT"));
            using var client = app.GetTestClient();

            // When
            var response = await client.PutAsJsonAsync("/mytypes/123", new { Name = "foo" });

            // Then
            response.EnsureSuccessStatusCode();
            await dispatcherMock.Received().Dispatch(Arg.Is<ICommand>(c =>
                ((UpdateMyTypeCommand)c).Id == 123 && ((UpdateMyTypeCommand)c).Name == "foo"), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public async Task MapCommand_BodyIdConflictsWithRouteId_RouteValueWins()
        {
            // Given
            var dispatcherMock = Substitute.For<IDispatcher>();
            await using var app = await StartAppAsync(dispatcherMock, endpoints =>
                endpoints.MapCommand<UpdateMyTypeCommand>("/mytypes/{id}", "PUT"));
            using var client = app.GetTestClient();

            // When
            var response = await client.PutAsJsonAsync("/mytypes/123", new { Id = 999, Name = "foo" });

            // Then
            response.EnsureSuccessStatusCode();
            await dispatcherMock.Received().Dispatch(Arg.Is<ICommand>(c => ((UpdateMyTypeCommand)c).Id == 123), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public async Task MapQuery_GetWithRouteId_BindsIdFromRouteValueViaAsParameters()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<string>>(), Arg.Any<CancellationToken>()).Returns("ok");
            await using var app = await StartAppAsync(dispatcherMock, endpoints =>
                endpoints.MapQuery<GetMyTypeQuery, string>("/mytypes/{id}"));
            using var client = app.GetTestClient();

            // When
            var response = await client.GetAsync("/mytypes/123");

            // Then
            response.EnsureSuccessStatusCode();
            await dispatcherMock.Received().Dispatch(Arg.Is<IQuery<string>>(q => ((GetMyTypeQuery)q).Id == 123), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public async Task MapCommand_HandlerRaisesEventWithLocationAttribute_Returns201WithLocationHeader()
        {
            // Given
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddThemisquo();
            builder.Services.AddThemisquoCommandLocations();
            builder.Services.AddScoped<ICommandHandler<CreateCardCommand>, CreateCardCommandHandler>();
            builder.Services.AddScoped<IEventObserver<CardCreatedEvent>, NoopCardCreatedObserver>();

            await using var app = builder.Build();
            app.MapCommand<CreateCardCommand>("/projects/{projectId}/cards");
            await app.StartAsync();
            using var client = app.GetTestClient();

            var projectId = Guid.NewGuid();
            var cardId = Guid.NewGuid();

            // When
            var response = await client.PostAsJsonAsync($"/projects/{projectId}/cards", new { CardId = cardId });

            // Then
            Assert.AreEqual(System.Net.HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual($"/projects/{projectId}/cards/{cardId}", response.Headers.Location?.ToString());
        }

        [TestMethod]
        public async Task MapCommand_LocationsEnabledButNoLocatedEventRaised_Returns200Ok()
        {
            // Given
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddThemisquo();
            builder.Services.AddThemisquoCommandLocations();
            builder.Services.AddScoped<ICommandHandler<UpdateMyTypeCommand>, NoopUpdateMyTypeCommandHandler>();

            await using var app = builder.Build();
            app.MapCommand<UpdateMyTypeCommand>("/mytypes/{id}", "PUT");
            await app.StartAsync();
            using var client = app.GetTestClient();

            // When
            var response = await client.PutAsJsonAsync("/mytypes/123", new { Name = "foo" });

            // Then
            response.EnsureSuccessStatusCode();
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.IsNull(response.Headers.Location);
        }

        private static async Task<WebApplication> StartAppAsync<TService>(TService service, Action<WebApplication> mapEndpoints)
            where TService : class
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(service);

            var app = builder.Build();
            mapEndpoints(app);
            await app.StartAsync();
            return app;
        }

        public class DeleteMyTypeCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public int Id { get; set; }
        }

        public class UpdateMyTypeCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public class GetMyTypeQuery : IQuery<string>
        {
            public int Id { get; set; }
        }

        public class NoopUpdateMyTypeCommandHandler : ICommandHandler<UpdateMyTypeCommand>
        {
            public Task Handle(UpdateMyTypeCommand command, IEventDispatcher eventDispatcher, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        public class CreateCardCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public Guid ProjectId { get; set; }
            public Guid CardId { get; set; }
        }

        [Location("/projects/{ProjectId}/cards/{CardId}")]
        public class CardCreatedEvent : IEvent
        {
            public int Version => 1;
            public DateTime EventTime => DateTime.UtcNow;
            public Guid ProcessId => Guid.NewGuid();
            public required Guid ProjectId { get; init; }
            public required Guid CardId { get; init; }
        }

        public class CreateCardCommandHandler : ICommandHandler<CreateCardCommand>
        {
            public Task Handle(CreateCardCommand command, IEventDispatcher eventDispatcher, CancellationToken cancellationToken) =>
                eventDispatcher.Dispatch(new CardCreatedEvent { ProjectId = command.ProjectId, CardId = command.CardId }, cancellationToken);
        }

        public class NoopCardCreatedObserver : IEventObserver<CardCreatedEvent>
        {
            public Task Invoke(CardCreatedEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
