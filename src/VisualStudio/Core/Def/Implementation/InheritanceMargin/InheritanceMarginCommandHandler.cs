// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.InheritanceMargin;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
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
            if (!TryGetInheritanceMarginTag(args, out var tag))
            {
                return false;
            }

            var wpfTextViewHost = GetMarginTextViewHost();
            if (wpfTextViewHost == null)
            {
                return false;
            }

            var glyphMargin = wpfTextViewHost.GetTextViewMargin(PredefinedMarginNames.Glyph);

            return false;
        }

        public CommandState GetCommandState(InheritanceMarginCommandArgs args)
            => TryGetInheritanceMarginTag(args, out var _) ? CommandState.Available : CommandState.Unavailable;

        private IWpfTextViewHost? GetMarginTextViewHost()
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

        private bool TryGetInheritanceMarginTag(InheritanceMarginCommandArgs args, [NotNullWhen(true)] out InheritanceMarginTag? tag)
        {
            var textView = args.TextView;
            var caret = textView.GetCaretPoint(args.SubjectBuffer);
            if (caret == null)
            {
                tag = null;
                return false;
            }

            var caretLine = textView.TextViewLines.GetTextViewLineContainingBufferPosition(caret.Value);

            var tagAggregator = _aggregatorFactoryService.CreateTagAggregator<InheritanceMarginTag>(textView);
            var tags = tagAggregator.GetTags(new SnapshotSpan(caretLine.Start, length: 0)).ToImmutableArray();
            if (tags.Length != 1)
            {
                tag = null;
                return false;
            }

            tag = tags[0].Tag;
            return true;
        }
    }
}
