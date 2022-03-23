// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.LanguageServices.InheritanceMargin
{
    internal class InheritanceMarginGlyphTag : IGlyphTag
    {
        public readonly InheritanceMarginGlyph Glyph;

        public InheritanceMarginGlyphTag(InheritanceMarginGlyph glyph)
        {
            Glyph = glyph;
        }
    }
}
