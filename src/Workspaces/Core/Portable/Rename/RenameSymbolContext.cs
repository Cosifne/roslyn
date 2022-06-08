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
    internal readonly record struct RenameSymbolContext(
        int Priority,
        RenameAnnotation RenameRenamableSymbolDeclarationAnnotation,
        string ReplacementText,
        string OriginalText,
        ICollection<string> PossibleNameConflicts,
        Dictionary<TextSpan, RenameLocation> RenameLocations,
        ISymbol RenamedSymbol,
        IAliasSymbol? AliasSymbol,
        Location? RenamableDeclarationLocation,
        bool IsVerbatim,
        bool ReplacementTextValid,
        bool IsRenamingInStrings,
        bool IsRenamingInComments,
        HashSet<RenameLocation> StringAndCommentRenameLocations,
        ImmutableHashSet<TextSpan> RelatedTextSpans);
}
