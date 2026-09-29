using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Themisquo.Analyzers;

namespace Themisquo.Test
{
    [TestClass]
    public class TestQueryHandlerDependencyAnalyzer
    {
        private const string Preamble = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Themisquo;

            public class Query : IQuery<int> { }
            public class Command : ICommand { public Guid Instance => Guid.Empty; }
            """;

        private const string HandleMethod =
            "public Task<int> Handle(Query query, CancellationToken cancellationToken) => Task.FromResult(0);";

        [TestMethod]
        public async Task Analyze_QueryHandlerDependsOnQueryDispatcher_ReportsNothing()
        {
            // Given
            var source = $$"""
                public class Handler : IQueryHandler<Query, int>
                {
                    public Handler(IQueryDispatcher dispatcher) { }
                    {{HandleMethod}}
                }
                """;

            // When
            var diagnostics = await Analyze(source);

            // Then
            Assert.AreEqual(0, diagnostics.Length);
        }

        [TestMethod]
        [DataRow("IDispatcher")]
        [DataRow("Dispatcher")]
        [DataRow("IEventDispatcher")]
        [DataRow("ICommandHandler<Command>")]
        [DataRow("Lazy<IDispatcher>")]
        [DataRow("Func<IDispatcher>")]
        [DataRow("IEnumerable<ICommandHandler<Command>>")]
        [DataRow("IDispatcher[]")]
        public async Task Analyze_QueryHandlerDependsOnForbiddenType_ReportsTHQ001(string dependency)
        {
            // Given
            var source = $$"""
                public class Handler : IQueryHandler<Query, int>
                {
                    public Handler({{dependency}} dependency) { }
                    {{HandleMethod}}
                }
                """;

            // When
            var diagnostics = await Analyze(source);

            // Then
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(QueryHandlerDependencyAnalyzer.DiagnosticId, diagnostics[0].Id);
            Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);
        }

        [TestMethod]
        public async Task Analyze_QueryHandlerPrimaryConstructorDependsOnDispatcher_ReportsTHQ001()
        {
            // Given
            var source = $$"""
                public class Handler(IDispatcher dispatcher) : IQueryHandler<Query, int>
                {
                    private readonly IDispatcher dispatcher = dispatcher;
                    {{HandleMethod}}
                }
                """;

            // When
            var diagnostics = await Analyze(source);

            // Then
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(QueryHandlerDependencyAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [TestMethod]
        public async Task Analyze_QueryHandlerInheritedThroughBaseClass_ReportsTHQ001()
        {
            // Given
            var source = $$"""
                public abstract class HandlerBase : IQueryHandler<Query, int>
                {
                    {{HandleMethod}}
                }

                public class Handler : HandlerBase
                {
                    public Handler(IDispatcher dispatcher) { }
                }
                """;

            // When
            var diagnostics = await Analyze(source);

            // Then
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(QueryHandlerDependencyAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [TestMethod]
        public async Task Analyze_CommandHandlerDependsOnDispatcher_ReportsNothing()
        {
            // Given
            var source = """
                public class Handler : ICommandHandler<Command>
                {
                    public Handler(IDispatcher dispatcher) { }
                    public Task Handle(Command command, IEventDispatcher eventDispatcher, CancellationToken cancellationToken) => Task.CompletedTask;
                }
                """;

            // When
            var diagnostics = await Analyze(source);

            // Then
            Assert.AreEqual(0, diagnostics.Length);
        }

        private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .Append(MetadataReference.CreateFromFile(typeof(IDispatcher).Assembly.Location));

            var compilation = CSharpCompilation.Create(
                "AnalyzerTest",
                [CSharpSyntaxTree.ParseText(Preamble + Environment.NewLine + source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var compilationErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.AreEqual(0, compilationErrors.Count, string.Join(Environment.NewLine, compilationErrors));

            return await compilation
                .WithAnalyzers([new QueryHandlerDependencyAnalyzer()])
                .GetAnalyzerDiagnosticsAsync();
        }
    }
}
