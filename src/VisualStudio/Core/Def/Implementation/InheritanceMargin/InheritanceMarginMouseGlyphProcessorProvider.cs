// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin
{
    [Export(typeof(IGlyphMouseProcessorProvider))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Order(After = "BreakpointGlyphMouseProcessorProvider")]
    [Name(nameof(InheritanceMarginMouseGlyphProcessorProvider))]
    internal class InheritanceMarginMouseGlyphProcessorProvider : IGlyphMouseProcessorProvider
    {
        private readonly IViewTagAggregatorFactoryService _aggregatorFactoryService;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InheritanceMarginMouseGlyphProcessorProvider(IViewTagAggregatorFactoryService viewTagAggregatorFactoryService)
        {
            _aggregatorFactoryService = viewTagAggregatorFactoryService;
        }

        public IMouseProcessor GetAssociatedMouseProcessor(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin margin)
        {
            return new InheritanceMarginMouseProcessor(wpfTextViewHost, margin, _aggregatorFactoryService);
        }
    }
}
