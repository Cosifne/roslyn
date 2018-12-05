// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.PullMemberUp;
using Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp.WarningDialog;
using Microsoft.VisualStudio.PlatformUI;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp.MainDialog
{
    /// <summary>
    /// Interaction logic for PullhMemberUpDialog.xaml
    /// </summary>
    internal partial class PullMemberUpDialog : DialogWindow
    {
        public string OK => ServicesVSResources.OK;

        public string Cancel => ServicesVSResources.Cancel;

        public string PullMembersUpTitle => ServicesVSResources.Pull_Members_Up;

        public string SelectMembers => ServicesVSResources.Select_members;

        public string SelectDestination => ServicesVSResources.Select_destinations;

        public string Description => ServicesVSResources.Select_destination_and_members_to_pull_up;

        public string SelectPublic => ServicesVSResources.Select_Public;

        public string SelectDependents => ServicesVSResources.Select_Dependents;

        public string Members => ServicesVSResources.Members;

        public string MakeAbstract => ServicesVSResources.Make_abstract;

        public string InterfaceCantHaveField => ServicesVSResources.Interface_cant_have_field;
            
        public string InterfaceCantHaveAbstractMember => ServicesVSResources.Interface_cant_have_abstract_member;

        public PullMemberUpViewModel ViewModel { get; }

        private bool ProceedToSelectAll { get; set; } = false;

        private readonly CancellationTokenSource _cancellationTokenSource;

        internal PullMemberUpDialog(PullMemberUpViewModel pullMemberUpViewModel, CancellationTokenSource cts)
        {
            ViewModel = pullMemberUpViewModel;
            DataContext = ViewModel;
            _cancellationTokenSource = cts;
            InitializeComponent();
            MemberSelection.SizeChanged += (s, e) =>
            {
                var memberSelectionView = ((GridView)MemberSelection.View);
                memberSelectionView.Columns[0].Width = e.NewSize.Width - memberSelectionView.Columns[1].Width - 50;
            };
        }

        private void TargetMembersContainer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (TargetMembersContainer.SelectedItem is BaseTypeTreeNodeViewModel memberGraphNode)
            {
                ViewModel.SelectedDestination = memberGraphNode;
                if (memberGraphNode.MemberSymbol is INamedTypeSymbol interfaceSymbol &&
                    interfaceSymbol.TypeKind == TypeKind.Interface)
                {
                    DisableAllFieldCheckBox();
                    DisableAllMakeAbstractBox();
                }
                else
                {
                    EnableAllFieldCheckBox();
                    EnableAllMakeAbstractBox();
                }
            }
        }

        private void DisableAllMakeAbstractBox()
        {
            foreach (var member in ViewModel.Members)
            {
                member.IsMakeAbstractCheckable = false;
            }
        }

        private void EnableAllMakeAbstractBox()
        {
            foreach (var member in ViewModel.Members)
            {
                if (member.MemberSymbol.Kind != SymbolKind.Field && !member.MemberSymbol.IsAbstract)
                {
                    member.IsMakeAbstractCheckable = true;
                }
            }
        }

        private void DisableAllFieldCheckBox()
        {
            // Check box won't be cleared when it is disabled. It is made to prevent user
            // loses their choice when moves around the target type
            foreach (var member in ViewModel.Members)
            {
                if (member.MemberSymbol.Kind == SymbolKind.Field)
                {
                    member.IsCheckable = false;
                }
            }
        }

        private void EnableAllFieldCheckBox()
        {
           foreach (var member in ViewModel.Members)
            {
                if (member.MemberSymbol.Kind == SymbolKind.Field)
                {
                    member.IsCheckable = true;
                }
            }
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            var selectedMembers = ViewModel.Members.
                Where(memberSymbolView => memberSymbolView.IsChecked && memberSymbolView.IsCheckable).
                Select(memberSymbolView => memberSymbolView.MemberSymbol);
            if (ViewModel.SelectedDestination != null && selectedMembers.Count() != 0)
            {
                var result = ViewModel.CreateAnaysisResult();
                if (result.PullUpOperationCausesError)
                {
                    DialogResult = true;
                }
                else
                {
                    if (ShowWarningDialog(result))
                    {
                        DialogResult = true;
                    }
                }
            }
        }

        private bool ShowWarningDialog(PullMembersUpAnalysisResult result)
        {
            var warningViewModel = new PullMemberUpWarningViewModel(result);
            var warningDialog = new PullMemberUpWarningDialog(warningViewModel);

            return warningDialog.ShowModal().GetValueOrDefault();
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private async void SelecDependentsButton_Click(object sender, RoutedEventArgs e)
        {
            var checkedMembers = ViewModel.Members.
                Where(member => member.IsChecked &&
                      member.MemberSymbol.Kind != SymbolKind.Field);
            
            foreach (var member in checkedMembers)
            {
                // Should we block the UI and show something to notice the user if the calculation of dependents map is not finshed?
                var dependents = await ViewModel.DependentsMap[member.MemberSymbol].GetValueAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                foreach (var symbol in dependents)
                {
                    var memberView = ViewModel.SymbolToMemberViewMap[symbol];
                    if (memberView.IsCheckable)
                    {
                        memberView.IsChecked = true;
                    }
                }
            }
        }

        private void SelectAllButton_Click()
        {
            foreach (var member in ViewModel.Members)
            {
                if (member.IsCheckable)
                {
                    member.IsChecked = true;
                }
            }
        }

        private void SelectPublic_Click(object sender, RoutedEventArgs e)
        {
            foreach (var member in ViewModel.Members)
            {
                if (member.IsCheckable && member.MemberSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    member.IsChecked = true;
                }
            }
        }

        private void DeselectedAll_Click()
        {
            foreach (var member in ViewModel.Members)
            {
                if (member.IsCheckable)
                {
                    member.IsChecked = false;
                }
            }
        }

        private void SelectAllAndDeselectedAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (ProceedToSelectAll)
            {
                SelectAllButton_Click();
            }

            ProceedToSelectAll = true;
        }

        private void SelectAllAndDeselectCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DeselectedAll_Click();
            ProceedToSelectAll = true;
        }

        private void MemberSelectionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ProceedToSelectAll = false;
            ViewModel.SelectAllAndDeselectAllChecked = true;
        }
    }

    internal class BooleanReverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
