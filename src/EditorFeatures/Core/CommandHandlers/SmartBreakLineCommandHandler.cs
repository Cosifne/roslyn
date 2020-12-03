using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.CodeAnalysis.Editor.CommandHandlers
{
    [Export]
    internal class SmartBreakLineCommandHandler
    {
        [ImportingConstructor]
        [System.Obsolete(CodeAnalysis.Host.Mef.MefConstruction.ImportingConstructorMessage, error: true)]
        public SmartBreakLineCommandHandler()
        {
        }

        public void ExecuteCommand(ITextView textView, ITextBuffer textBuffer)
        {
            var caretPoint = textView.GetCaretPoint(textBuffer);
            if (caretPoint is null)
            {
                return;
            }

            var document = caretPoint.Value.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            var postion = caretPoint.Value.Position;
            // Do cool stuff here
        }
    }
}
