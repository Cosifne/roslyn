// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Remote;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Rename
{
    internal record SymbolRenameInfo
    {
        public ISymbol Symbol { get; init; }
        public string ReplacementText { get; init; }
        public SymbolRenameOptions Options { get; init; }
        public ImmutableHashSet<ISymbol> NonConflictSymbols { get; init; }

        public SerializableSymbolRenameInfo? Dehydrate(Solution solution, CancellationToken cancellationToken)
        {
            if (SerializableSymbolAndProjectId.TryCreate(Symbol, solution, cancellationToken, out var serializableSymbolAndProjectId))
            {
                var serializableNonConflictSymbols = NonConflictSymbols.SelectAsArray(symbol => SerializableSymbolAndProjectId.Dehydrate(solution, symbol, cancellationToken));
                return new SerializableSymbolRenameInfo()
                {
                    SerilizableSymbol = serializableSymbolAndProjectId,
                    ReplacementText = ReplacementText,
                    Options = Options,
                    SerilizableNonConflictSymbols = serializableNonConflictSymbols
                };
            }

            return null;
        }
    }
}
