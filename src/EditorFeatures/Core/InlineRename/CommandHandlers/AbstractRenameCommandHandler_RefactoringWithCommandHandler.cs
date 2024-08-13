// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler :
    ICommandHandler<ReorderParametersCommandArgs>,
    ICommandHandler<RemoveParametersCommandArgs>,
    ICommandHandler<ExtractInterfaceCommandArgs>,
    ICommandHandler<EncapsulateFieldCommandArgs>
{
    public CommandState GetCommandState(ReorderParametersCommandArgs args)
        => CommandState.Unspecified;

    public bool ExecuteCommand(ReorderParametersCommandArgs args, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(ReorderParametersCommandArgs)));
        CommitIfActiveAsync(args).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
        return false;
    }

    public CommandState GetCommandState(RemoveParametersCommandArgs args)
        => CommandState.Unspecified;

    public bool ExecuteCommand(RemoveParametersCommandArgs args, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(RemoveParametersCommandArgs)));
        CommitIfActiveAsync(args).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
        return false;
    }

    public CommandState GetCommandState(ExtractInterfaceCommandArgs args)
        => CommandState.Unspecified;

    public bool ExecuteCommand(ExtractInterfaceCommandArgs args, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(ExtractInterfaceCommandArgs)));
        CommitIfActiveAsync(args).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
        return false;
    }

    public CommandState GetCommandState(EncapsulateFieldCommandArgs args)
        => CommandState.Unspecified;

    public bool ExecuteCommand(EncapsulateFieldCommandArgs args, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(EncapsulateFieldCommandArgs)));
        CommitIfActiveAsync(args).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
        return false;
    }
}
