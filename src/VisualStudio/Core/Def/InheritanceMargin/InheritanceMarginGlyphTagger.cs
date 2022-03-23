// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin;
using Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.InheritanceMargin
{
    internal class InheritanceMarginGlyphTagger : ForegroundThreadAffinitizedObject, ITagger<InheritanceMarginGlyphTag>
    {
        private readonly IThreadingContext _threadingContext;
        private readonly IStreamingFindUsagesPresenter _streamingFindUsagesPresenter;
        private readonly ClassificationTypeMap _classificationTypeMap;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IUIThreadOperationExecutor _operationExecutor;
        private readonly IWpfTextView _textView;
        private readonly IGlobalOptionService _globalOptionService;
        private readonly IAsynchronousOperationListener _listener;
        private readonly ITagAggregator<InheritanceMarginTag> _tagAggregator;

        public InheritanceMarginGlyphTagger(
            IThreadingContext threadingContext,
            IStreamingFindUsagesPresenter streamingFindUsagesPresenter,
            ClassificationTypeMap classificationTypeMap,
            IClassificationFormatMap classificationFormatMap,
            IUIThreadOperationExecutor operationExecutor,
            IWpfTextView textView,
            IGlobalOptionService globalOptionService,
            IAsynchronousOperationListener listener,
            ITagAggregator<InheritanceMarginTag> tagAggregator) : base(threadingContext)
        {
            _threadingContext = threadingContext;
            _streamingFindUsagesPresenter = streamingFindUsagesPresenter;
            _classificationTypeMap = classificationTypeMap;
            _classificationFormatMap = classificationFormatMap;
            _operationExecutor = operationExecutor;
            _textView = textView;
            _globalOptionService = globalOptionService;
            _listener = listener;
            _tagAggregator = tagAggregator;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<InheritanceMarginGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.IsEmpty())
            {
                return SpecializedCollections.EmptyEnumerable<ITagSpan<InheritanceMarginGlyphTag>>();
            }

            var tags = _tagAggregator.GetTags(spans);
            foreach (var mappingTagSpan in tags)
            {
                var tag = mappingTagSpan.Tag;
                var glyph = new InheritanceMarginGlyph(
                    _threadingContext,
                    _streamingFindUsagesPresenter,
                    _classificationTypeMap,
                    _classificationFormatMap,
                    _operationExecutor,
                    tag,
                    _textView,
                    _listener);
            }
        }
    }
}
