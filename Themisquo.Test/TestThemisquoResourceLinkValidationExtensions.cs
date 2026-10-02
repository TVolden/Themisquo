using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Themisquo.AspNetCore;

namespace Themisquo.Test
{
    [TestClass]
    public class TestThemisquoResourceLinkValidationExtensions
    {
        [TestMethod]
        public async Task ValidateResourceLinks_AllDeclarationsValid_ReturnsEndpoints()
        {
            // Given
            await using var app = BuildApp(endpoints =>
            {
                endpoints.MapQuery<ListLinkedItemsQuery, IEnumerable<IItem>>("/folders/{folderId}/items");
                endpoints.MapQuery<GetFolderQuery, IFolder>("/folders/{folderId}");
            });

            // When
            var result = app.ValidateResourceLinks();

            // Then
            Assert.AreSame(app, result);
        }

        [TestMethod]
        public async Task ValidateResourceLinks_LinkedQueryNotMapped_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
                endpoints.MapQuery<ListLinkedItemsQuery, IEnumerable<IItem>>("/folders/{folderId}/items"));

            // When
            var exception = Assert.ThrowsException<InvalidResourceLinkException>(() => app.ValidateResourceLinks());

            // Then
            Assert.IsTrue(exception.Errors.Single().Contains(nameof(GetFolderQuery)));
        }

        [TestMethod]
        public async Task ValidateResourceLinks_LinkedQueryMappedAsPost_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
            {
                endpoints.MapQuery<ListLinkedItemsQuery, IEnumerable<IItem>>("/folders/{folderId}/items");
                endpoints.MapQuery<GetFolderQuery, IFolder>("/folders/{folderId}", "POST");
            });

            // When
            var exception = Assert.ThrowsException<InvalidResourceLinkException>(() => app.ValidateResourceLinks());

            // Then
            Assert.IsTrue(exception.Errors.Single().Contains("GET"));
        }

        [TestMethod]
        public async Task ValidateResourceLinks_ItemDeclarationsOnSingleQuery_Throws()
        {
            // Given
            await using var app = BuildApp(endpoints =>
                endpoints.MapQuery<GetFolderWithItemDeclarationsQuery, IFolder>("/folders/{folderId}"));

            // When
            var exception = Assert.ThrowsException<InvalidResourceLinkException>(() => app.ValidateResourceLinks());

            // Then
            Assert.AreEqual(2, exception.Errors.Count);
            Assert.IsTrue(exception.Errors.Any(e => e.Contains("item links")));
            Assert.IsTrue(exception.Errors.Any(e => e.Contains("AutoItemLinks")));
        }

        private static WebApplication BuildApp(Action<WebApplication> mapEndpoints)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton(Substitute.For<IQueryDispatcher>());
            var app = builder.Build();
            mapEndpoints(app);
            return app;
        }

        public interface IItem
        {
            int Id { get; }
        }

        public interface IFolder
        {
            int FolderId { get; }
        }

        [Resource(AutoItemLinks = false)]
        [ItemLink<GetFolderQuery>(Rel = "folder")]
        [ItemLink("preview", "https://preview.example/items/{itemId}")]
        public class ListLinkedItemsQuery : IQuery<IEnumerable<IItem>>
        {
            public int FolderId { get; set; }
        }

        public class GetFolderQuery : IQuery<IFolder>
        {
            public int FolderId { get; set; }
        }

        [Resource(AutoItemLinks = false)]
        [ItemLink("preview", "/preview/{folderId}")]
        public class GetFolderWithItemDeclarationsQuery : IQuery<IFolder>
        {
            public int FolderId { get; set; }
        }
    }
}
