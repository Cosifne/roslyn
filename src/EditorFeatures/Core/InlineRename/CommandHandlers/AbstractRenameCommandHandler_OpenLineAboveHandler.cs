// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler : IChainedCommandHandler<OpenLineAboveCommandArgs>
{
    public CommandState GetCommandState(OpenLineAboveCommandArgs args, Func<CommandState> nextHandler)
        => GetCommandState(nextHandler);

    public void ExecuteCommand(OpenLineAboveCommandArgs args, Action nextHandler, CommandExecutionContext context)
    {
        var token = _listener.BeginAsyncOperation(string.Join(nameof(ExecuteCommand), ".", nameof(OpenLineAboveCommandArgs)));
        HandlePossibleTypingCommandAsync(args, nextHandler, async (activeSession, span) =>
        {
            await activeSession.CommitAsync(previewChanges: false).ConfigureAwait(false);
            nextHandler();
        }).ReportNonFatalErrorAsync().CompletesAsyncOperation(token);
    }
}
