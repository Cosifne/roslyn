using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;

namespace Microsoft.CodeAnalysis.Editor.Commanding.Commands
{
    [ExcludeFromCodeCoverage]
    internal class SmartBreakLineCommandArgs : EditorCommandArgs
    {
        public SmartBreakLineCommandArgs(ITextView textView, ITextBuffer subjectBuffer) : base(textView, subjectBuffer)
        {
        }
    }
}
