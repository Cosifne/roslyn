// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin
{
    internal class InheritanceMarginGlyphMouseProcessor : MouseProcessorBase
    {
        public InheritanceMarginGlyphMouseProcessor(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin margin)
        {

        }

        public override void PostprocessMouseDown(MouseButtonEventArgs e)
        {
        }
    }
}
