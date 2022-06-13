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
            public readonly RenameSymbolContext RenameSymbolContext;

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
                    ReplacementTextValid,
                    RenameLocationSet.Options.RenameInStrings,
                    RenameLocationSet.Options.RenameInComments);
            }
        }
    }
}
