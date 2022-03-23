// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.VisualStudio.LanguageServices.InheritanceMargin
{
    internal class InheritanceMarginGlyphTagger : ITagger<InheritanceMarginGlyphTag>
    {
        public InheritanceMarginGlyphTagger()
        {

        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<InheritanceMarginGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            throw new NotImplementedException();
        }
    }
}
