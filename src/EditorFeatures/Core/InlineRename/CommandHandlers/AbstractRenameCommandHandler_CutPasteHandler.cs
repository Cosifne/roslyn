// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler :
    IChainedCommandHandler<CutCommandArgs>, IChainedCommandHandler<PasteCommandArgs>
{
    public CommandState GetCommandState(CutCommandArgs args, Func<CommandState> nextHandler)
        => nextHandler();

    public void ExecuteCommand(CutCommandArgs args, Action nextHandler, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(CutCommandArgs)));
        HandlePossibleTypingCommandAsync(args, nextHandler, (activeSession, span) =>
        {
            nextHandler();
        }, context.OperationContext.UserCancellationToken).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
    }

    public CommandState GetCommandState(PasteCommandArgs args, Func<CommandState> nextHandler)
        => nextHandler();

    public void ExecuteCommand(PasteCommandArgs args, Action nextHandler, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(PasteCommandArgs)));
        HandlePossibleTypingCommandAsync(args, nextHandler, (activeSession, span) =>
        {
            nextHandler();
        }, context.OperationContext.UserCancellationToken).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
    }
}
