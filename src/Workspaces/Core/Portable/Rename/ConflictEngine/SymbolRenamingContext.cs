// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Microsoft.CodeAnalysis.Rename.ConflictEngine
{
    /// <summary>
    /// Contains the immutable context information to rename a symbol.
    /// </summary>
    internal sealed class SymbolRenamingContext
    {
        // Set of All Locations that will be renamed (does not include non-reference locations that need to be checked for conflicts)
        public RenameLocations RenameLocations { get; }

        // Rename Symbol's Source Location
        public Location RenameSymbolDeclarationLocation { get; }
        public DocumentId DocumentIdOfRenameSymbolDeclaration { get; }
        public string OriginalText { get; }
        public string ReplacementText { get; }
        public SymbolRenameOptions Options { get; }
        public ImmutableHashSet<ISymbol>? NonConflictSymbols { get; }
        public RenameAnnotation RenamedSymbolDeclarationAnnotation { get; }
        public bool ReplacementTextValid { get; }

        public SymbolRenamingContext(
            RenameLocations renameLocations,
            Location renameSymbolDeclarationLocation,
            DocumentId documentIdOfRenameSymbolDeclaration,
            string originalText,
            string replacementText,
            SymbolRenameOptions options,
            ImmutableHashSet<ISymbol>? nonConflictSymbols,
            RenameAnnotation renamedSymbolDeclarationAnnotation)
        {
            RenameLocations = renameLocations;
            RenameSymbolDeclarationLocation = renameSymbolDeclarationLocation;
            DocumentIdOfRenameSymbolDeclaration = documentIdOfRenameSymbolDeclaration;
            OriginalText = originalText;
            ReplacementText = replacementText;
            Options = options;
            NonConflictSymbols = nonConflictSymbols;
            RenamedSymbolDeclarationAnnotation = renamedSymbolDeclarationAnnotation;
            ReplacementTextValid = replacementTextValid;
        }
    }
}
