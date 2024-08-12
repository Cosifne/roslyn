// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler : ICommandHandler<SaveCommandArgs>
{
    public CommandState GetCommandState(SaveCommandArgs args)
        => GetCommandState();

    public bool ExecuteCommand(SaveCommandArgs args, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(SaveCommandArgs)));
        if (_renameService.ActiveSession != null)
        {
            _ = _renameService.ActiveSession.CommitAsync(previewChanges: false, context.OperationContext.UserCancellationToken).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
            SetFocusToTextView(args.TextView);
        }

        return false;
    }
}
