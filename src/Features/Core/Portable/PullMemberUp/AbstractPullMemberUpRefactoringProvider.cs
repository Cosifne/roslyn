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

<<<<<<< HEAD
            var selectedMember = semanticModel.GetDeclaredSymbol(selectedMemberNode);
            if (selectedMember == null || selectedMember.ContainingType == null)
=======
            var userSelectNodeSymbol = semanticModel.GetDeclaredSymbol(userSelectedNode);
            if (userSelectNodeSymbol == null)
>>>>>>> Move location of services
            {
                return;
            }

<<<<<<< HEAD
            if (!selectedMember.IsKind(SymbolKind.Property) &&
                !selectedMember.IsKind(SymbolKind.Event) &&
                !selectedMember.IsKind(SymbolKind.Field) &&
                !selectedMember.IsKind(SymbolKind.Method))
=======
            var allTargetClasses = FindAllTargetBaseClasses(userSelectNodeSymbol.ContainingType, context.Document.Project.Solution, context.CancellationToken);
            var allTargetInterfaces = FindAllTargetInterfaces(userSelectNodeSymbol.ContainingType, context.Document.Project.Solution, context.CancellationToken);

            if (allTargetInterfaces.Count == 0 && allTargetClasses.Count == 0)
>>>>>>> Move location of services
            {
                // Static, abstract and accessiblity are not checked here but in PullMemberUpAnalyzer.cs since there are
                // two refactoring options provided for pull members up,
                // 1. Quick Action (Only allow members that don't cause error)
                // 2. Dialog box (Allow modifers may cause errors and will provide fixing)
                return;
            }

<<<<<<< HEAD
            if (selectedMember is IMethodSymbol methodSymbol && !methodSymbol.IsOrdinaryMethod())
            {
                return;
            }

            if (!IsSelectionValid(context.Span, selectedMemberNode))
            {
                return;
=======
            if (userSelectNodeSymbol.Kind == SymbolKind.Method ||
                userSelectNodeSymbol.Kind == SymbolKind.Property ||
                userSelectNodeSymbol.Kind == SymbolKind.Event)
            {
                await AddPullUpMemberToClassRefactoringViaQuickAction(allTargetClasses, userSelectedNode, userSelectNodeSymbol, context);
                await AddPullUpMemberToInterfaceRefactoringViaQuickAction(allTargetInterfaces, userSelectedNode, userSelectNodeSymbol, context);
                AddPullUpMemberRefactoringViaDialogBox(userSelectNodeSymbol, context, root, semanticModel);
            }
            else if (userSelectNodeSymbol.Kind == SymbolKind.Field)
            {
                await AddPullUpMemberToClassRefactoringViaQuickAction(allTargetClasses, userSelectedNode, userSelectNodeSymbol, context);
                AddPullUpMemberRefactoringViaDialogBox(userSelectNodeSymbol, context, root, semanticModel);
>>>>>>> Move location of services
            }

            var allDestinations = FindAllValidDestinations(
                selectedMember,
                document.Project.Solution,
                context.CancellationToken);
            if (allDestinations.Length == 0)
            {
                return;
            }
            
            PullMemberUpViaQuickAction(context, selectedMember, allDestinations);
        }

        private ImmutableArray<INamedTypeSymbol> FindAllValidDestinations(
            ISymbol selectedMember,
            Solution solution,
            CancellationToken cancellationToken)
        {
            var containingType = selectedMember.ContainingType;
            var allDestinations = selectedMember.IsKind(SymbolKind.Field)
                ? containingType.GetBaseTypes().ToImmutableArray()
                : containingType.AllInterfaces.Concat(containingType.GetBaseTypes()).ToImmutableArray();

            return allDestinations.WhereAsArray(baseType =>
                baseType != null &&
                // It could be ErrorType if there is syntax error on the baseType
                (baseType.TypeKind == TypeKind.Interface || baseType.TypeKind == TypeKind.Class) &&
                baseType.DeclaringSyntaxReferences.Length > 0 &&
                IsLocationValid(baseType, solution, cancellationToken));
        }

<<<<<<< HEAD
        private bool IsLocationValid(INamedTypeSymbol symbol, Solution solution, CancellationToken cancellationToken)
        {
            return symbol.Locations.Any(location => location.IsInSource &&
                !solution.GetDocument(location.SourceTree).IsGeneratedCode(cancellationToken));
        }

        private void PullMemberUpViaQuickAction(
            CodeRefactoringContext context,
            ISymbol selectedMember,
            ImmutableArray<INamedTypeSymbol> destinations)
        {
            foreach (var destination in destinations)
            {
                var puller = destination.TypeKind == TypeKind.Interface
                    ? InterfacePullerWithQuickAction.Instance as AbstractMemberPullerWithQuickAction
                    : ClassPullerWithQuickAction.Instance;
                var action = puller.TryComputeRefactoring(context.Document, selectedMember, destination);
=======
        protected async Task AddPullUpMemberToClassRefactoringViaQuickAction(
            List<INamedTypeSymbol> targetClasses,
            SyntaxNode userSelectedNode,
            ISymbol userSelectNodeSymbol,
            CodeRefactoringContext context)
        {
            var puller = context.Document.Project.LanguageServices.GetService<AbstractClassPullerWithQuickAction>();
            foreach (var eachClass in targetClasses)
            {
                var action = await puller.ComputeRefactoring(eachClass, context, userSelectedNode, userSelectNodeSymbol);
                if (action != null)
                {
                    context.RegisterRefactoring(action);
                }
            }
        }

        protected async virtual Task AddPullUpMemberToInterfaceRefactoringViaQuickAction(
            List<INamedTypeSymbol> targetInterfaces,
            SyntaxNode userSelectedNode,
            ISymbol userSelectNodeSymbol,
            CodeRefactoringContext context)
        {
            var puller = new InterfacePullerWithQuickAction();
            foreach (var eachInterface in targetInterfaces)
            {
                var action = await puller.ComputeRefactoring(eachInterface, context, userSelectedNode, userSelectNodeSymbol);

>>>>>>> Move location of services
                if (action != null)
                {
                    context.RegisterRefactoring(action);
                }
            }
        }
    }
}
