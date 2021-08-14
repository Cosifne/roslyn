// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin
{
    internal class InheritanceMarginMouseProcessor : MouseProcessorBase
    {
        private readonly IWpfTextViewHost _textViewHost;
        private readonly IWpfTextViewMargin _textViewMargin;
        private readonly IViewTagAggregatorFactoryService _aggregatorFactoryService;

        public InheritanceMarginMouseProcessor(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin wpfTextViewMargin, IViewTagAggregatorFactoryService viewTagAggregatorFactoryService)
        {
            _textViewHost = wpfTextViewHost;
            _textViewMargin = wpfTextViewMargin;
            _aggregatorFactoryService = viewTagAggregatorFactoryService;
        }

        public override void PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var textView = _textViewHost.TextView;
            var pt = e.GetPosition(textView.VisualElement);
            pt.Y += textView.ViewportTop;
            pt.X += textView.ViewportLeft;
            var textLine = textView.TextViewLines.GetTextViewLineContainingYCoordinate(pt.Y);
            var tag = GetInheritanceMarginTag(textView, textLine);
            if (tag != null)
            {
                InheritanceMarginCommandHandler.ShowContextMenu(textView, textLine, tag, _textViewMargin.VisualElement);
            }
        }

        private InheritanceMarginTag? GetInheritanceMarginTag(ITextView view, ITextViewLine textViewLine)
        {
            var tagAggregator = _aggregatorFactoryService.CreateTagAggregator<InheritanceMarginTag>(view);
            var tags = tagAggregator.GetTags(new SnapshotSpan(textViewLine.Start, length: 0)).ToImmutableArray();
            if (tags.Length != 1)
            {
                return null;
            }

            return tags[0].Tag;
        }
    }
}
