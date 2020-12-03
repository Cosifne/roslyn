using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CommandHandlers
{
    [Export(typeof(ICommandHandler))]
    [ContentType(ContentTypeNames.CSharpContentType)]
    [Name(nameof(SmartBreakLineCommandHandler))]
    [Order(After = PredefinedCompletionNames.CompletionCommandHandler)]
    internal class SmartBreakLineCommandHandler : ICommandHandler<SmartBreakLineCommandArgs>
    {
        public string DisplayName { get; }

        public CommandState GetCommandState(SmartBreakLineCommandArgs args)
        {
            // Do Cool stuff
            return CommandState.Available;
        }

        public bool ExecuteCommand(SmartBreakLineCommandArgs args, CommandExecutionContext executionContext)
        {
            // Do Cool stuff
            return true;
        }
    }
}
