// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler
{
    private readonly IThreadingContext _threadingContext;
    private readonly InlineRenameService _renameService;
    private readonly IAsynchronousOperationListener _listener;

    protected AbstractRenameCommandHandler(
        IThreadingContext threadingContext,
        InlineRenameService renameService,
        IAsynchronousOperationListenerProvider asynchronousOperationListenerProvider)
    {
        _threadingContext = threadingContext;
        _renameService = renameService;
        _listener = asynchronousOperationListenerProvider.GetListener(FeatureAttribute.Rename);
    }

    public string DisplayName => EditorFeaturesResources.Rename;

    protected abstract bool AdornmentShouldReceiveKeyboardNavigation(ITextView textView);

    protected abstract void SetFocusToTextView(ITextView textView);

    protected abstract void SetFocusToAdornment(ITextView textView);

    protected abstract void SetAdornmentFocusToPreviousElement(ITextView textView);

    protected abstract void SetAdornmentFocusToNextElement(ITextView textView);

    private CommandState GetCommandState(Func<CommandState> nextHandler)
    {
        if (_renameService.ActiveSession != null)
        {
            return CommandState.Available;
        }

        return nextHandler();
    }

    private CommandState GetCommandState()
        => _renameService.ActiveSession != null ? CommandState.Available : CommandState.Unspecified;

    private async Task HandlePossibleTypingCommandAsync<TArgs>(TArgs args, Action nextHandler, Func<InlineRenameSession, SnapshotSpan, Task> actionIfInsideActiveSpan)
        where TArgs : EditorCommandArgs
    {
        if (_renameService.ActiveSession == null)
        {
            nextHandler();
            return;
        }

        var selectedSpans = args.TextView.Selection.GetSnapshotSpansOnBuffer(args.SubjectBuffer);

        if (selectedSpans.Count > 1)
        {
            // If we have multiple spans active, then that means we have something like box
            // selection going on. In this case, we'll just forward along.
            nextHandler();
            return;
        }

        var singleSpan = selectedSpans.Single();
        if (_renameService.ActiveSession.TryGetContainingEditableSpan(singleSpan.Start, out var containingSpan) &&
            containingSpan.Contains(singleSpan))
        {
            await actionIfInsideActiveSpan(_renameService.ActiveSession, containingSpan).ConfigureAwait(false);
        }
        else if (_renameService.ActiveSession.IsInOpenTextBuffer(singleSpan.Start))
        {
            // It's in a read-only area that is open, so let's commit the rename 
            // and then let the character go through

            await CommitIfActiveAndCallNextHandlerAsync(args, nextHandler).ConfigureAwait(false);
        }
        else
        {
            nextHandler();
            return;
        }
    }

    private async Task CommitIfActiveAsync(EditorCommandArgs args)
    {
        if (_renameService.ActiveSession != null)
        {
            var selection = args.TextView.Selection.VirtualSelectedSpans.First();

            await _renameService.ActiveSession.CommitAsync(previewChanges: false).ConfigureAwait(false);

            var translatedSelection = selection.TranslateTo(args.TextView.TextBuffer.CurrentSnapshot);
            args.TextView.Selection.Select(translatedSelection.Start, translatedSelection.End);
            args.TextView.Caret.MoveTo(translatedSelection.End);
        }
    }

    private async Task CommitIfActiveAndCallNextHandlerAsync(EditorCommandArgs args, Action nextHandler)
    {
        await CommitIfActiveAsync(args).ConfigureAwait(false);
        nextHandler();
    }
}
