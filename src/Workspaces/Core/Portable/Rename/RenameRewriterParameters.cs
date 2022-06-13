// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.Rename.ConflictEngine;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Rename
{
    internal class RenameRewriterParameters
    {
        internal readonly ISet<TextSpan> ConflictLocationSpans;
        internal readonly Solution OriginalSolution;
        internal readonly SyntaxTree OriginalSyntaxTree;
        internal readonly RenamedSpansTracker RenameSpansTracker;
        internal readonly SyntaxNode SyntaxRoot;
        internal readonly Document Document;
        internal readonly SemanticModel SemanticModel;
        internal readonly AnnotationTable<RenameAnnotation> RenameAnnotations;
        internal readonly ImmutableArray<TextSpanRenameContext> TokenTextSpanRenameContexts;
        internal readonly ImmutableArray<TextSpanRenameContext> StringAndCommentsTextSpanRenameContexts;
        internal readonly ImmutableArray<RenameSymbolContext> RenameSymbolContexts;
        internal readonly CancellationToken CancellationToken;

        public RenameRewriterParameters(
            ISet<TextSpan> conflictLocationSpans,
            Solution originalSolution,
            RenamedSpansTracker renameSpansTracker,
            SyntaxNode syntaxRoot,
            Document document,
            SemanticModel semanticModel,
            AnnotationTable<RenameAnnotation> renameAnnotations,
            ImmutableArray<TextSpanRenameContext> tokenTextSpanRenameContexts,
            ImmutableArray<TextSpanRenameContext> stringAndCommentsTextSpanRenameContexts,
            ImmutableArray<RenameSymbolContext> renameSymbolContexts,
            CancellationToken cancellationToken)
        {
            ConflictLocationSpans = conflictLocationSpans;
            OriginalSolution = originalSolution;
            RenameSpansTracker = renameSpansTracker;
            SyntaxRoot = syntaxRoot;
            Document = document;
            SemanticModel = semanticModel;
            OriginalSyntaxTree = semanticModel.SyntaxTree;
            RenameAnnotations = renameAnnotations;
            TokenTextSpanRenameContexts = tokenTextSpanRenameContexts;
            StringAndCommentsTextSpanRenameContexts = stringAndCommentsTextSpanRenameContexts;
            RenameSymbolContexts = renameSymbolContexts;
            CancellationToken = cancellationToken;
        }
    }
}
