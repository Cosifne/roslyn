// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin
{
    [Export(typeof(IGlyphMouseProcessorProvider))]
    [Name(nameof(InheritanceMarginGlyphMouseProcessorProvider))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Order(After = "BreakpointGlyphMouseProcessorProvider")]
    internal class InheritanceMarginGlyphMouseProcessorProvider : IGlyphMouseProcessorProvider
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InheritanceMarginGlyphMouseProcessorProvider()
        {
        }

        public IMouseProcessor GetAssociatedMouseProcessor(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin margin)
        {
            return new InheritanceMarginGlyphMouseProcessor(wpfTextViewHost, margin);
        }
    }
}
