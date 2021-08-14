// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph
{
    internal class InheritanceMarginContextMenuViewModel
    {
        /// <summary>
        /// ViewModels for the context menu items.
        /// </summary>
        public ImmutableArray<InheritanceMenuItemViewModel> MenuItemViewModels { get; }

        public InheritanceMarginContextMenuViewModel(InheritanceMarginTag tag)
        {
            if (tag.MembersOnLine.Length == 1)
            {
                var menuItemViewModels = InheritanceMarginHelpers
                    .CreateMenuItemViewModelsForSingleMember(tag.MembersOnLine[0].TargetItems);
                MenuItemViewModels = menuItemViewModels;
            }
            else
            {
                var menuItemViewModels = InheritanceMarginHelpers.CreateMenuItemViewModelsForMultipleMembers(tag.MembersOnLine);
                MenuItemViewModels = menuItemViewModels;
            }
        }
    }
}
