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
            CollectionAssert.AreEqual(new[] { "iMyItem", "collection" }, Strings(root.GetProperty("class")));
            var entities = root.GetProperty("entities").EnumerateArray().ToArray();
            Assert.AreEqual(2, entities.Length);
            for (var i = 0; i < entities.Length; i++)
            {
                CollectionAssert.AreEqual(new[] { "item" }, Strings(entities[i].GetProperty("rel")));
                CollectionAssert.AreEqual(new[] { "iMyItem" }, Strings(entities[i].GetProperty("class")));
                Assert.AreEqual(i + 1, entities[i].GetProperty("properties").GetProperty("id").GetInt32());
                Assert.IsFalse(entities[i].TryGetProperty("links", out _));
            }
            Assert.AreEqual("/myitems", SelfHref(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListQueryHasResourceType_ClassUsesIt()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IGadget>>>(), Arg.Any<CancellationToken>())
                .Returns(new IGadget[] { new Gadget { Id = 1, Key = "a" } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
                endpoints.MapQuery<ListGadgetsQuery, IEnumerable<IGadget>>("/gadgets"));
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/gadgets");

            // Then
            CollectionAssert.AreEqual(new[] { "device", "collection" }, Strings(root.GetProperty("class")));
            var item = root.GetProperty("entities").EnumerateArray().Single();
            CollectionAssert.AreEqual(new[] { "device" }, Strings(item.GetProperty("class")));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_SingleQueryHasResourceType_ClassUsesIt()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IGadget>>(), Arg.Any<CancellationToken>())
                .Returns(new Gadget { Id = 1, Key = "a" });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
                endpoints.MapQuery<GetGadgetQuery, IGadget>("/gadgets/{gadgetKey}"));
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/gadgets/a");

            // Then
            CollectionAssert.AreEqual(new[] { "device" }, Strings(root.GetProperty("class")));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListQueryWithoutResource_UsesTypeFromSingleItemQuery()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IGadget>>>(), Arg.Any<CancellationToken>())
                .Returns(new IGadget[] { new Gadget { Id = 1, Key = "a" } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListUnnamedGadgetsQuery, IEnumerable<IGadget>>("/gadgets");
                endpoints.MapQuery<GetGadgetQuery, IGadget>("/gadgets/{gadgetKey}");
            });
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/gadgets");

            // Then
            CollectionAssert.AreEqual(new[] { "device", "collection" }, Strings(root.GetProperty("class")));
            var item = root.GetProperty("entities").EnumerateArray().Single();
            CollectionAssert.AreEqual(new[] { "device" }, Strings(item.GetProperty("class")));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_SingleItemQueryHasResourceId_LastPlaceholderResolvesToItInsteadOfId()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IGadget>>>(), Arg.Any<CancellationToken>())
                .Returns(new IGadget[] { new Gadget { Id = 1, Key = "a" } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListUnnamedGadgetsQuery, IEnumerable<IGadget>>("/gadgets");
                endpoints.MapQuery<GetGadgetQuery, IGadget>("/gadgets/{gadgetKey}");
            });
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/gadgets");

            // Then
            CollectionAssert.AreEqual(new[] { "/gadgets/a" }, ItemSelfHrefs(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListItemHasSingleQueryRoute_LastPlaceholderResolvesToItemId()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IMyItem>>>(), Arg.Any<CancellationToken>())
                .Returns(new IMyItem[] { new MyItem { Id = 1 }, new MyItem { Id = 2 } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListMyItemsQuery, IEnumerable<IMyItem>>("/myitems");
                endpoints.MapQuery<GetMyItemQuery, IMyItem>("/myitems/{myItemId}");
            });
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/myitems");

            // Then
            CollectionAssert.AreEqual(new[] { "/myitems/1", "/myitems/2" }, ItemSelfHrefs(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListItemRouteHasConstraint_ResolvesPlaceholder()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IMyItem>>>(), Arg.Any<CancellationToken>())
                .Returns(new IMyItem[] { new MyItem { Id = 1 } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListMyItemsQuery, IEnumerable<IMyItem>>("/myitems");
                endpoints.MapQuery<GetMyItemQuery, IMyItem>("/myitems/{myItemId:int}");
            });
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/myitems");

            // Then
            CollectionAssert.AreEqual(new[] { "/myitems/1" }, ItemSelfHrefs(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListItemRoutePlaceholderNotOnItem_ResolvesFromRequestRouteValue()
        {
            // Given
            var projectId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<ICard>>>(), Arg.Any<CancellationToken>())
                .Returns(new ICard[] { new Card { Id = 7 } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListProjectCardsQuery, IEnumerable<ICard>>("/projects/{projectId}/cards");
                endpoints.MapQuery<GetProjectCardQuery, ICard>("/projects/{projectId}/cards/{cardId}");
            });
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, $"/projects/{projectId}/cards");

            // Then
            CollectionAssert.AreEqual(new[] { $"/projects/{projectId}/cards/7" }, ItemSelfHrefs(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListItemRoutePlaceholderMatchesItemProperty_UsesItemValue()
        {
            // Given
            var projectId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IAssignment>>>(), Arg.Any<CancellationToken>())
                .Returns(new IAssignment[] { new Assignment { Id = 3, ProjectId = projectId } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListAssignmentsQuery, IEnumerable<IAssignment>>("/assignments");
                endpoints.MapQuery<GetAssignmentQuery, IAssignment>("/projects/{projectId}/assignments/{assignmentId}");
            });
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/assignments");

            // Then
            CollectionAssert.AreEqual(new[] { $"/projects/{projectId}/assignments/3" }, ItemSelfHrefs(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListItemRoutePlaceholderUnresolvable_ItemHasNoLinks()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<ICard>>>(), Arg.Any<CancellationToken>())
                .Returns(new ICard[] { new Card { Id = 7 } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListCardsQuery, IEnumerable<ICard>>("/cards");
                endpoints.MapQuery<GetProjectCardQuery, ICard>("/projects/{projectId}/cards/{cardId}");
            });
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/cards");

            // Then
            var item = root.GetProperty("entities").EnumerateArray().Single();
            Assert.IsFalse(item.TryGetProperty("links", out _));
        }

        [TestMethod]
        public async Task MapCQEndpoints_SirenEnabled_ListItemHasEndpointAttributeRoute_SelfLinkIncludesBaseUrl()
        {
            // Given
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<IWidget>>>(), Arg.Any<CancellationToken>())
                .Returns(new IWidget[] { new Widget { Id = 5 } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
                endpoints.MapCQEndpoints(typeof(ListWidgetsQuery).Assembly, baseUrl: "/api"));
            using var client = app.GetTestClient();

            // When
            var root = await GetSirenAsync(client, "/api/widgets");

            // Then
            CollectionAssert.AreEqual(new[] { "/api/widgets/5" }, ItemSelfHrefs(root));
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

        private static string? SelfHref(JsonElement entity) =>
            entity.GetProperty("links").EnumerateArray()
                .Single(l => Strings(l.GetProperty("rel")).Contains("self"))
                .GetProperty("href").GetString();

        private static string?[] ItemSelfHrefs(JsonElement root) =>
            root.GetProperty("entities").EnumerateArray().Select(SelfHref).ToArray();

        private static async Task<JsonElement> GetSirenAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }

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

        public class GetMyItemQuery : IQuery<IMyItem>
        {
            public int MyItemId { get; set; }
        }

        public interface ICard
        {
            int Id { get; }
        }

        public class Card : ICard
        {
            public int Id { get; set; }
        }

        public class ListCardsQuery : IQuery<IEnumerable<ICard>>
        {
        }

        public class ListProjectCardsQuery : IQuery<IEnumerable<ICard>>
        {
            public Guid ProjectId { get; set; }
        }

        public class GetProjectCardQuery : IQuery<ICard>
        {
            public Guid ProjectId { get; set; }
            public int CardId { get; set; }
        }

        public interface IAssignment
        {
            int Id { get; }
            Guid ProjectId { get; }
        }

        public class Assignment : IAssignment
        {
            public int Id { get; set; }
            public Guid ProjectId { get; set; }
        }

        public class ListAssignmentsQuery : IQuery<IEnumerable<IAssignment>>
        {
        }

        public class GetAssignmentQuery : IQuery<IAssignment>
        {
            public Guid ProjectId { get; set; }
            public int AssignmentId { get; set; }
        }

        public interface IGadget
        {
            int Id { get; }
            string Key { get; }
        }

        public class Gadget : IGadget
        {
            public int Id { get; set; }
            public string Key { get; set; } = "";
        }

        [Resource(Type = "device")]
        public class ListGadgetsQuery : IQuery<IEnumerable<IGadget>>
        {
        }

        public class ListUnnamedGadgetsQuery : IQuery<IEnumerable<IGadget>>
        {
        }

        [Resource(Type = "device", Id = nameof(IGadget.Key))]
        public class GetGadgetQuery : IQuery<IGadget>
        {
            public string GadgetKey { get; set; } = "";
        }

        public interface IWidget
        {
            int Id { get; }
        }

        public class Widget : IWidget
        {
            public int Id { get; set; }
        }

        [Endpoint("/widgets")]
        public class ListWidgetsQuery : IQuery<IEnumerable<IWidget>>
        {
        }

        [Endpoint("/widgets/{widgetId}")]
        public class GetWidgetQuery : IQuery<IWidget>
        {
            public int WidgetId { get; set; }
        }
    }
}
