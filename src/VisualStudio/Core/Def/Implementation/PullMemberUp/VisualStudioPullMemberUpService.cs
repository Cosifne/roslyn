// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp.Dialog;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.PullMemberUp;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp.MainDialog;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp
{
    [ExportWorkspaceService(typeof(IPullMemberUpOptionsService), ServiceLayer.Host), Shared]
    internal class VisualStudioPullMemberUpService : IPullMemberUpOptionsService
    {
        private readonly IGlyphService _glyphService;

        [ImportingConstructor]
        public VisualStudioPullMemberUpService(IGlyphService glyphService)
        {
            _glyphService = glyphService;
        }

        public PullMembersUpAnalysisResult GetPullTargetAndMembers(
            ISymbol selectedMember)
        {
            var baseTypeRootViewModel = BaseTypeTreeNodeViewModel.CreateBaseTypeTree(selectedMember.ContainingType, _glyphService);

            ViewModel = new PullMemberUpViewModel(selectedNodeSymbol, _glyphService);
            var dialog = new PullMemberUpDialog(ViewModel);
            if (dialog.ShowModal().GetValueOrDefault())
            {
                var analysisResult = ViewModel.CreateAnaysisResult();
                return new PullMemberDialogResult(analysisResult);
            }
            else
            {
                return PullMemberDialogResult.CanceledResult;
            }
        }

        private ImmutableArray<PullUpMemberSymbolViewModel> CreateMembersViewModel(ISymbol selectedMember)
        {
            var members = selectedMember.ContainingType.GetMembers().
                WhereAsArray(member => FilterNotSupportedMembers(member)).
                SelectAsArray(member => new PullUpMemberSymbolViewModel(member, _glyphService)
                {
                    // The member user selects will be checked at the begining.
                    IsChecked = member.Equals(selectedMember),
                    MakeAbstract = false,
                    IsMakeAbstractCheckable = member.Kind != SymbolKind.Field && !member.IsAbstract,
                    IsCheckable = true
                });
        }

        private bool FilterNotSupportedMembers(ISymbol member)
        {
            // Support field, ordinary method, event and property.
            switch (member)
            {
                case IMethodSymbol methodSymbol:
                    return methodSymbol.MethodKind == MethodKind.Ordinary;
                case IFieldSymbol fieldSymbol:
                    return fieldSymbol.IsImplicitlyDeclared;
                default:
                    return member.IsKind(SymbolKind.Property) || member.IsKind(SymbolKind.Event);
            }
        }
    }
}
