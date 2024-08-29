﻿// Licensed to the .NET Foundation under one or more agreements.
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
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

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

    private void HandlePossibleTypingCommand<TArgs>(TArgs args, Action nextHandler, IUIThreadOperationContext operationContext, Action<InlineRenameSession, IUIThreadOperationContext, SnapshotSpan> actionIfInsideActiveSpan)
        where TArgs : EditorCommandArgs
    {
        if (_renameService.ActiveSession == null)
        {
            nextHandler();
            return;
        }

        // If commit in progress, don't do anything, as we don't want user change the text view
        if (_renameService.ActiveSession.IsCommitInProgress)
        {
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
            actionIfInsideActiveSpan(_renameService.ActiveSession, operationContext, containingSpan);
        }
        else if (_renameService.ActiveSession.IsInOpenTextBuffer(singleSpan.Start))
        {
            // It's in a read-only area that is open, so let's commit the rename 
            // and then let the character go through
            CommitIfActiveAndCallNextHandler(args, nextHandler, operationContext);
        }
        else
        {
            nextHandler();
            return;
        }
    }

    private void CommitIfActive(EditorCommandArgs args, IUIThreadOperationContext operationContext)
    {
        if (_renameService.ActiveSession != null)
        {
            var selection = args.TextView.Selection.VirtualSelectedSpans.First();

            CommitOnTextEditAndSaveCommand(operationContext);

            var translatedSelection = selection.TranslateTo(args.TextView.TextBuffer.CurrentSnapshot);
            args.TextView.Selection.Select(translatedSelection.Start, translatedSelection.End);
            args.TextView.Caret.MoveTo(translatedSelection.End);
        }
    }

    private void CommitIfActiveAndCallNextHandler(EditorCommandArgs args, Action nextHandler, IUIThreadOperationContext operationContext)
    {
        CommitIfActive(args, operationContext);
        nextHandler();
    }

    private void CommitOnTextEditAndSaveCommand(IUIThreadOperationContext operationContext)
    {
        RoslynDebug.AssertNotNull(_renameService.ActiveSession);
        if (_renameService.ActiveSession.IsCommitInProgress)
        {
            // When the commit is in-progress, don't sync wait for the commit.
            // This might happen when user async commit the rename using 'enter' key, then they invoke 'save command'. We don't want to block UI thread again.
            return;
        }

        // We only async commit the rename session when it is invoked by 'enter' key or from the button in UI. (Which should cover most cases)
        // For other command handler still commit the session because we don't want to regress the behavior.
        // Example: When the rename session is committed by 'SaveCommand', we want to ensure rename is committed, then save the document.
        // If it's async rename, the document would still be dirty after 'SaveCommand' is executed.
        _renameService.ActiveSession.Commit(previewChanges: false, operationContext);
    }
}
