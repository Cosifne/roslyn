// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Editor.Commanding;
using Microsoft.VisualStudio.LanguageServices;

namespace Microsoft.VisualStudio.Editor.Implementation
{
    internal sealed class CommandBindings
    {
        [Export]
        [CommandBinding(Guids.RoslynGroupIdString, ID.RoslynCommands.GoToImplementation, typeof(GoToImplementationCommandArgs))]
        internal CommandBindingDefinition gotoImplementationCommandBinding;

        [Export]
        [CommandBinding(Guids.CSharpGroupIdString, ID.CSharpCommands.OrganizeSortUsings, typeof(SortImportsCommandArgs))]
        internal CommandBindingDefinition organizeSortCommandBinding;

        [Export]
        [CommandBinding(Guids.CSharpGroupIdString, ID.CSharpCommands.OrganizeRemoveAndSort, typeof(SortAndRemoveUnnecessaryImportsCommandArgs))]
        internal CommandBindingDefinition organizeRemoveAndSortCommandBinding;

        [Export]
        [CommandBinding(Guids.CSharpGroupIdString, ID.CSharpCommands.ContextOrganizeRemoveAndSort, typeof(SortAndRemoveUnnecessaryImportsCommandArgs))]
        internal CommandBindingDefinition contextOrganizeRemoveAndSortCommandBinding;

        [Export] [CommandBinding("4C7763BF-5FAF-4264-A366-B7E1F27BA958", (int)VSConstants.VSStd14CmdID.SmartBreakLine, typeof(SmartBreakLineCommandArgs))]
        internal CommandBindingDefinition smartBreakLineCommandBinding;
    }
}
