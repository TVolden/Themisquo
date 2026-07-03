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
            await dispatcherMock.Received().Dispatch(Arg.Is<ICommand>(c => ((DeleteMyTypeCommand)c).Id == 123));
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
                ((UpdateMyTypeCommand)c).Id == 123 && ((UpdateMyTypeCommand)c).Name == "foo"));
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
            await dispatcherMock.Received().Dispatch(Arg.Is<ICommand>(c => ((UpdateMyTypeCommand)c).Id == 123));
        }

        [TestMethod]
        public async Task MapQuery_GetWithRouteId_BindsIdFromRouteValueViaAsParameters()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<string>>()).Returns("ok");
            await using var app = await StartAppAsync(dispatcherMock, endpoints =>
                endpoints.MapQuery<GetMyTypeQuery, string>("/mytypes/{id}"));
            using var client = app.GetTestClient();

            // When
            var response = await client.GetAsync("/mytypes/123");

            // Then
            response.EnsureSuccessStatusCode();
            await dispatcherMock.Received().Dispatch(Arg.Is<IQuery<string>>(q => ((GetMyTypeQuery)q).Id == 123));
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
    }
}
