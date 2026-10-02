using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Themisquo.AspNetCore;

namespace Themisquo.Test
{
    [TestClass]
    public class TestThemisquoResourceActionValidationExtensions
    {
        [TestMethod]
        public async Task ValidateResourceActions_AllDeclarationsValid_ReturnsEndpoints()
        {
            // Given
            await using var app = BuildApp(endpoints =>
            {
                endpoints.MapQuery<ListValidItemsQuery, IEnumerable<IItem>>("/folders/{folderId}/items");
                endpoints.MapQuery<GetValidItemQuery, IItem>("/items/{itemId}");
                endpoints.MapCommand<RemoveItemCommand>("/folders/{folderId}/items/{itemId}", "DELETE");
                endpoints.MapCommand<ArchiveItemCommand>("/archive/{itemId}");
            });

            // When
            var result = app.ValidateResourceActions();

            // Then
            Assert.AreSame(app, result);
        }

        [TestMethod]
        public async Task ValidateResourceActions_DeclaredCommandNotMapped_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
                endpoints.MapQuery<ListValidItemsQuery, IEnumerable<IItem>>("/folders/{folderId}/items"));

            // When
            var exception = Assert.ThrowsException<InvalidResourceActionException>(() => app.ValidateResourceActions());

            // Then
            Assert.IsTrue(exception.Errors.Any(e => e.Contains(nameof(RemoveItemCommand)) && e.Contains("isn't mapped")));
        }

        [TestMethod]
        public async Task ValidateResourceActions_ContextMatchesNoMappedCommand_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
                endpoints.MapQuery<GetValidItemQuery, IItem>("/items/{itemId}"));

            // When
            var exception = Assert.ThrowsException<InvalidResourceActionException>(() => app.ValidateResourceActions());

            // Then
            Assert.IsTrue(exception.Errors.Single().Contains("Context 'archivable'"));
        }

        [TestMethod]
        public async Task ValidateResourceActions_ItemDeclarationsOnSingleQuery_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
            {
                endpoints.MapQuery<GetItemWithItemDeclarationsQuery, IItem>("/items/{itemId}");
                endpoints.MapCommand<ArchiveItemCommand>("/archive/{itemId}");
            });

            // When
            var exception = Assert.ThrowsException<InvalidResourceActionException>(() => app.ValidateResourceActions());

            // Then
            Assert.AreEqual(3, exception.Errors.Count);
            Assert.IsTrue(exception.Errors.Any(e => e.Contains("item actions")));
            Assert.IsTrue(exception.Errors.Any(e => e.Contains("ItemContext")));
            Assert.IsTrue(exception.Errors.Any(e => e.Contains("AutoItemActions")));
        }

        [TestMethod]
        public async Task ValidateResourceActions_ExternalActionHasInvalidMethod_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
                endpoints.MapQuery<GetItemWithInvalidMethodQuery, IItem>("/items/{itemId}"));

            // When
            var exception = Assert.ThrowsException<InvalidResourceActionException>(() => app.ValidateResourceActions());

            // Then
            Assert.IsTrue(exception.Errors.Single().Contains("'SEND'"));
        }

        [TestMethod]
        public async Task ValidateResourceActions_CommandActionHasFields_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
            {
                endpoints.MapQuery<GetItemWithCommandFieldsQuery, IItem>("/items/{itemId}");
                endpoints.MapCommand<ArchiveItemCommand>("/archive/{itemId}");
            });

            // When
            var exception = Assert.ThrowsException<InvalidResourceActionException>(() => app.ValidateResourceActions());

            // Then
            Assert.IsTrue(exception.Errors.Single().Contains("Fields"));
        }

        private static WebApplication BuildApp(Action<WebApplication> mapEndpoints)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton(Substitute.For<IQueryDispatcher>());
            builder.Services.AddSingleton(Substitute.For<IDispatcher>());
            var app = builder.Build();
            mapEndpoints(app);
            return app;
        }

        public interface IItem
        {
            int Id { get; }
        }

        [Resource(AutoItemActions = false)]
        [ItemAction<RemoveItemCommand>]
        [ItemAction("print", "post", "https://print.example/items/{itemId}")]
        public class ListValidItemsQuery : IQuery<IEnumerable<IItem>>
        {
            public int FolderId { get; set; }
        }

        [Resource(Context = "archivable")]
        public class GetValidItemQuery : IQuery<IItem>
        {
            public int ItemId { get; set; }
        }

        [Resource(ItemContext = "archivable", AutoItemActions = false)]
        [ItemAction("print", "POST", "/print/{itemId}")]
        public class GetItemWithItemDeclarationsQuery : IQuery<IItem>
        {
            public int ItemId { get; set; }
        }

        [ResourceAction("print", "SEND", "/print/{itemId}")]
        public class GetItemWithInvalidMethodQuery : IQuery<IItem>
        {
            public int ItemId { get; set; }
        }

        [ResourceAction<ArchiveItemCommand>(Fields = ["reason"])]
        public class GetItemWithCommandFieldsQuery : IQuery<IItem>
        {
            public int ItemId { get; set; }
        }

        public class RemoveItemCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public int FolderId { get; set; }
            public int ItemId { get; set; }
        }

        [Action(Context = "archivable")]
        public class ArchiveItemCommand : ICommand
        {
            public Guid Instance { get; } = Guid.NewGuid();
            public int ItemId { get; set; }
        }
    }
}
