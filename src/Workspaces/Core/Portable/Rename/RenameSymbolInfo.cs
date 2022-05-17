// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Microsoft.CodeAnalysis.Rename
{
    internal class RenameSymbolInfo
    {
        public string ReplacementText { get; }

        public SymbolRenameOptions Options { get; }

        public ImmutableHashSet<ISymbol>? NonConflictSymbols { get; }

        public RenameLocations RenameLocations { get; }

        public RenameSymbolInfo(string replacementText, SymbolRenameOptions options, ImmutableHashSet<ISymbol>? nonConflictSymbols, RenameLocations renameLocations)
        {
            ReplacementText = replacementText;
            Options = options;
            NonConflictSymbols = nonConflictSymbols;
            RenameLocations = renameLocations;
        }
    }
}
