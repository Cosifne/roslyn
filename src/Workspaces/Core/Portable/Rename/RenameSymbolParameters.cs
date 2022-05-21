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
    internal readonly record struct RenameRewriterSymbolParameters(
        bool IsRenamingInStrings,
        bool IsRenamingInComments,
        string OriginalText,
        ICollection<string> PossibleNameConflicts,
        RenameAnnotation RenamedSymbolDeclarationAnnotation,
        Dictionary<TextSpan, RenameLocation> RenameLocations,
        ISymbol RenameSymbol,
        string ReplacementText,
        bool ReplacementTextValid,
        ImmutableDictionary<TextSpan, ImmutableSortedSet<TextSpan>?> StringAndCommentTextSpans,
        ImmutableHashSet<TextSpan> RelatedTextSpans);
}
