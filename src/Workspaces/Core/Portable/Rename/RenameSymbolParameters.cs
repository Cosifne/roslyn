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
    internal class RenameSymbolParameters
    {
        internal readonly bool IsRenamingInStrings;
        internal readonly bool IsRenamingInComments;
        internal readonly string OriginalText;
        internal readonly ICollection<string> PossibleNameConflicts;
        internal readonly RenameAnnotation RenamedSymbolDeclarationAnnotation;
        internal readonly Dictionary<TextSpan, RenameLocation> RenameLocations;
        internal readonly ISymbol RenameSymbol;
        internal readonly string ReplacementText;
        internal readonly bool ReplacementTextValid;
        internal readonly ImmutableDictionary<TextSpan, ImmutableSortedSet<TextSpan>?> StringAndCommentTextSpans;
    }
}
