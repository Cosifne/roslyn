// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler : IChainedCommandHandler<BackspaceKeyCommandArgs>, IChainedCommandHandler<DeleteKeyCommandArgs>
{
    public CommandState GetCommandState(BackspaceKeyCommandArgs args, Func<CommandState> nextHandler)
        => GetCommandState(nextHandler);

    public CommandState GetCommandState(DeleteKeyCommandArgs args, Func<CommandState> nextHandler)
        => GetCommandState(nextHandler);

    public void ExecuteCommand(BackspaceKeyCommandArgs args, Action nextHandler, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(BackspaceKeyCommandArgs)));
        HandlePossibleTypingCommandAsync(args, nextHandler, (activeSession, span) =>
            {
                var caretPoint = args.TextView.GetCaretPoint(args.SubjectBuffer);
                if (!args.TextView.Selection.IsEmpty || caretPoint.Value != span.Start)
                {
                    nextHandler();
                }
            }, context.OperationContext.UserCancellationToken).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
    }

    public void ExecuteCommand(DeleteKeyCommandArgs args, Action nextHandler, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(DeleteKeyCommandArgs)));
        HandlePossibleTypingCommandAsync(args, nextHandler, (activeSession, span) =>
            {
                var caretPoint = args.TextView.GetCaretPoint(args.SubjectBuffer);
                if (!args.TextView.Selection.IsEmpty || caretPoint.Value != span.End)
                {
                    nextHandler();
                }
            }, context.OperationContext.UserCancellationToken).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
    }
}
