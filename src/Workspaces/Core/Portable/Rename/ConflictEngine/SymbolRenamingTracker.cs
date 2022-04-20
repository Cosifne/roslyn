// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Rename.ConflictEngine
{
    /// <summary>
    /// Contains the mutable information when renaming a symbol.
    /// </summary>
    internal class SymbolRenamingTracker
    {
        // Contains Strings like Bar -> BarAttribute ; Property Bar -> Bar , get_Bar, set_Bar
        public List<string> PossibleNameConflicts { get; }
        public HashSet<DocumentId> DocumentsIdsToBeCheckedForConflict { get; }
        public AnnotationTable<RenameAnnotation> RenameAnnotations { get; }

        public ISet<ConflictLocationInfo> ConflictLocations { get; set; }
        public bool ReplacementTextValid { get; set; }
        public List<ProjectId>? TopologicallySortedProjects { get; set; }
        public bool DocumentOfRenameSymbolHasBeenRenamed { get; set; }

        public SymbolRenamingTracker()
        {
            PossibleNameConflicts = new List<string>();
            DocumentsIdsToBeCheckedForConflict = new HashSet<DocumentId>();
            RenameAnnotations = new AnnotationTable<RenameAnnotation>(RenameAnnotation.Kind);
            ConflictLocations = SpecializedCollections.EmptySet<ConflictLocationInfo>();
            ReplacementTextValid = true;
            DocumentOfRenameSymbolHasBeenRenamed = false;
        }
    }
}
