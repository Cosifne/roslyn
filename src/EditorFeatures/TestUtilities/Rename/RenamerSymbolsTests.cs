// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests.Renamer
{
    [UseExportProvider]
    public class RenamerSymbolsTests
    {
        #region AssertHelpers
        private async Task TestRenameSymbolsInDocumentAsync(
            string source,
            string expected,
            string languageName)
        {
            using var workspace = CreateTestWorkspace(source, languageName);
            var cancellationToken = CancellationToken.None;
            var testDocument = workspace.Documents.Single();
            var annotatedSpans = testDocument.AnnotatedSpans;
            var document = workspace.CurrentSolution.GetRequiredDocument(testDocument.Id);
            var builder = ImmutableDictionary.CreateBuilder<ISymbol, (string newName, SymbolRenameOptions options)>();
            var options = new SymbolRenameOptions();
            foreach (var (newName, spans) in annotatedSpans)
            {
                foreach (var span in spans)
                {
                    var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, span.Start, cancellationToken).ConfigureAwait(false);
                    if (symbol is not null)
                    {
                        builder.Add(symbol, (newName, options));
                    }
                }
            }

            await VerifyRenameSymbolsAsync(workspace, builder.ToImmutable(), expected, cancellationToken);
        }

        private async Task TestRenameSymbolsInDocumentAsync(
            string source,
            string expected,
            string languageName,
            ImmutableDictionary<string, (string newName, SymbolRenameOptions options)> tagToNewNameAndOptionsDictionary)
        {
            using var workspace = CreateTestWorkspace(source, languageName);
            var cancellationToken = CancellationToken.None;
            var testDocument = workspace.Documents.Single();
            var annotatedSpans = testDocument.AnnotatedSpans;
            var document = workspace.CurrentSolution.GetRequiredDocument(testDocument.Id);
            var builder = ImmutableDictionary.CreateBuilder<ISymbol, (string newName, SymbolRenameOptions options)>();
            foreach (var (tag, spans) in annotatedSpans)
            {
                if (tagToNewNameAndOptionsDictionary.TryGetValue(tag, out var newNameAndOption))
                {
                    foreach (var span in spans)
                    {
                        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, span.Start, cancellationToken).ConfigureAwait(false);
                        if (symbol is not null)
                        {
                            builder.Add(symbol, newNameAndOption);
                        }
                    }
                }
            }

            await VerifyRenameSymbolsAsync(workspace, builder.ToImmutable(), expected, cancellationToken);
        }

        private async Task TestRenameSymbolsInWorkspaceAsync(
            string source,
            string expected,
            ImmutableDictionary<string, (string newName, SymbolRenameOptions options)> tagToNewNameAndOptionsDictionary)
        {
            using var workspace = TestWorkspace.Create(source);
            var cancellationToken = CancellationToken.None;
            var testDocuments = workspace.Documents.Where(doc => !doc.AnnotatedSpans.IsEmpty()).ToImmutableArray();
            var documents = testDocuments.SelectAsArray(doc => workspace.CurrentSolution.GetRequiredDocument(doc.Id));
            var builder = ImmutableDictionary.CreateBuilder<ISymbol, (string newName, SymbolRenameOptions options)>();

            for (var i = 0; i < testDocuments.Length; i++)
            {
                var testDocument = testDocuments[i];
                var document = documents[i];
                foreach (var (tag, spans) in testDocument.AnnotatedSpans)
                {
                    if (tagToNewNameAndOptionsDictionary.TryGetValue(tag, out var newNameAndOption))
                    {
                        foreach (var span in spans)
                        {
                            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, span.Start, cancellationToken).ConfigureAwait(false);
                            if (symbol is not null)
                            {
                                builder.Add(symbol, newNameAndOption);
                            }
                        }
                    }
                }
            }

            var newSolution = await Rename.Renamer.RenameSymbolsAsync(
                workspace.CurrentSolution,
                builder.ToImmutable(),
                cancellationToken).ConfigureAwait(false);

            var expectedWorkspace = TestWorkspace.Create(expected);
            await VerifyTwoSolution(expectedWorkspace.CurrentSolution, newSolution);
        }

        private async Task VerifyTwoSolution(Solution expected, Solution actual)
        {
            var expectedProjects = expected.Projects.OrderBy(p => p.Name).ToImmutableArray();
            var actualProjects = actual.Projects.OrderBy(p => p.Name).ToImmutableArray();

            Assert.Equal(expectedProjects.Length, actualProjects.Length);
            for (var i = 0; i < expectedProjects.Length; i++)
            {
                await VerifyTwoProjects(expectedProjects[i], actualProjects[i]);
            }
        }

        private async Task VerifyTwoProjects(Project expected, Project actual)
        {
            var expectedDocuments = expected.Documents
                .OrderBy(d => d.Name)
                .ThenBy(d => d.Project.Name).ToImmutableArray();
            var actualDocuments = actual.Documents
                .OrderBy(d => d.Name)
                .ThenBy(d => d.Project.Name).ToImmutableArray();
            Assert.Equal(expectedDocuments.Length, actualDocuments.Length);

            for (var i = 0; i < actualDocuments.Length; i++)
            {
                await VerifyTwoDocumentsAsync(expectedDocuments[i], actualDocuments[i]);
            }
        }

        private async Task VerifyTwoDocumentsAsync(Document expected, Document actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            var expectedText = await expected.GetTextAsync().ConfigureAwait(false);
            var actualText = await actual.GetTextAsync().ConfigureAwait(false);
            Assert.Equal(expectedText, actualText);
        }

        private static async Task VerifyRenameSymbolsAsync(
            Workspace workspace,
            ImmutableDictionary<ISymbol, (string newName, SymbolRenameOptions options)> renameSymbols,
            string expected,
            CancellationToken cancellationToken)
        {
            var solution = await Rename.Renamer.RenameSymbolsAsync(
                workspace.CurrentSolution,
                renameSymbols,
                cancellationToken).ConfigureAwait(false);

            var document = solution.Projects.Single().Documents.Single();
            var actualText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            AssertEx.EqualOrDiff(actualText.ToString(), expected);

            var renameDocumentSymbols = renameSymbols.Where(kvp => kvp.Value.options.RenameFile).ToImmutableArray();
            if (!renameDocumentSymbols.IsEmpty)
            {
                var renamingDocumentSymbol = renameSymbols.Single();
                Assert.Equal(renamingDocumentSymbol.Value.newName, document.Name);
            }
        }

        private static TestWorkspace CreateTestWorkspace(string source, string languageName)
        {
            var code = @$"<![CDATA[
{source}]]>";

            var workspaceFile = $@"
<Workspace>
   <Project Language=""{languageName}"" CommonReferences=""true"">
       <Document>
            {code}
       </Document>
   </Project>
</Workspace>";

            return TestWorkspace.Create(workspaceFile);
        }
        #endregion
    }
}
