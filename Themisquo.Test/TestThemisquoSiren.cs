using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Text.Json;
using Themisquo.AspNetCore;
using Themisquo.AspNetCore.Siren;

namespace Themisquo.Test
{
    [TestClass]
    public class TestThemisquoSiren
    {
        [TestMethod]
        public async Task MapQuery_SirenEnabled_SingleResult_ReturnsSirenEntityWithPropertiesAndSelfLink()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<MyType>>(), Arg.Any<CancellationToken>())
                .Returns(new MyType { Id = 123, DisplayName = "foo" });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
                endpoints.MapQuery<GetMyTypeQuery, MyType>("/mytypes/{id}"));
            using var client = app.GetTestClient();

            // When
            var response = await client.GetAsync("/mytypes/123?expand=all");

            // Then
            response.EnsureSuccessStatusCode();
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            CollectionAssert.AreEqual(new[] { "myType" }, Strings(root.GetProperty("class")));
            var properties = root.GetProperty("properties");
            Assert.AreEqual(123, properties.GetProperty("id").GetInt32());
            Assert.AreEqual("foo", properties.GetProperty("displayName").GetString());
            Assert.AreEqual("/mytypes/123?expand=all", SelfHref(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListResult_ReturnsCollectionWithItemSubEntities()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IMyItem>>>(), Arg.Any<CancellationToken>())
                .Returns(new IMyItem[] { new MyItem { Id = 1 }, new MyItem { Id = 2 } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
                endpoints.MapQuery<ListMyItemsQuery, IEnumerable<IMyItem>>("/myitems"));
            using var client = app.GetTestClient();

            // When
            var response = await client.GetAsync("/myitems");

            // Then
            response.EnsureSuccessStatusCode();
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            CollectionAssert.AreEqual(new[] { "myItem", "collection" }, Strings(root.GetProperty("class")));
            var entities = root.GetProperty("entities").EnumerateArray().ToArray();
            Assert.AreEqual(2, entities.Length);
            for (var i = 0; i < entities.Length; i++)
            {
                CollectionAssert.AreEqual(new[] { "item" }, Strings(entities[i].GetProperty("rel")));
                CollectionAssert.AreEqual(new[] { "myItem" }, Strings(entities[i].GetProperty("class")));
                Assert.AreEqual(i + 1, entities[i].GetProperty("properties").GetProperty("id").GetInt32());
            }
            Assert.AreEqual("/myitems", SelfHref(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ScalarResult_WrapsValueInProperties()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<string>>(), Arg.Any<CancellationToken>()).Returns("ok");
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
                endpoints.MapQuery<GetMyNameQuery, string>("/mynames/{id}"));
            using var client = app.GetTestClient();

            // When
            var response = await client.GetAsync("/mynames/123");

            // Then
            response.EnsureSuccessStatusCode();
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.AreEqual("ok", root.GetProperty("properties").GetProperty("value").GetString());
            Assert.AreEqual("/mynames/123", SelfHref(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenNotEnabled_ReturnsPlainJson()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<MyType>>(), Arg.Any<CancellationToken>())
                .Returns(new MyType { Id = 123, DisplayName = "foo" });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: false, endpoints =>
                endpoints.MapQuery<GetMyTypeQuery, MyType>("/mytypes/{id}"));
            using var client = app.GetTestClient();

            // When
            var response = await client.GetAsync("/mytypes/123");

            // Then
            response.EnsureSuccessStatusCode();
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.AreEqual(123, root.GetProperty("id").GetInt32());
            Assert.AreEqual("foo", root.GetProperty("displayName").GetString());
            Assert.IsFalse(root.TryGetProperty("properties", out _));
        }

        private static string?[] Strings(JsonElement array) =>
            array.EnumerateArray().Select(e => e.GetString()).ToArray();

        private static string? SelfHref(JsonElement root) =>
            root.GetProperty("links").EnumerateArray()
                .Single(l => Strings(l.GetProperty("rel")).Contains("self"))
                .GetProperty("href").GetString();

        private static async Task<WebApplication> StartAppAsync(IQueryDispatcher dispatcher, bool sirenEnabled, Action<WebApplication> mapEndpoints)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(dispatcher);
            if (sirenEnabled)
            {
                builder.Services.AddThemisquoSiren();
            }

            var app = builder.Build();
            mapEndpoints(app);
            await app.StartAsync();
            return app;
        }

        public class MyType
        {
            public int Id { get; set; }
            public string DisplayName { get; set; } = "";
        }

        public interface IMyItem
        {
            int Id { get; }
        }

        public class MyItem : IMyItem
        {
            public int Id { get; set; }
        }

        public class GetMyTypeQuery : IQuery<MyType>
        {
            public int Id { get; set; }
        }

        public class ListMyItemsQuery : IQuery<IEnumerable<IMyItem>>
        {
        }

        public class GetMyNameQuery : IQuery<string>
        {
            public int Id { get; set; }
        }
    }
}
