﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp.Dialog;

namespace Microsoft.CodeAnalysis.CodeRefactorings.PullMembrUp.Dialog
{
    internal class PullMemberUpWithDialogCodeAction : CodeActionWithOptions
    {
        private IPullMemberUpOptionsService PullMemberUpService { get; }

        private IEnumerable<ISymbol> Members { get; }

        private ISymbol SelectedNodeSymbol { get; }

        private Document ContextDocument { get; }

        private Dictionary<ISymbol, Lazy<List<ISymbol>>> LazyDependentsMap { get; }

        public override string Title => "...";

        internal PullMemberUpWithDialogCodeAction(
            SemanticModel semanticModel,
            CodeRefactoringContext context,
            ISymbol selectedNodeSymbol)
        {
            PullMemberUpService = context.Document.Project.Solution.Workspace.Services.GetService<IPullMemberUpOptionsService>();
            Members = selectedNodeSymbol.ContainingType.GetMembers().Where(
                member => {
                    if (member is IMethodSymbol methodSymbol)
                    {
                        return methodSymbol.MethodKind == MethodKind.Ordinary;
                    }
                    else if (member is IFieldSymbol fieldSymbol)
                    {
                        return !member.IsImplicitlyDeclared;
                    }
                    else if (member.Kind == SymbolKind.Property || member.Kind == SymbolKind.Event)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });

            var membersSet = new HashSet<ISymbol>(Members);

            // This map contains the content used by select dependents button
            LazyDependentsMap = Members.ToDictionary(
                memberSymbol => memberSymbol,
                memberSymbol => new Lazy<List<ISymbol>>(
                   () =>
                   {
                       if (memberSymbol.Kind == SymbolKind.Field || memberSymbol.Kind == SymbolKind.Event)
                       {
                           return new List<ISymbol>();
                       }
                       else
                       {
                           return SymbolDependentsBuilder.Build(semanticModel, memberSymbol, membersSet, context.Document, context.CancellationToken);
                       }

                   }, false));
            SelectedNodeSymbol = selectedNodeSymbol;
            ContextDocument = context.Document;
        }

        public override object GetOptions(CancellationToken cancellationToken)
        {
            var result = PullMemberDialogResult.CanceledResult;
            do
            {
                result = PullMemberUpService.GetPullTargetAndMembers(SelectedNodeSymbol, Members, LazyDependentsMap);
                if (result.IsCanceled)
                {
                    PullMemberUpService.ResetSession();
                    return result;
                }
                else
                {
                    var analysisResult = PullMembersUpAnalysisBuilder.BuildAnalysisResult(result.Target, result.SelectedMembers);
                    if (analysisResult.IsValid)
                    {
                        PullMemberUpService.ResetSession();
                        return result;
                    }
                    else
                    {
                        var proceedToRefactoring = PullMemberUpService.CreateWarningDialog(analysisResult);
                        if (proceedToRefactoring)
                        {
                            PullMemberUpService.ResetSession();
                            return result;
                        }
                    }
                }
            } while (!cancellationToken.IsCancellationRequested &&
                     !result.IsCanceled);

            PullMemberUpService.ResetSession();
            return result;
        }
        
        protected async override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(object options, CancellationToken cancellationToken)
        {
            if (options is PullMemberDialogResult result && !result.IsCanceled)
            {
                if (result.Target.TypeKind == TypeKind.Interface)
                {
                    var puller = ContextDocument.Project.LanguageServices.GetService<AbstractInterfacePullerWithDialog>();
                    var operation = new ApplyChangesOperation(
                        await puller.ComputeChangedSolution(result, ContextDocument, cancellationToken));
                    return new CodeActionOperation[] { operation };
                }
                else if (result.Target.TypeKind == TypeKind.Class)
                {
                    var puller = ContextDocument.Project.LanguageServices.GetService<AbstractClassPullerWithDialog>();
                    var operation = new ApplyChangesOperation(
                        await puller.ComputeChangedSolution(result, ContextDocument, cancellationToken));
                    return new CodeActionOperation[] { operation };
                }
                else
                {
                    throw new ArgumentException($"{nameof(result.Target)} should be interface or class");
                }
            }
            else
            {
                return new CodeActionOperation[0];
            }
        }
    }
}
