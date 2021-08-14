// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin
{
    [Export(typeof(ICommandHandler))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Name(PredefinedCommandHandlerNames.ExpandInheritanceMargin)]
    internal class InheritanceMarginCommandHandler : ICommandHandler<InheritanceMarginCommandArgs>
    {
        private readonly IViewTagAggregatorFactoryService _aggregatorFactoryService;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InheritanceMarginCommandHandler(IViewTagAggregatorFactoryService tagAggregatorFactoryService)
        {
            _aggregatorFactoryService = tagAggregatorFactoryService;
        }

        public string DisplayName => ServicesVSResources.Expand_inheritance_margin;

        public bool ExecuteCommand(InheritanceMarginCommandArgs args, CommandExecutionContext executionContext)
        {
            var textView = args.TextView;
            var textBuffer = args.SubjectBuffer;
            var caret = textView.GetCaretPoint(textBuffer);
            if (caret == null)
            {
                return false;
            }

            var caretLine = textView.TextViewLines.GetTextViewLineContainingBufferPosition(caret.Value);
            if (caretLine == null)
            {
                return false;
            }

            var tag = GetInheritanceMarginTag(textView, caretLine);
            if (tag == null)
            {
                return false;
            }

            var wpfTextViewHost = GetMarginTextViewHost();
            if (wpfTextViewHost == null)
            {
                return false;
            }

            var glyphMargin = wpfTextViewHost.GetTextViewMargin(PredefinedMarginNames.Glyph);
            if (glyphMargin == null)
            {
                return false;
            }

            var marginGrid = glyphMargin.VisualElement;
            var xAxisPosition = marginGrid.ActualWidth / 2 + textView.ViewportLeft;
            var yAxisPosition = textView.ViewportTop + (caretLine.TextTop + caretLine.TextBottom) / 2;
            // TODO: this should be the real context menu
            var contextMenu = new InheritanceMarginContextMenu();
            contextMenu.ItemContainerTemplateSelector = new MenuItemContainerTemplateSelector();
            var vm = new InheritanceMarginContextMenuViewModel(tag);
            contextMenu.DataContext = vm;
            contextMenu.PlacementTarget = marginGrid;
            contextMenu.Placement = PlacementMode.RelativePoint;
            contextMenu.HorizontalOffset = xAxisPosition;
            contextMenu.VerticalOffset = yAxisPosition;
            contextMenu.IsOpen = true;

            return true;
        }

        public CommandState GetCommandState(InheritanceMarginCommandArgs args)
            => CommandState.Available;

        public static void ShowContextMenu(ITextView textView, ITextViewLine caretLine, InheritanceMarginTag tag, FrameworkElement marginGrid)
        {
            var contextMenu = new InheritanceMarginContextMenu();
            contextMenu.ItemContainerTemplateSelector = new MenuItemContainerTemplateSelector();
            var vm = new InheritanceMarginContextMenuViewModel(tag);
            var xAxisPosition = marginGrid.ActualWidth / 2 + textView.ViewportLeft;
            var yAxisPosition = textView.ViewportTop + (caretLine.TextTop + caretLine.TextBottom) / 2;
            contextMenu.DataContext = vm;
            contextMenu.PlacementTarget = marginGrid;
            contextMenu.Placement = PlacementMode.RelativePoint;
            contextMenu.HorizontalOffset = xAxisPosition;
            contextMenu.VerticalOffset = yAxisPosition;
            contextMenu.IsOpen = true;
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

        private static IWpfTextViewHost? GetMarginTextViewHost()
        {
            var vsTextManager = (IVsTextManager)Shell.Package.GetGlobalService(typeof(SVsTextManager));
            var getActiveViewResult = vsTextManager.GetActiveView(fMustHaveFocus: 1, null, out var vsTestView);
            if (getActiveViewResult == VSConstants.S_OK && vsTestView is IVsUserData vsUserData)
            {
                var getDataResult = vsUserData.GetData(DefGuidList.guidIWpfTextViewHost, out var pvtData);
                if (getDataResult == VSConstants.S_OK && pvtData is IWpfTextViewHost wpfTextViewHost)
                {
                    return wpfTextViewHost;
                }
            }

            return null;
        }
    }
}
