using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.NamingStyles;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.CodeFixes.NamingStyles
{
    internal partial class NamingStyleCodeFixProvider
    {
        private class NamingStyleFixAllProvider : FixAllProvider
        {
            public static readonly NamingStyleFixAllProvider Instance = new();

            private NamingStyleFixAllProvider() { }

            public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
            {
                var document = fixAllContext.Document;
                var diagnostics = fixAllContext.Scope switch
                {
                    FixAllScope.Document when document is not null => await fixAllContext.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false),
                    FixAllScope.Project => await fixAllContext.GetAllDiagnosticsAsync(fixAllContext.Project).ConfigureAwait(false),
                    FixAllScope.Solution => await GetDiagnosticsInAllProjectsAsync(fixAllContext).ConfigureAwait(false),
                    _ => ImmutableArray<Diagnostic>.Empty,
                };

                if (diagnostics.IsEmpty)
                {
                    return null;
                }

                var solution = fixAllContext.Solution;
                var documentToDiagnosticMap = GetDocumentToSymbolMapAsync(solution, diagnostics);

                return null;
            }

            private static ImmutableArray<ISymbol> GetRenamingSymbols(
                ImmutableDictionary<Document, ImmutableArray<Diagnostic>> documentToDiagnosticMap)
            {
            }


            private static async Task<ImmutableDictionary<Document, ImmutableHashSet<ISymbol>>> GetDocumentToSymbolMapAsync(
                Solution solution,
                ImmutableArray<Diagnostic> diagnostics,
                CancellationToken cancellationToken)
            {
                var groupings = diagnostics
                    .WhereAsArray(diagnostic => diagnostic.Location is { SourceTree: not null, IsInSource: true })
                    .GroupBy(diagnostic => diagnostic.Location.SourceTree!);
                using var _1 = PooledDictionary<Document, ImmutableHashSet<ISymbol>>.GetInstance(out var dictionaryBuilder);
                foreach (var grouping in groupings)
                {
                    var syntaxTree = grouping.Key;
                    var document = solution.GetRequiredDocument(syntaxTree);
                    var syntaxFactsService = document.GetRequiredLanguageService<ISyntaxFactsService>();
                    var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                    var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                    using var _2 = PooledHashSet<ISymbol>.GetInstance(out var setBuilder);

                    foreach (var diagnostic in grouping)
                    {
                        var node = syntaxRoot.FindNode(diagnostic.Location.SourceSpan);
                        var symbol = GetSymbol(node, semanticModel, syntaxFactsService, cancellationToken);
                        if (symbol is not null)
                        {
                            setBuilder.Add(symbol); 
                        }
                    }

                    dictionaryBuilder.Add(document, setBuilder.ToImmutableHashSet());
                }

                return dictionaryBuilder.ToImmutableDictionary();
            }

            private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsInAllProjectsAsync(FixAllContext fixAllContext)
            {
                using var _ = ArrayBuilder<Diagnostic>.GetInstance(out var builder);
                foreach (var project in fixAllContext.Solution.Projects)
                {
                    var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                    builder.AddRange(diagnostics);
                }

                return builder.ToImmutable();
            }


            private static Task<CodeAction> GetFixForProject()
            {

            }

            private static Task<CodeAction> GetFixForDocument()
            {

            }
        }
    }
}
