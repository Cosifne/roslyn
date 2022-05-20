// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Microsoft.CodeAnalysis.Rename
{
    internal record RenameSymbolInfo(string ReplacementText, ImmutableHashSet<ISymbol>? NonConflictSymbols, RenameLocations RenameLocations);
}
