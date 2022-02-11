using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CodeFixes.NamingStyles
{
    internal partial class NamingStyleCodeFixProvider
    {
        private class NamingStyleFixAllProvider : FixAllProvider
        {
            public static readonly NamingStyleFixAllProvider Instance = new();

            private NamingStyleFixAllProvider()
            {
            }

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
        }
    }
}
