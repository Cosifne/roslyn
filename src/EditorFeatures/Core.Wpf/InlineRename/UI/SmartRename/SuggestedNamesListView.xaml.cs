// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;

namespace Microsoft.CodeAnalysis.InlineRename.UI.SmartRename
{
    /// <summary>
    /// Interaction logic for SuggestedNamesControl.xaml
    /// </summary>
    internal partial class SuggestedNamesListView : ListView
    {
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(
                name: nameof(IsExpanded),
                propertyType: typeof(bool),
                ownerType: typeof(SuggestedNamesListView),
                typeMetadata: new FrameworkPropertyMetadata(defaultValue: false));

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public SuggestedNamesListView()
        {
            InitializeComponent();
        }
    }
}
