// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Rename.ConflictEngine
{
    internal partial class ConflictResolver
    {
        private class SymbolSession
        {
            private readonly int Priority;
            // Set of All Locations that will be renamed (does not include non-reference locations that need to be checked for conflicts)
            public readonly RenameLocations RenameLocationSet;

            // Rename Symbol's Source Location
            public readonly Location RenameSymbolDeclarationLocation;
            public readonly DocumentId DocumentIdOfRenameSymbolDeclaration;
            public readonly string OriginalText;
            public readonly string ReplacementText;
            public readonly ImmutableHashSet<ISymbol>? NonConflictSymbols;

            public readonly RenameAnnotation RenamedSymbolDeclarationAnnotation = new();

            // Contains Strings like Bar -> BarAttribute ; Property Bar -> Bar , get_Bar, set_Bar
            public readonly ImmutableArray<string> PossibleNameConflicts = new();
            public readonly ImmutableHashSet<DocumentId> DocumentsIdsToBeCheckedForConflict;

            public readonly bool ReplacementTextValid;
            public readonly RenameSymbolContext RenameSymbolContext;

            public bool DocumentOfRenameSymbolHasBeenRenamed { get; set; } = false;
            public SymbolRenameOptions RenameOptions => RenameLocationSet.Options;

            public SymbolSession(
                int priority,
                RenameLocations renameLocationSet,
                Location renameSymbolDeclarationLocation,
                DocumentId documentIdOfRenameSymbolDeclaration,
                string originalText,
                string replacementText,
                ImmutableHashSet<ISymbol>? nonConflictSymbols,
                RenameAnnotation renamedSymbolDeclarationAnnotation,
                ImmutableArray<string> possibleNameConflicts,
                ImmutableHashSet<DocumentId> documentsIdsToBeCheckedForConflict,
                bool replacementTextValid)
            {
                Priority = priority;
                RenameLocationSet = renameLocationSet;
                RenameSymbolDeclarationLocation = renameSymbolDeclarationLocation;
                DocumentIdOfRenameSymbolDeclaration = documentIdOfRenameSymbolDeclaration;
                OriginalText = originalText;
                ReplacementText = replacementText;
                NonConflictSymbols = nonConflictSymbols;
                RenamedSymbolDeclarationAnnotation = renamedSymbolDeclarationAnnotation;
                PossibleNameConflicts = possibleNameConflicts;
                DocumentsIdsToBeCheckedForConflict = documentsIdsToBeCheckedForConflict;
                ReplacementTextValid = replacementTextValid;

                var symbol = RenameLocationSet.Symbol;
                RenameSymbolContext = new RenameSymbolContext(
                    priority,
                    renamedSymbolDeclarationAnnotation,
                    replacementText,
                    originalText,
                    possibleNameConflicts,
                    symbol,
                    symbol as IAliasSymbol,
                    symbol.Locations.FirstOrDefault(loc => loc.IsInSource && loc.SourceTree == syntaxTree),
                    ReplacementTextValid,
                    RenameLocationSet.Options.RenameInStrings,
                    RenameLocationSet.Options.RenameInComments);
            }

            public RenameSymbolContext GetRenameSymbolContextForDocument(
                Document document,
                SyntaxTree syntaxTree)
            {
                var syntaxFactsService = document.Project.GetRequiredLanguageService<ISyntaxFactsService>();

                //Get all rename locations for the current document.
                var renameLocations = RenameLocationSet.Locations;
                using var _ = PooledHashSet<RenameLocation>.GetInstance(out var renameLocationsInDocument);

                foreach (var location in renameLocations)
                {
                    if (location.DocumentId == document.Id)
                        renameLocationsInDocument.Add(location);
                }

                var allTextSpansInSingleSourceTree = renameLocationsInDocument
                    .Where(renameLocation => ShouldIncludeLocation(renameLocations, renameLocation))
                    .ToImmutableArray();

                // All textspan in the document documentId, that requires rename in String or Comment
                var stringAndCommentTextSpansInSingleSourceTree = renameLocationsInDocument
                    .Where(renaleLocation => renaleLocation.IsRenameInStringOrComment)
                    .ToImmutableArray();

                var symbol = RenameLocationSet.Symbol;

                return new RenameSymbolContext(
                    Priority,
                    RenamedSymbolDeclarationAnnotation,
                    ReplacementText,
                    OriginalText,
                    PossibleNameConflicts,
                    allTextSpansInSingleSourceTree,
                    symbol,
                    symbol as IAliasSymbol,
                    symbol.Locations.FirstOrDefault(loc => loc.IsInSource && loc.SourceTree == syntaxTree),
                    syntaxFactsService.IsVerbatimIdentifier(ReplacementText),
                    ReplacementTextValid,
                    RenameLocationSet.Options.RenameInStrings,
                    RenameLocationSet.Options.RenameInComments,
                    stringAndCommentTextSpansInSingleSourceTree);
            }
        }
    }
}
