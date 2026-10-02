using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
            using var client = SirenClient(app);

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
        public async Task MapQuery_SirenEnabled_SingleResultHasPropertyMatchingRelatedRoute_AddsRelatedLinkButNotToItself()
        {
            // Given
            var noteId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<INote>>(), Arg.Any<CancellationToken>())
                .Returns(new Note { NoteId = noteId, ProjectId = projectId });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                endpoints.MapQuery<GetProjectQuery, IProject>("/projects/{projectId}");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, $"/notes/{noteId}");

            // Then
            CollectionAssert.AreEqual(new[] { "self", "project" }, LinkRels(root));
            Assert.AreEqual($"/projects/{projectId}", LinkHref(root, "project"));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListItemHasPropertyMatchingRelatedRoute_ItemGetsRelatedLink()
        {
            // Given
            var projectId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<INote>>>(), Arg.Any<CancellationToken>())
                .Returns(new INote[] { new Note { NoteId = Guid.NewGuid(), ProjectId = projectId } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListNotesQuery, IEnumerable<INote>>("/notes");
                endpoints.MapQuery<GetProjectQuery, IProject>("/projects/{projectId}");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, "/notes");

            // Then
            CollectionAssert.AreEqual(new[] { "self" }, LinkRels(root));
            var item = root.GetProperty("entities").EnumerateArray().Single();
            Assert.AreEqual($"/projects/{projectId}", LinkHref(item, "project"));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_RelatedRouteHasOtherPlaceholder_ResolvesItFromRequestRouteValue()
        {
            // Given
            var noteId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<INote>>(), Arg.Any<CancellationToken>())
                .Returns(new Note { NoteId = noteId, ProjectId = projectId });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<GetNoteQuery, INote>("/orgs/{orgId}/notes/{noteId}");
                endpoints.MapQuery<GetOrgProjectQuery, IProject>("/orgs/{orgId}/projects/{projectId}");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, $"/orgs/acme/notes/{noteId}");

            // Then
            Assert.AreEqual($"/orgs/acme/projects/{projectId}", LinkHref(root, "project"));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_RelatedRouteCannotBeResolved_NoRelatedLink()
        {
            // Given
            var noteId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<INote>>(), Arg.Any<CancellationToken>())
                .Returns(new Note { NoteId = noteId, ProjectId = Guid.NewGuid() });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                endpoints.MapQuery<GetOrgProjectQuery, IProject>("/orgs/{orgId}/projects/{projectId}");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, $"/notes/{noteId}");

            // Then
            CollectionAssert.AreEqual(new[] { "self" }, LinkRels(root));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_SingleResultRouteHasCommand_AddsActionWithNonRouteFields()
        {
            // Given
            var noteId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<INote>>(), Arg.Any<CancellationToken>())
                .Returns(new Note { NoteId = noteId });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                endpoints.MapCommand<UpdateNoteCommand>("/notes/{noteId}", "PUT");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, $"/notes/{noteId}?expand=all");

            // Then
            var action = root.GetProperty("actions").EnumerateArray().Single();
            Assert.AreEqual("updateNoteCommand", action.GetProperty("name").GetString());
            Assert.AreEqual("PUT", action.GetProperty("method").GetString());
            Assert.AreEqual($"/notes/{noteId}", action.GetProperty("href").GetString());
            Assert.AreEqual("application/json", action.GetProperty("type").GetString());
            Assert.IsFalse(action.TryGetProperty("title", out _));
            var field = action.GetProperty("fields").EnumerateArray().Single();
            Assert.AreEqual("text", field.GetProperty("name").GetString());
            Assert.AreEqual("text", field.GetProperty("type").GetString());
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_CommandHasActionAttribute_ActionUsesItsNameAndTitle()
        {
            // Given
            var noteId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<INote>>(), Arg.Any<CancellationToken>())
                .Returns(new Note { NoteId = noteId });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                endpoints.MapCommand<RenameNoteCommand>("/notes/{noteId}", "PATCH");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, $"/notes/{noteId}");

            // Then
            var action = root.GetProperty("actions").EnumerateArray().Single();
            Assert.AreEqual("rename", action.GetProperty("name").GetString());
            Assert.AreEqual("Rename note", action.GetProperty("title").GetString());
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_CommandRouteHasDifferentPlaceholderNameAndNoBody_AddsActionWithoutFields()
        {
            // Given
            var noteId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<INote>>(), Arg.Any<CancellationToken>())
                .Returns(new Note { NoteId = noteId });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                endpoints.MapCommand<DeleteNoteCommand>("/notes/{id}", "DELETE");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, $"/notes/{noteId}");

            // Then
            var action = root.GetProperty("actions").EnumerateArray().Single();
            Assert.AreEqual("deleteNoteCommand", action.GetProperty("name").GetString());
            Assert.AreEqual("DELETE", action.GetProperty("method").GetString());
            Assert.AreEqual($"/notes/{noteId}", action.GetProperty("href").GetString());
            Assert.IsFalse(action.TryGetProperty("type", out _));
            Assert.IsFalse(action.TryGetProperty("fields", out _));
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListRouteHasCommand_CollectionGetsActionWithTypedFields()
        {
            // Given
            var projectId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<INote>>>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<INote>());
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListProjectNotesQuery, IEnumerable<INote>>("/projects/{projectId}/notes");
                endpoints.MapCommand<CreateNoteCommand>("/projects/{projectId}/notes");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, $"/projects/{projectId}/notes");

            // Then
            var action = root.GetProperty("actions").EnumerateArray().Single();
            Assert.AreEqual("createNoteCommand", action.GetProperty("name").GetString());
            Assert.AreEqual("POST", action.GetProperty("method").GetString());
            Assert.AreEqual($"/projects/{projectId}/notes", action.GetProperty("href").GetString());
            var fields = action.GetProperty("fields").EnumerateArray()
                .ToDictionary(f => f.GetProperty("name").GetString()!, f => f.GetProperty("type").GetString());
            CollectionAssert.AreEquivalent(new[] { "text", "priority", "pinned" }, fields.Keys.ToArray());
            Assert.AreEqual("text", fields["text"]);
            Assert.AreEqual("number", fields["priority"]);
            Assert.AreEqual("checkbox", fields["pinned"]);
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_ListItemRouteHasCommand_ItemGetsActionOnItsOwnPath()
        {
            // Given
            var noteId = Guid.NewGuid();
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<IEnumerable<INote>>>(), Arg.Any<CancellationToken>())
                .Returns(new INote[] { new Note { NoteId = noteId } });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
            {
                endpoints.MapQuery<ListNotesQuery, IEnumerable<INote>>("/notes");
                endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                endpoints.MapCommand<UpdateNoteCommand>("/notes/{noteId}", "PUT");
            });
            using var client = SirenClient(app);

            // When
            var root = await GetSirenAsync(client, "/notes");

            // Then
            Assert.IsFalse(root.TryGetProperty("actions", out _));
            var item = root.GetProperty("entities").EnumerateArray().Single();
            var action = item.GetProperty("actions").EnumerateArray().Single();
            Assert.AreEqual("updateNoteCommand", action.GetProperty("name").GetString());
            Assert.AreEqual($"/notes/{noteId}", action.GetProperty("href").GetString());
        }

        [TestMethod]
        public async Task MapCommand_SirenEnabled_LocationMatchesQueryRoute_Returns201WithCreatedSirenEntity()
        {
            // Given
            await using var app = await StartCommandAppAsync(
                services => services.AddQueryHandler<GetNoteQueryHandler, GetNoteQuery, INote>(),
                endpoints =>
                {
                    endpoints.MapCommand<AddNoteCommand>("/notes");
                    endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                    endpoints.MapCommand<UpdateNoteCommand>("/notes/{noteId}", "PUT");
                });
            using var client = SirenClient(app);
            var noteId = Guid.NewGuid();

            // When
            var response = await client.PostAsJsonAsync("/notes", new { NoteId = noteId });

            // Then
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual($"/notes/{noteId}", response.Headers.Location?.ToString());
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            CollectionAssert.AreEqual(new[] { "iNote" }, Strings(root.GetProperty("class")));
            Assert.AreEqual(noteId, root.GetProperty("properties").GetProperty("noteId").GetGuid());
            Assert.AreEqual($"/notes/{noteId}", SelfHref(root));
            var action = root.GetProperty("actions").EnumerateArray().Single();
            Assert.AreEqual("updateNoteCommand", action.GetProperty("name").GetString());
            Assert.AreEqual($"/notes/{noteId}", action.GetProperty("href").GetString());
        }

        [TestMethod]
        public async Task MapCommand_SirenEnabled_LocationMatchesNoQueryRoute_Returns201WithoutBody()
        {
            // Given
            await using var app = await StartCommandAppAsync(
                services => { },
                endpoints => endpoints.MapCommand<AddNoteCommand>("/notes"));
            using var client = SirenClient(app);
            var noteId = Guid.NewGuid();

            // When
            var response = await client.PostAsJsonAsync("/notes", new { NoteId = noteId });

            // Then
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual($"/notes/{noteId}", response.Headers.Location?.ToString());
            Assert.AreEqual("", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task MapCommand_SirenEnabled_LocationQueryFails_Returns201WithoutBody()
        {
            // Given
            await using var app = await StartCommandAppAsync(
                services => services.AddQueryHandler<FailingGetNoteQueryHandler, GetNoteQuery, INote>(),
                endpoints =>
                {
                    endpoints.MapCommand<AddNoteCommand>("/notes");
                    endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                });
            using var client = SirenClient(app);
            var noteId = Guid.NewGuid();

            // When
            var response = await client.PostAsJsonAsync("/notes", new { NoteId = noteId });

            // Then
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual($"/notes/{noteId}", response.Headers.Location?.ToString());
            Assert.AreEqual("", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task MapCommand_SirenEnabled_NoLocatedEvent_Returns200WithoutBody()
        {
            // Given
            await using var app = await StartCommandAppAsync(
                services => services.AddCommandHandler<NoopUpdateNoteCommandHandler, UpdateNoteCommand>(),
                endpoints => endpoints.MapCommand<UpdateNoteCommand>("/notes/{noteId}", "PUT"));
            using var client = SirenClient(app);

            // When
            var response = await client.PutAsJsonAsync($"/notes/{Guid.NewGuid()}", new { Text = "foo" });

            // Then
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_NoAcceptHeader_ReturnsPlainJsonVaryingOnAccept()
        {
            // Given
            var response = await GetMyTypeAsync(sirenAsDefault: false, accept: null);

            // Then
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
            CollectionAssert.Contains(response.Headers.Vary.ToArray(), "Accept");
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabledAsDefault_NoAcceptHeader_ReturnsSiren()
        {
            // Given
            var response = await GetMyTypeAsync(sirenAsDefault: true, accept: null);

            // Then
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabledAsDefault_AcceptsAnything_ReturnsSiren()
        {
            // Given
            var response = await GetMyTypeAsync(sirenAsDefault: true, accept: "*/*");

            // Then
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabledAsDefault_AcceptsApplicationJson_ReturnsPlainJson()
        {
            // Given
            var response = await GetMyTypeAsync(sirenAsDefault: true, accept: "application/json");

            // Then
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabled_AcceptPrefersJsonByQuality_ReturnsPlainJson()
        {
            // Given
            var response = await GetMyTypeAsync(sirenAsDefault: true, accept: "application/vnd.siren+json;q=0.5, application/json");

            // Then
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        }

        [TestMethod]
        public async Task MapQuery_SirenEnabledAsDefault_AcceptsUnsupportedType_FallsBackToSiren()
        {
            // Given
            var response = await GetMyTypeAsync(sirenAsDefault: true, accept: "application/xml");

            // Then
            Assert.AreEqual("application/vnd.siren+json", response.Content.Headers.ContentType?.MediaType);
        }

        [TestMethod]
        public async Task MapCommand_SirenEnabled_NoAcceptHeader_Returns201WithoutBody()
        {
            // Given
            await using var app = await StartCommandAppAsync(
                services => services.AddQueryHandler<GetNoteQueryHandler, GetNoteQuery, INote>(),
                endpoints =>
                {
                    endpoints.MapCommand<AddNoteCommand>("/notes");
                    endpoints.MapQuery<GetNoteQuery, INote>("/notes/{noteId}");
                });
            using var client = app.GetTestClient();
            var noteId = Guid.NewGuid();

            // When
            var response = await client.PostAsJsonAsync("/notes", new { NoteId = noteId });

            // Then
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual($"/notes/{noteId}", response.Headers.Location?.ToString());
            Assert.AreEqual("", await response.Content.ReadAsStringAsync());
        }

        private static async Task<HttpResponseMessage> GetMyTypeAsync(bool sirenAsDefault, string? accept)
        {
            var dispatcherMock = Substitute.For<IQueryDispatcher>();
            dispatcherMock.Dispatch(Arg.Any<IQuery<MyType>>(), Arg.Any<CancellationToken>())
                .Returns(new MyType { Id = 123, DisplayName = "foo" });
            await using var app = await StartAppAsync(dispatcherMock, sirenEnabled: true, endpoints =>
                endpoints.MapQuery<GetMyTypeQuery, MyType>("/mytypes/{id}"), sirenAsDefault);
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/mytypes/123");
            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation("Accept", accept);
            }

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await response.Content.LoadIntoBufferAsync();
            return response;
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
            using var client = SirenClient(app);

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

        private static string?[] LinkRels(JsonElement entity) =>
            entity.GetProperty("links").EnumerateArray().SelectMany(l => Strings(l.GetProperty("rel"))).ToArray();

        private static string? LinkHref(JsonElement entity, string rel) =>
            entity.GetProperty("links").EnumerateArray()
                .Single(l => Strings(l.GetProperty("rel")).Contains(rel))
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

        private static async Task<WebApplication> StartCommandAppAsync(Action<IServiceCollection> registerHandlers, Action<WebApplication> mapEndpoints)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddThemisquo();
            builder.Services.AddThemisquoCommandLocations();
            builder.Services.AddThemisquoSiren();
            builder.Services.AddCommandHandler<AddNoteCommandHandler, AddNoteCommand>();
            builder.Services.AddScoped<IEventObserver<NoteAddedEvent>, NoopNoteAddedObserver>();
            registerHandlers(builder.Services);

            var app = builder.Build();
            mapEndpoints(app);
            await app.StartAsync();
            return app;
        }

        private static HttpClient SirenClient(WebApplication app)
        {
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.siren+json");
            return client;
        }

        private static async Task<WebApplication> StartAppAsync(IQueryDispatcher dispatcher, bool sirenEnabled, Action<WebApplication> mapEndpoints, bool sirenAsDefault = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(dispatcher);
            builder.Services.AddSingleton(Substitute.For<IDispatcher>()); // for command endpoints
            if (sirenEnabled)
            {
                builder.Services.AddThemisquoSiren(asDefault: sirenAsDefault);
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

        public interface INote
        {
            Guid NoteId { get; }
            Guid ProjectId { get; }
        }

        public class Note : INote
        {
            public Guid NoteId { get; set; }
            public Guid ProjectId { get; set; }
        }

        public class GetNoteQuery : IQuery<INote>
        {
            public Guid NoteId { get; set; }
        }

        public class ListNotesQuery : IQuery<IEnumerable<INote>>
        {
        }

        public class ListProjectNotesQuery : IQuery<IEnumerable<INote>>
        {
            public Guid ProjectId { get; set; }
        }

        public class UpdateNoteCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public Guid NoteId { get; set; }
            public string Text { get; set; } = "";
        }

        public class AddNoteCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public Guid NoteId { get; set; }
        }

        [Location("/notes/{NoteId}")]
        public class NoteAddedEvent : IEvent
        {
            public int Version => 1;
            public DateTime EventTime => DateTime.UtcNow;
            public Guid ProcessId => Guid.NewGuid();
            public required Guid NoteId { get; init; }
        }

        public class AddNoteCommandHandler : ICommandHandler<AddNoteCommand>
        {
            public Task Handle(AddNoteCommand command, IEventDispatcher eventDispatcher, CancellationToken cancellationToken) =>
                eventDispatcher.Dispatch(new NoteAddedEvent { NoteId = command.NoteId }, cancellationToken);
        }

        public class NoopNoteAddedObserver : IEventObserver<NoteAddedEvent>
        {
            public Task Invoke(NoteAddedEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        public class NoopUpdateNoteCommandHandler : ICommandHandler<UpdateNoteCommand>
        {
            public Task Handle(UpdateNoteCommand command, IEventDispatcher eventDispatcher, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        public class GetNoteQueryHandler : IQueryHandler<GetNoteQuery, INote>
        {
            public Task<INote> Handle(GetNoteQuery query, CancellationToken cancellationToken) =>
                Task.FromResult<INote>(new Note { NoteId = query.NoteId });
        }

        public class FailingGetNoteQueryHandler : IQueryHandler<GetNoteQuery, INote>
        {
            public Task<INote> Handle(GetNoteQuery query, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("The read model hasn't caught up yet.");
        }

        [Action(Name = "rename", Title = "Rename note")]
        public class RenameNoteCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public Guid NoteId { get; set; }
            public string Title { get; set; } = "";
        }

        public class DeleteNoteCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public Guid Id { get; set; }
        }

        public class CreateNoteCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public Guid ProjectId { get; set; }
            public string Text { get; set; } = "";
            public int Priority { get; set; }
            public bool Pinned { get; set; }
        }

        public interface IProject
        {
            Guid ProjectId { get; }
        }

        [Resource(Type = "project")]
        public class GetProjectQuery : IQuery<IProject>
        {
            public Guid ProjectId { get; set; }
        }

        [Resource(Type = "project")]
        public class GetOrgProjectQuery : IQuery<IProject>
        {
            public string OrgId { get; set; } = "";
            public Guid ProjectId { get; set; }
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
