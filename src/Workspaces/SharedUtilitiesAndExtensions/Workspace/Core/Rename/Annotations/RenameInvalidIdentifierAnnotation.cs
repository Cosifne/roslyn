// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Runtime.CompilerServices;

namespace Microsoft.CodeAnalysis.Rename.ConflictEngine
{
    internal class RenameInvalidIdentifierAnnotation : RenameAnnotation
    {
        public RenameInvalidIdentifierAnnotation(ISymbol symbol) : base(symbol)
        {
        }
    }
}
