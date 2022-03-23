// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.InheritanceMargin
{
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(InheritanceMarginGlyphTag))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Name(nameof(InheritanceMarginGlyphTaggerProvider))]
    internal class InheritanceMarginGlyphTaggerProvider : IViewTaggerProvider
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InheritanceMarginGlyphTaggerProvider()
        {
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            throw new NotImplementedException();
        }
    }
}
