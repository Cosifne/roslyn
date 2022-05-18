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
        internal readonly CancellationToken CancellationToken;
        internal readonly ISet<TextSpan> ConflictLocationSpans;
        internal readonly Solution OriginalSolution;
        internal readonly SyntaxTree OriginalSyntaxTree;
        internal readonly RenamedSpansTracker RenameSpansTracker;
        internal readonly SyntaxNode SyntaxRoot;
        internal readonly Document Document;
        internal readonly SemanticModel SemanticModel;
        internal readonly AnnotationTable<RenameAnnotation> RenameAnnotations;
        internal readonly ImmutableHashSet<RenameRewriterSymbolParameters> SymbolParameters;

        public RenameRewriterParameters(
            Solution originalSolution,
            RenamedSpansTracker renameSpansTracker,
            Document document,
            SemanticModel semanticModel,
            SyntaxNode syntaxRoot,
            ImmutableHashSet<RenameRewriterSymbolParameters> symbolParameters,
            ISet<TextSpan> conflictLocationSpans,
            AnnotationTable<RenameAnnotation> renameAnnotations,
            CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            ConflictLocationSpans = conflictLocationSpans;
            OriginalSolution = originalSolution;
            RenameSpansTracker = renameSpansTracker;
            SyntaxRoot = syntaxRoot;
            Document = document;
            SemanticModel = semanticModel;
            RenameAnnotations = renameAnnotations;
            SymbolParameters = symbolParameters;
            OriginalSyntaxTree = semanticModel.SyntaxTree;
        }
    }
}
