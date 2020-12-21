// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editor.UnitTests.Utilities;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Test.Utilities.BraceCompletion
{
    [UseExportProvider]
    public abstract class AbstractBraceCompletionCommandHanderTest
    {
        protected abstract TestWorkspace CreateWorkspace(string code);

        protected abstract ICommandHandler GetCommandHandler(TestWorkspace workspace);

        protected void Test(string initialMarkup, string expectedMarkup)
        {
            using var workspace = CreateWorkspace(initialMarkup);

            var view = workspace.Documents.Single().GetTextView();
            var buffer = workspace.Documents.Single().GetTextBuffer();

            view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, workspace.Documents.Single(d => d.CursorPosition.HasValue).CursorPosition!.Value));
            var commandHandler = GetCommandHandler(workspace);
            commandHandler.ExecuteCommand(new AutomaticLineEnderCommandArgs(view, buffer), () => { }, TestCommandExecutionContext.Create());

            MarkupTestFile.GetPosition(expectedMarkup, out var expected, out int expectedPosition);

            var virtualPosition = view.Caret.Position.VirtualBufferPosition;
            expected = expected.Remove(virtualPosition.Position, virtualPosition.VirtualSpaces);

            Assert.Equal(expected, buffer.CurrentSnapshot.GetText());
            Assert.Equal(expectedPosition, virtualPosition.Position.Position + virtualPosition.VirtualSpaces);
        }
    }
}
