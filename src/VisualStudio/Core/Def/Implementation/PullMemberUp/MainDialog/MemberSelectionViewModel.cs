// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.VisualStudio.Language.Intellisense;
using static Microsoft.VisualStudio.LanguageServices.Implementation.ExtractInterface.ExtractInterfaceDialogViewModel;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp.MainDialog
{
    internal class PullUpMemberSymbolViewModel : MemberSymbolViewModel
    {
        /// <summary>
        /// Property controls the 'Make abstract' check box's Visibility.
        /// The check box is hidden for members impossbile to be made to abstract.
        /// </summary>
        public Visibility MakeAbstractVisibility => MemberSymbol.Kind == SymbolKind.Field || MemberSymbol.IsAbstract ? Visibility.Hidden : Visibility.Visible;

        /// <summary>
        /// Property indicates whether 'Make abstract' check box is checked.
        /// </summary>
        public bool MakeAbstract { get; set; }

        private bool _isCheckable;

        /// <summary>
        /// Property indicates whether this member checkable.
        /// </summary>
        public bool IsCheckable { get => _isCheckable; set => SetProperty(ref _isCheckable, value, nameof(IsCheckable)); }

        /// <summary>
        /// The content of the tooltip.
        /// </summary>
        public string Accessibility => MemberSymbol.DeclaredAccessibility.ToString();

        private Visibility _spinnerVisibility = Visibility.Hidden;

        /// <summary>
        /// Property controls the find dependents spinner.
        /// </summary>
        public Visibility SpinnerVisibility { get => _spinnerVisibility; set => SetProperty(ref _spinnerVisibility, value); }

        public PullUpMemberSymbolViewModel(ISymbol symbol, IGlyphService glyphService) : base(symbol, glyphService)
        {
        }
    }
}
