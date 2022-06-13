// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.Rename.ConflictEngine;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Rename
{
    /// <summary>
    /// RenameSymbolContext contains all the immutable context information to rename the <paramref name="RenamedSymbol"/>.
    /// </summary>
    internal record RenameSymbolContext(
        int Priority,
        RenameAnnotation RenamableSymbolDeclarationAnnotation,
        string ReplacementText,
        string OriginalText,
        ICollection<string> PossibleNameConflicts,
        ISymbol RenamedSymbol,
        IAliasSymbol? AliasSymbol,
        bool ReplacementTextValid,
        bool IsRenamingInStrings,
        bool IsRenamingInComments);

    internal record TextSpanRenameContext(RenameLocation RenameLocation, RenameSymbolContext SymbolContext)
    {
        public int Priority => SymbolContext.Priority;

        public TextSpanRenameContext GetTextSpanRenameContextForDocument(
            RenameSymbolContext renameSymbolContext,
            Document document,
            SyntaxTree syntaxTree)
        {
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
        }
    }
}
