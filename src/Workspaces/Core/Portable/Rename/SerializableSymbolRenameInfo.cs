// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Remote;
using Roslyn.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Rename
{
    [DataContract]
    internal record SerializableSymbolRenameInfo
    {
        [DataMember(Order = 0)]
        public SerializableSymbolAndProjectId SerilizableSymbol { get; init; }

        [DataMember(Order = 1)]
        public string ReplacementText { get; init; }

        [DataMember(Order = 2)]
        public SymbolRenameOptions Options { get; init; }

        [DataMember(Order = 3)]
        public ImmutableArray<SerializableSymbolAndProjectId> SerilizableNonConflictSymbols { get; init; }

        public async Task<SymbolRenameInfo?> RehydrateAsync(Solution solution, CancellationToken cancellationToken)
        {
            var symbol = await SerilizableSymbol.TryRehydrateAsync(solution, cancellationToken).ConfigureAwait(false);
            if (symbol == null)
                return null;

            var nonConflictSymbols = await GetNonConflictSymbolsAsync(solution, SerilizableNonConflictSymbols, cancellationToken).ConfigureAwait(false);
            return new SymbolRenameInfo()
            {
                Symbol = symbol,
                ReplacementText = ReplacementText,
                Options = Options,
                NonConflictSymbols = nonConflictSymbols,
            };
        }

        private static async Task<ImmutableHashSet<ISymbol>> GetNonConflictSymbolsAsync(Solution solution, ImmutableArray<SerializableSymbolAndProjectId> nonConflictSymbolIds, CancellationToken cancellationToken)
        {
            if (nonConflictSymbolIds.IsEmpty)
            {
                return ImmutableHashSet<ISymbol>.Empty;
            }

            var builder = ImmutableHashSet.CreateBuilder<ISymbol>();
            foreach (var id in nonConflictSymbolIds)
            {
                var symbol = await id.TryRehydrateAsync(solution, cancellationToken).ConfigureAwait(false);
                if (symbol != null)
                    builder.Add(symbol);
            }

            return builder.ToImmutable();
        }
    }
}
