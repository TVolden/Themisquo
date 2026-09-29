using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Themisquo.Analyzers
{
    /// <summary>
    /// Reports query handlers whose public constructors take a dependency that would let them change state:
    /// IDispatcher, IEventDispatcher or ICommandHandler&lt;T&gt;, including when wrapped in another generic type
    /// such as Lazy&lt;IDispatcher&gt;. Mirrors the runtime check in Themisquo.QueryHandlerDependencies.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class QueryHandlerDependencyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "THQ001";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "Query handlers must not depend on command-side services",
            messageFormat: "Query handler '{0}' must not depend on '{1}', as it would allow the query to change state; use IQueryDispatcher instead",
            category: "CQRS",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Queries must not change state. Injecting IDispatcher, IEventDispatcher or ICommandHandler<T> into a query handler would allow it to dispatch commands or raise events.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var compilation = compilationContext.Compilation;
                var queryHandler = compilation.GetTypeByMetadataName("Themisquo.IQueryHandler`2");
                var commandHandler = compilation.GetTypeByMetadataName("Themisquo.ICommandHandler`1");
                var dispatcher = compilation.GetTypeByMetadataName("Themisquo.IDispatcher");
                var eventDispatcher = compilation.GetTypeByMetadataName("Themisquo.IEventDispatcher");
                if (queryHandler is null)
                {
                    return;
                }

                var checker = new ForbiddenTypeChecker(commandHandler, dispatcher, eventDispatcher);
                compilationContext.RegisterSymbolAction(
                    symbolContext => AnalyzeType(symbolContext, queryHandler, checker),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol queryHandler, ForbiddenTypeChecker checker)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Class ||
                !type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, queryHandler)))
            {
                return;
            }

            foreach (var constructor in type.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                foreach (var parameter in constructor.Parameters)
                {
                    if (checker.IsForbidden(parameter.Type))
                    {
                        var location = parameter.Locations.FirstOrDefault() ?? type.Locations.FirstOrDefault();
                        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, parameter.Type.ToDisplayString()));
                    }
                }
            }
        }

        private sealed class ForbiddenTypeChecker
        {
            private readonly INamedTypeSymbol? commandHandler;
            private readonly INamedTypeSymbol? dispatcher;
            private readonly INamedTypeSymbol? eventDispatcher;

            public ForbiddenTypeChecker(INamedTypeSymbol? commandHandler, INamedTypeSymbol? dispatcher, INamedTypeSymbol? eventDispatcher)
            {
                this.commandHandler = commandHandler;
                this.dispatcher = dispatcher;
                this.eventDispatcher = eventDispatcher;
            }

            public bool IsForbidden(ITypeSymbol type)
            {
                if (IsOrImplements(type, dispatcher) || IsOrImplements(type, eventDispatcher) || IsOrImplements(type, commandHandler))
                {
                    return true;
                }

                return type switch
                {
                    IArrayTypeSymbol array => IsForbidden(array.ElementType),
                    INamedTypeSymbol { IsGenericType: true } named => named.TypeArguments.Any(IsForbidden),
                    _ => false,
                };
            }

            private static bool IsOrImplements(ITypeSymbol type, INamedTypeSymbol? target)
            {
                if (target is null)
                {
                    return false;
                }

                return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, target) ||
                    type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, target));
            }
        }
    }
}
