// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.PullMemberUp.QuickAction;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp
{
    internal abstract class AbstractPullMemberUpRefactoringProvider : CodeRefactoringProvider
    {
        protected abstract bool IsSelectionValid(TextSpan span, SyntaxNode selectedMemberNode);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            // Currently support to pull field, method, event, property and indexer up,
            // constructor, operator and finalizer are excluded.
            var document = context.Document;
            var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var selectedMemberNode = root.FindNode(context.Span);

            if (selectedMemberNode == null)
            {
                return;
            }

            var selectedMember = semanticModel.GetDeclaredSymbol(selectedMemberNode);
            if (selectedMember == null || selectedMember.ContainingType == null)
            {
                return;
            }

            if (!selectedMember.IsKind(SymbolKind.Property) &&
                !selectedMember.IsKind(SymbolKind.Event) &&
                !selectedMember.IsKind(SymbolKind.Field) &&
                !selectedMember.IsKind(SymbolKind.Method))
            {
                // Static, abstract and accessiblity are not checked here but in PullMemberUpAnalyzer.cs since there are
                // two refactoring options provided for pull members up,
                // 1. Quick Action (Only allow members that don't cause error)
                // 2. Dialog box (Allow modifers may cause errors and will provide fixing)
                return;
            }

            if (selectedMember is IMethodSymbol methodSymbol && !methodSymbol.IsOrdinaryMethod())
            {
                return;
            }

            if (!IsSelectionValid(context.Span, selectedMemberNode))
            {
                return;
            }

            var allDestinations = await FindAllValidDestinations(
                selectedMember,
                document.Project,
                context.CancellationToken);
            if (allDestinations.Length == 0)
            {
                return;
            }
            
            PullMemberUpViaQuickAction(context, selectedMember, allDestinations);
        }

        private async Task<ImmutableArray<INamedTypeSymbol>> FindAllValidDestinations(
            ISymbol selectedMember,
            Project contextProject,
            CancellationToken cancellationToken)
        {
            var containingType = selectedMember.ContainingType;
            var allDestinations = selectedMember.IsKind(SymbolKind.Field)
                ? containingType.GetBaseTypes().ToImmutableArray()
                : containingType.AllInterfaces.Concat(containingType.GetBaseTypes()).ToImmutableArray();

            var solution = contextProject.Solution;
            var validDestinations = await allDestinations.WhereAsArray(baseType => !IsGeneratedCode(baseType, solution, cancellationToken)).
                GroupBy(destination => solution.GetProject(destination.ContainingAssembly)).
                Where(groupedDestination => groupedDestination.Key != null).
                SelectManyAsync(async (groupedDestinations, token) =>
                {
                    var project = groupedDestinations.Key;
                    if (project.Language != contextProject.Language)
                    {
                        return await GetDestinationSymbolForDifferentProject(groupedDestinations, token);
                    }
                    else
                    {
                        return await Task.FromResult(groupedDestinations.AsEnumerable());
                    }
                }, cancellationToken);

            return validDestinations.ToImmutableArray();
        }

        private async Task<ImmutableArray<INamedTypeSymbol>> GetDestinationSymbolForDifferentProject(
            IGrouping<Project, INamedTypeSymbol> groupedDestinations, CancellationToken cancellationToken)
        {
            var compilation = await groupedDestinations.Key.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var symbolFromDestinationProject = groupedDestinations.
                Select(originalDestination =>
                {
                    if (originalDestination.TypeKind == TypeKind.Interface)
                    {
                        var symbolId = SymbolKey.Create(originalDestination, cancellationToken);
                        return symbolId.Resolve(compilation, cancellationToken: cancellationToken).Symbol as INamedTypeSymbol;
                    }
                    else
                    {
                        return null;
                    }
                }).Where(namedTypeSymbol => namedTypeSymbol != null).ToImmutableArray();

            return symbolFromDestinationProject;
        }

        private bool IsGeneratedCode(INamedTypeSymbol symbol, Solution solution, CancellationToken cancellationToken)
        {
            return symbol.Locations.Any(location =>
                solution.GetDocument(location.SourceTree).IsGeneratedCode(cancellationToken));
        }

        private void PullMemberUpViaQuickAction(
            CodeRefactoringContext context,
            ISymbol selectedMember,
            ImmutableArray<INamedTypeSymbol> destinations)
        {
            foreach (var destination in destinations)
            {
                if (destination.TypeKind == TypeKind.Interface ||
                    destination.TypeKind == TypeKind.Class)
                {
                    var puller = destination.TypeKind == TypeKind.Interface
                        ? InterfacePullerWithQuickAction.Instance as AbstractMemberPullerWithQuickAction
                        : ClassPullerWithQuickAction.Instance;
                    var action = puller.TryComputeRefactoring(context.Document, selectedMember, destination);
                    if (action != null)
                    {
                        context.RegisterRefactoring(action);
                    }
                }
            }
        }
    }
}
