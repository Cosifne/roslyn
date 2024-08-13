// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler :
    ICommandHandler<MoveSelectedLinesUpCommandArgs>, ICommandHandler<MoveSelectedLinesDownCommandArgs>
{
    public CommandState GetCommandState(MoveSelectedLinesUpCommandArgs args)
        => CommandState.Unspecified;

    public bool ExecuteCommand(MoveSelectedLinesUpCommandArgs args, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(MoveSelectedLinesUpCommandArgs)));
        CommitIfActiveAsync(args).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
        return false;
    }

    public CommandState GetCommandState(MoveSelectedLinesDownCommandArgs args)
        => CommandState.Unspecified;

    public bool ExecuteCommand(MoveSelectedLinesDownCommandArgs args, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(MoveSelectedLinesDownCommandArgs)));
        CommitIfActiveAsync(args).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
        return false;
    }
}
