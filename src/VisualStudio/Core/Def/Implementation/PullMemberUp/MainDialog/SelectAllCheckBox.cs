// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System.Windows.Controls;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp.MainDialog
{
    /// <summary>
    /// A check box used to imitate the behavior of select all check box of VS.
    /// It reverses the three state of the state of check box to null -> true -> false
    /// </summary>
    internal class SelectAllCheckBox : CheckBox
    {
        protected override void OnToggle()
        {
            if (IsChecked == false)
            {
                IsChecked = IsThreeState ? null : true as bool?;
            }
            else
            {
                IsChecked = new bool?(!IsChecked.HasValue);  
            }
        }
    }
}
