// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Rename.ConflictEngine;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Rename
{
    internal interface IRenameRewriterLanguageService : ILanguageService
    {
        /// <summary>
        /// This method annotates the given syntax tree with all the locations that need to be checked for conflict
        /// after the rename operation.  It also renames all the reference locations and expands any conflict locations.
        /// </summary>
        /// <param name="parameters">The options describing this rename operation</param>
        /// <returns>The root of the annotated tree.</returns>
        SyntaxNode AnnotateAndRename(RenameRewriterParameters parameters);

        /// <summary>
        /// Based on the kind of the symbol and the new name, this function determines possible conflicting names that
        /// should be tracked for semantic changes during rename.
        /// </summary>
        /// <param name="symbol">The symbol that gets renamed.</param>
        /// <param name="newName">The new name for the symbol.</param>
        /// <param name="possibleNameConflicts">List where possible conflicting names will be added to.</param>
        void TryAddPossibleNameConflicts(
            ISymbol symbol,
            string newName,
            ICollection<string> possibleNameConflicts);

        /// <summary>
        /// Identifies the conflicts caused by the new declaration created during rename.
        /// </summary>
        /// <param name="replacementText">The replacementText as given from the user.</param>
        /// <param name="renamedSymbol">The new symbol (after rename).</param>
        /// <param name="renameSymbol">The original symbol that got renamed.</param>
        /// <param name="referencedSymbols">All referenced symbols that are part of this rename session.</param>
        /// <param name="baseSolution">The original solution when rename started.</param>
        /// <param name="newSolution">The resulting solution after rename.</param>
        /// <param name="reverseMappedLocations">A mapping from new to old locations.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>All locations where conflicts were caused because the new declaration.</returns>
        Task<ImmutableArray<Location>> ComputeDeclarationConflictsAsync(
            string replacementText,
            ISymbol renamedSymbol,
            ISymbol renameSymbol,
            IEnumerable<ISymbol> referencedSymbols,
            Solution baseSolution,
            Solution newSolution,
            IDictionary<Location, Location> reverseMappedLocations,
            CancellationToken cancellationToken);

        /// <summary>
        /// Identifies the conflicts caused by implicitly referencing the renamed symbol.
        /// </summary>
        /// <param name="renameSymbol">The original symbol that got renamed.</param>
        /// <param name="renamedSymbol">The new symbol (after rename).</param>
        /// <param name="implicitReferenceLocations">All implicit reference locations.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of implicit conflicts.</returns>
        Task<ImmutableArray<Location>> ComputeImplicitReferenceConflictsAsync(
            ISymbol renameSymbol,
            ISymbol renamedSymbol,
            IEnumerable<ReferenceLocation> implicitReferenceLocations,
            CancellationToken cancellationToken);

        /// <summary>
        /// Identifies the conflicts caused by implicitly referencing the renamed symbol.
        /// </summary>
        /// <param name="renamedSymbol">The new symbol (after rename).</param>
        /// <param name="semanticModel">The SemanticModel of the document in the new solution containing the renamedSymbol</param>
        /// <param name="originalDeclarationLocation">The location of the renamedSymbol in the old solution</param>
        /// <param name="newDeclarationLocationStartingPosition">The starting position of the renamedSymbol in the new solution</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of implicit conflicts.</returns>
        ImmutableArray<Location> ComputePossibleImplicitUsageConflicts(
            ISymbol renamedSymbol,
            SemanticModel semanticModel,
            Location originalDeclarationLocation,
            int newDeclarationLocationStartingPosition,
            CancellationToken cancellationToken);

        /// <summary>
        /// Identifies potential Conflicts into the inner scope locals. This may give false positives.
        /// </summary>
        /// <param name="token">The Token that may introduce errors else where</param>
        /// <param name="newReferencedSymbols">The symbols that this token binds to after the rename
        /// has been applied</param>
        /// <returns>Returns if there is a potential conflict</returns>
        bool LocalVariableConflict(
            SyntaxToken token,
            IEnumerable<ISymbol> newReferencedSymbols);

        /// <summary>
        /// Used to find if the replacement Identifier is valid
        /// </summary>
        /// <param name="replacementText"></param>
        /// <param name="syntaxFactsService"></param>
        /// <returns></returns>
        bool IsIdentifierValid(
            string replacementText,
            ISyntaxFactsService syntaxFactsService);

        /// <summary>
        /// Gets the top most enclosing statement as target to call MakeExplicit on.
        /// It's either the enclosing statement, or if this statement is inside of a lambda expression, the enclosing
        /// statement of this lambda.
        /// </summary>
        /// <param name="token">The token to get the complexification target for.</param>
        /// <returns></returns>
        SyntaxNode? GetExpansionTargetForLocation(SyntaxToken token);

        bool IsRenamableTokenInComment(SyntaxToken token);
    }

    internal abstract class AbstractRenameRewriterLanguageService : IRenameRewriterLanguageService
    {
        public abstract SyntaxNode AnnotateAndRename(RenameRewriterParameters parameters);
        public abstract Task<ImmutableArray<Location>> ComputeDeclarationConflictsAsync(string replacementText, ISymbol renamedSymbol, ISymbol renameSymbol, IEnumerable<ISymbol> referencedSymbols, Solution baseSolution, Solution newSolution, IDictionary<Location, Location> reverseMappedLocations, CancellationToken cancellationToken);
        public abstract Task<ImmutableArray<Location>> ComputeImplicitReferenceConflictsAsync(ISymbol renameSymbol, ISymbol renamedSymbol, IEnumerable<ReferenceLocation> implicitReferenceLocations, CancellationToken cancellationToken);
        public abstract ImmutableArray<Location> ComputePossibleImplicitUsageConflicts(ISymbol renamedSymbol, SemanticModel semanticModel, Location originalDeclarationLocation, int newDeclarationLocationStartingPosition, CancellationToken cancellationToken);
        public abstract SyntaxNode? GetExpansionTargetForLocation(SyntaxToken token);
        public abstract bool IsIdentifierValid(string replacementText, ISyntaxFactsService syntaxFactsService);
        public abstract bool LocalVariableConflict(SyntaxToken token, IEnumerable<ISymbol> newReferencedSymbols);
        public abstract void TryAddPossibleNameConflicts(ISymbol symbol, string newName, ICollection<string> possibleNameConflicts);
        public abstract bool IsRenamableTokenInComment(SyntaxToken token);

        protected static void AddConflictingParametersOfProperties(
            IEnumerable<ISymbol> properties, string newPropertyName, ArrayBuilder<Location> conflicts)
        {
            // check if the new property name conflicts with any parameter of the properties.
            // Note: referencedSymbols come from the original solution, so there is no need to reverse map the locations of the parameters
            foreach (var symbol in properties)
            {
                var prop = (IPropertySymbol)symbol;

                var conflictingParameter = prop.Parameters.FirstOrDefault(
                    param => string.Compare(param.Name, newPropertyName, StringComparison.OrdinalIgnoreCase) == 0);

                if (conflictingParameter != null)
                {
                    conflicts.AddRange(conflictingParameter.Locations);
                }
            }
        }

        protected static Dictionary<TextSpan, RenameSymbolContext> CreateRenameContextDictionary(
            ImmutableHashSet<RenameSymbolContext> symbolParameters)
        {
            var textSpanToSymbolContext = new Dictionary<TextSpan, RenameSymbolContext>();
            foreach (var symbolParameter in symbolParameters)
            {
                var symbolContext = new RenameSymbolContext(
                    Priority: 1,
                    symbolParameter.RenamedSymbolDeclarationAnnotation,
                    symbolParameter.ReplacementText,
                    symbolParameter.OriginalText,
                    symbolParameter.PossibleNameConflicts,
                    symbolParameter.RenameLocations,
                    symbolParameter.RenameSymbol,
                    symbolParameter.RenameSymbol as IAliasSymbol,
                    symbolParameter.RenameSymbol.Locations.FirstOrDefault(loc => loc.IsInSource && loc.SourceTree == semanticModel.SyntaxTree),
                    IsVerbatim: syntaxFactsService.IsVerbatimIdentifier(symbolParameter.ReplacementText),
                    ReplacementTextValid: symbolParameter.ReplacementTextValid,
                    IsRenamingInStrings: symbolParameter.IsRenamingInStrings,
                    IsRenamingInComments: symbolParameter.IsRenamingInComments,
                    StringAndCommentRenameLocations: symbolParameter.StringAndCommentTextSpans,
                    RelatedTextSpans: symbolParameter.RelatedTextSpans);
                foreach (var relatedSpan in symbolParameter.RelatedTextSpans)
                {
                    if (!textSpanToSymbolContext.ContainsKey(relatedSpan))
                    {
                        textSpanToSymbolContext[relatedSpan] = symbolContext;
                    }
                    else
                    {
                        // Each textSpan should only be renamed by one symbol.
                        RoslynDebug.Assert(false);
                    }
                }
            }

            return textSpanToSymbolContext;
        }

        protected static Dictionary<SymbolKey, RenameSymbolContext> GroupRenameContextBySymbolKey(
            ImmutableArray<RenameSymbolContext> symbolContexts)
        {
            var renameContexts = new Dictionary<SymbolKey, RenameSymbolContext>();
            foreach (var context in symbolContexts)
            {
                renameContexts[context.RenamedSymbol.GetSymbolKey()] = context;
            }

            return renameContexts;
        }

        protected static Dictionary<TextSpan, RenameSymbolContext> GroupSymbolContextsByTextSpan(
            IEnumerable<RenameSymbolContext> renameSymbolContexts)
        {
            var textSpanToRenameContext = new Dictionary<TextSpan, RenameSymbolContext>();
            foreach (var symbolContext in renameSymbolContexts.OrderByDescending(c => c.Priority))
            {
                foreach (var (textSpan, _) in symbolContext.RenameLocations)
                {
                    if (!textSpanToRenameContext.ContainsKey(textSpan))
                    {
                        textSpanToRenameContext[textSpan] = symbolContext;
                    }
                    else
                    {
                        // How could one text span is needed to be renamed for two symbols?
                        RoslynDebug.Assert(false);
                    }
                }
            }

            return textSpanToRenameContext;
        }

        protected static Dictionary<TextSpan, HashSet<RenameSymbolContext>> GroupSymbolContextByStringAndCommentTextSpan(
            IEnumerable<RenameSymbolContext> renameSymbolContexts)
        {
            var textSpanToRenameContexts = new Dictionary<TextSpan, HashSet<RenameSymbolContext>>();
            foreach (var symbolContext in renameSymbolContexts)
            {
                foreach (var renameLocation in symbolContext.StringAndCommentRenameLocations)
                {
                    var containingSpan = renameLocation.ContainingLocationForStringOrComment;
                    if (textSpanToRenameContexts.TryGetValue(containingSpan, out var existingContexts))
                    {
                        existingContexts.Add(symbolContext);
                    }
                    else
                    {
                        textSpanToRenameContexts[containingSpan] = new HashSet<RenameSymbolContext>() { symbolContext };
                    }
                }
            }

            return textSpanToRenameContexts;
        }

        protected static ImmutableHashSet<RenameSymbolContext> GetMatchedContexts(IEnumerable<RenameSymbolContext> renameContexts, Func<RenameSymbolContext, bool> predicate)
        {
            using var _ = PooledHashSet<RenameSymbolContext>.GetInstance(out var builder);

            foreach (var renameSymbolContext in renameContexts)
            {
                if (predicate(renameSymbolContext))
                    builder.Add(renameSymbolContext);
            }

            return builder.ToImmutableHashSet();
        }
    }
}
