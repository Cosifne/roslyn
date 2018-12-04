﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp;
using Microsoft.CodeAnalysis.PullMemberUp;
using Microsoft.VisualStudio.LanguageServices.Implementation.Utilities;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp.MainDialog
{
    internal class PullMemberUpViewModel : AbstractNotifyPropertyChanged
    {
        public ImmutableArray<PullUpMemberSymbolViewModel> Members { get; set; }

        public ImmutableArray<BaseTypeTreeNodeViewModel> Destinations { get; set; }

        private BaseTypeTreeNodeViewModel _selectedTarget;

        public BaseTypeTreeNodeViewModel SelectedTarget { get => _selectedTarget; set => SetProperty(ref _selectedTarget, value, nameof(SelectedTarget)); }

        public ImmutableDictionary<ISymbol, AsyncLazy<ImmutableArray<ISymbol>>> DependentsMap;

        public ImmutableDictionary<ISymbol, PullUpMemberSymbolViewModel> SymbolToMemberViewMap { get; }

        private bool _selectAllAndDeselectAllChecked;

        public bool SelectAllAndDeselectAllChecked
        {
            get => _selectAllAndDeselectAllChecked;
            set
            {
                _selectAllAndDeselectAllChecked = value;
                NotifyPropertyChanged($"{nameof(SelectAllAndDeselectAllChecked)}");
            }
        }

        internal PullMemberUpViewModel(
            ImmutableArray<BaseTypeTreeNodeViewModel> destinations,
            ImmutableArray<PullUpMemberSymbolViewModel> members,
            ImmutableDictionary<ISymbol, AsyncLazy<ImmutableArray<ISymbol>>> dependentsMap)
        {
            Destinations = destinations;
            DependentsMap = dependentsMap;
            Members = members;
            SymbolToMemberViewMap = members.ToImmutableDictionary(memberViewModel => memberViewModel.MemberSymbol);
        }

        internal PullMembersUpAnalysisResult CreateAnaysisResult()
        {
            // Check box won't be cleared when it is disabled. It is made to prevent user
            // loses their choice when moves around the target type
            var membersInfo = Members.
                WhereAsArray(memberSymbolView => memberSymbolView.IsChecked && memberSymbolView.IsCheckable).
                SelectAsArray(memberSymbolView =>
                    (memberSymbolView.MemberSymbol,
                    memberSymbolView.MakeAbstract &&
                    memberSymbolView.IsMakeAbstractCheckable));
            // TODO, change the result to accept make abstract parameters
            var result = PullMembersUpAnalysisBuilder.BuildAnalysisResult(
                SelectedTarget.MemberSymbol as INamedTypeSymbol,
                membersInfo);
            return result;
        }
    }
}
