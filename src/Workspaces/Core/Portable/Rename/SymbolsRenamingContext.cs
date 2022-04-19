// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Microsoft.CodeAnalysis.Rename
{
    /// <summary>
    /// Contains a set of symbols's rename location.
    /// </summary>
    internal sealed class SymbolsRenamingContext
    {
        public Solution Solution { get; }

        public ImmutableHashSet<RenameLocations> RenameLocations { get; }

        public ImmutableDictionary<ISymbol, string> renamingSymbolsToNewName { get; }


    }
}
