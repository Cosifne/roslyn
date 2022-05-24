// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeCleanup;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Rename.ConflictEngine
{
    internal static partial class ConflictResolver
    {
        /// <summary>
        /// Helper class to track the state necessary for finding/resolving conflicts in a 
        /// rename session.
        /// </summary>
        private class Session
        {
            private readonly Solution _baseSolution;
            private readonly ImmutableArray<SymbolSession> _symbolSessions;
            private readonly ImmutableDictionary<DocumentId, HashSet<SymbolSession>> _documentIdToAffectSymbolSessions;
            private readonly ImmutableDictionary<ProjectId, HashSet<SymbolSession>> _projectIdToAffectSymbolSessions;

            // Rename Symbol's Source Location
            private readonly CancellationToken _cancellationToken;
            private readonly AnnotationTable<RenameAnnotation> _renameAnnotations;

            private readonly RenamedSpansTracker _renamedSpansTracker;
            private readonly ImmutableArray<ProjectId> _topologicallySortedProjects;
            private readonly CodeCleanupOptionsProvider _fallbackOptions;
            private ISet<ConflictLocationInfo> _conflictLocations;

            private Session(
                Solution solution,
                ImmutableArray<ProjectId> topologicallySortedProjects,
                ImmutableArray<SymbolSession> symbolSessions,
                ImmutableDictionary<DocumentId, HashSet<SymbolSession>> documentIdToAffectSymbolSessions,
                ImmutableDictionary<ProjectId, HashSet<SymbolSession>> projectIdToAffectSymbolSessions,
                RenamedSpansTracker renamedSpansTracker,
                CodeCleanupOptionsProvider fallbackOptions,
                CancellationToken cancellationToken)
            {
                _baseSolution = solution;
                _topologicallySortedProjects = topologicallySortedProjects;
                _symbolSessions = symbolSessions;
                _documentIdToAffectSymbolSessions = documentIdToAffectSymbolSessions;
                _projectIdToAffectSymbolSessions = projectIdToAffectSymbolSessions;
                _fallbackOptions = fallbackOptions;
                _conflictLocations = SpecializedCollections.EmptySet<ConflictLocationInfo>();
                _renameAnnotations = new AnnotationTable<RenameAnnotation>(RenameAnnotation.Kind);
                _renamedSpansTracker = renamedSpansTracker;
                _cancellationToken = cancellationToken;
            }

            public static async Task<Session> CreateAsync(
                Solution solution,
                ImmutableDictionary<ISymbol, RenameSymbolInfo> renameSymbolsInfo,
                CodeCleanupOptionsProvider fallbackOptions,
                CancellationToken cancellationToken)
            {
                var dependencyGraph = solution.GetProjectDependencyGraph();
                var topologicallySortedProjects = dependencyGraph.GetTopologicallySortedProjects(cancellationToken).ToImmutableArray();
                var symbolSessions = await CreateSymbolSessionsAsync(solution, renameSymbolsInfo, cancellationToken).ConfigureAwait(false);

                // Create a map from each documentId in documentsIdNeedsToBeCheckForConflict to the affect symbol sessions.
                // So later when rename & check conflict for a document, we know the symbols need to be renamed within this document.
                var documentIdToAffectSymbolSessions = GroupSymbolSessionsByDocumentsId(symbolSessions);
                var projectIdToAffectSymbolSessions = GroupSymbolSessionsByProjectsId(symbolSessions);
                var renamedSpansTracker = new RenamedSpansTracker(GetAreAllReplacementTextsValidInDocument(documentIdToAffectSymbolSessions));
                return new Session(
                    solution,
                    topologicallySortedProjects,
                    symbolSessions,
                    documentIdToAffectSymbolSessions,
                    projectIdToAffectSymbolSessions,
                    renamedSpansTracker,
                    fallbackOptions,
                    cancellationToken);
            }

            private static async Task<ImmutableArray<SymbolSession>> CreateSymbolSessionsAsync(
                Solution solution,
                ImmutableDictionary<ISymbol, RenameSymbolInfo> renameSymbolsInfo,
                CancellationToken cancellationToken)
            {
                using var _ = ArrayBuilder<SymbolSession>.GetInstance(out var symbolSesssionBuilder);
                foreach (var (_, symbolInfo) in renameSymbolsInfo)
                {
                    var renameLocationSet = symbolInfo.RenameLocations;
                    var renameSymbolDeclarationLocation = symbolInfo.RenameLocations.Symbol.Locations.Where(loc => loc.IsInSource).FirstOrDefault();
                    var originalText = symbolInfo.RenameLocations.Symbol.Name;
                    var replacementText = symbolInfo.ReplacementText;

                    // only process documents which possibly contain the identifiers.
                    var documentIdOfRenameSymbolDeclaration = solution.GetRequiredDocument(renameSymbolDeclarationLocation.SourceTree!).Id;

                    using var _1 = PooledHashSet<DocumentId>.GetInstance(out var documentsIdsToBeCheckedForConflictBuilder);

                    var possibleNameConflicts = new List<string>();
                    await FindDocumentsAndPossibleNameConflictsAsync(
                        solution,
                        renameLocationSet,
                        originalText,
                        replacementText,
                        documentsIdsToBeCheckedForConflictBuilder,
                        possibleNameConflicts,
                        cancellationToken).ConfigureAwait(false);
                    var replacementTextValid = IsIdentifierValid_Worker(solution, replacementText, documentsIdsToBeCheckedForConflictBuilder.Select(doc => doc.ProjectId));

                    symbolSesssionBuilder.Add(new SymbolSession(
                        renameLocationSet,
                        renameSymbolDeclarationLocation,
                        documentIdOfRenameSymbolDeclaration,
                        originalText,
                        replacementText,
                        symbolInfo.NonConflictSymbols,
                        new(),
                        possibleNameConflicts.ToImmutableArray(),
                        documentsIdsToBeCheckedForConflict: documentsIdsToBeCheckedForConflictBuilder.ToImmutableHashSet(),
                        replacementTextValid));
                }

                return symbolSesssionBuilder.ToImmutableArray();
            }

            private static ImmutableDictionary<DocumentId, HashSet<SymbolSession>> GroupSymbolSessionsByDocumentsId(ImmutableArray<SymbolSession> symbolSessions)
            {
                using var _ = PooledDictionary<DocumentId, HashSet<SymbolSession>>.GetInstance(out var builder);
                foreach (var session in symbolSessions)
                {
                    foreach (var documentId in session.DocumentsIdsToBeCheckedForConflict)
                    {
                        if (builder.TryGetValue(documentId, out var sessionSet))
                        {
                            sessionSet.Add(session);
                        }
                        else
                        {
                            builder[documentId] = new HashSet<SymbolSession>() { session };
                        }
                    }
                }

                return builder.ToImmutableDictionary();
            }

            private static ImmutableDictionary<ProjectId, HashSet<SymbolSession>> GroupSymbolSessionsByProjectsId(ImmutableArray<SymbolSession> symbolSessions)
            {
                using var _ = PooledDictionary<ProjectId, HashSet<SymbolSession>>.GetInstance(out var builder);
                foreach (var session in symbolSessions)
                {
                    foreach (var documentId in session.DocumentsIdsToBeCheckedForConflict)
                    {
                        var projectId = documentId.ProjectId;
                        if (builder.TryGetValue(projectId, out var sessionSet))
                        {
                            sessionSet.Add(session);
                        }
                        else
                        {
                            builder[projectId] = new HashSet<SymbolSession>() { session };
                        }
                    }
                }

                return builder.ToImmutableDictionary();
            }

            private static ImmutableDictionary<DocumentId, bool> GetAreAllReplacementTextsValidInDocument(ImmutableDictionary<DocumentId, HashSet<SymbolSession>> documentToSessions)
            {
                using var _ = PooledDictionary<DocumentId, bool>.GetInstance(out var builder);
                foreach (var (documentId, sessions) in documentToSessions)
                {
                    builder[documentId] = sessions.All(s => s.ReplacementTextValid);
                }

                return builder.ToImmutableDictionary();
            }

            private readonly struct ConflictLocationInfo
            {
                // The span of the Node that needs to be complexified 
                public readonly TextSpan ComplexifiedSpan;
                public readonly DocumentId DocumentId;

                // The identifier span that needs to be checked for conflict
                public readonly TextSpan OriginalIdentifierSpan;

                public ConflictLocationInfo(RelatedLocation location)
                {
                    Debug.Assert(location.ComplexifiedTargetSpan.Contains(location.ConflictCheckSpan) || location.Type == RelatedLocationType.UnresolvableConflict);
                    this.ComplexifiedSpan = location.ComplexifiedTargetSpan;
                    this.DocumentId = location.DocumentId;
                    this.OriginalIdentifierSpan = location.ConflictCheckSpan;
                }
            }

            private class SymbolSession
            {
                // Set of All Locations that will be renamed (does not include non-reference locations that need to be checked for conflicts)
                public readonly RenameLocations RenameLocationSet;

                // Rename Symbol's Source Location
                public readonly Location RenameSymbolDeclarationLocation;
                public readonly DocumentId DocumentIdOfRenameSymbolDeclaration;
                public readonly string OriginalText;
                public readonly string ReplacementText;
                public readonly ImmutableHashSet<ISymbol>? NonConflictSymbols;

                public readonly RenameAnnotation RenamedSymbolDeclarationAnnotation = new();

                // Contains Strings like Bar -> BarAttribute ; Property Bar -> Bar , get_Bar, set_Bar
                public readonly ImmutableArray<string> PossibleNameConflicts = new();
                public readonly ImmutableHashSet<DocumentId> DocumentsIdsToBeCheckedForConflict;

                public readonly bool ReplacementTextValid;
                public bool DocumentOfRenameSymbolHasBeenRenamed { get; set; } = false;

                public SymbolRenameOptions RenameOptions => RenameLocationSet.Options;

                public SymbolSession(
                    RenameLocations renameLocationSet,
                    Location renameSymbolDeclarationLocation,
                    DocumentId documentIdOfRenameSymbolDeclaration,
                    string originalText,
                    string replacementText,
                    ImmutableHashSet<ISymbol>? nonConflictSymbols,
                    RenameAnnotation renamedSymbolDeclarationAnnotation,
                    ImmutableArray<string> possibleNameConflicts,
                    ImmutableHashSet<DocumentId> documentsIdsToBeCheckedForConflict,
                    bool replacementTextValid)
                {
                    RenameLocationSet = renameLocationSet;
                    RenameSymbolDeclarationLocation = renameSymbolDeclarationLocation;
                    DocumentIdOfRenameSymbolDeclaration = documentIdOfRenameSymbolDeclaration;
                    OriginalText = originalText;
                    ReplacementText = replacementText;
                    NonConflictSymbols = nonConflictSymbols;
                    RenamedSymbolDeclarationAnnotation = renamedSymbolDeclarationAnnotation;
                    PossibleNameConflicts = possibleNameConflicts;
                    DocumentsIdsToBeCheckedForConflict = documentsIdsToBeCheckedForConflict;
                    ReplacementTextValid = replacementTextValid;
                }

                public RenameRewriterSymbolParameters ToRenameRewriterParametersForDocument(DocumentId documentId)
                {
                    // TODO: Logic could be simplified here.
                    //Get all rename locations for the current document.
                    var renameLocations = RenameLocationSet.Locations;
                    var allTextSpansInSingleSourceTree = renameLocations
                        .Where(l => l.DocumentId == documentId && ShouldIncludeLocation(renameLocations, l))
                        .ToDictionary(l => l.Location.SourceSpan);

                    // All textspan in the document documentId, that requires rename in String or Comment
                    var stringAndCommentTextSpansInSingleSourceTree = renameLocations
                        .Where(l => l.DocumentId == documentId && l.IsRenameInStringOrComment)
                        .GroupBy(l => l.ContainingLocationForStringOrComment)
                        .ToImmutableDictionary(
                            g => g.Key,
                            g => GetSubSpansToRenameInStringAndCommentTextSpans(g.Key, g));

                    using var _ = PooledHashSet<TextSpan>.GetInstance(out var relatedSpansBuilder);
                    relatedSpansBuilder.AddRange(allTextSpansInSingleSourceTree.Keys);
                    relatedSpansBuilder.AddRange(stringAndCommentTextSpansInSingleSourceTree.Keys);

                    foreach (var renameLocation in renameLocations)
                    {
                        if (renameLocation.DocumentId == documentId)
                        {
                            relatedSpansBuilder.Add(renameLocation.Location.SourceSpan);
                        }
                    }

                    return new RenameRewriterSymbolParameters(
                        IsRenamingInStrings: RenameOptions.RenameInStrings,
                        IsRenamingInComments: RenameOptions.RenameInComments,
                        OriginalText,
                        PossibleNameConflicts,
                        RenamedSymbolDeclarationAnnotation,
                        allTextSpansInSingleSourceTree,
                        RenameLocationSet.Symbol,
                        ReplacementText,
                        ReplacementTextValid,
                        stringAndCommentTextSpansInSingleSourceTree,
                        relatedSpansBuilder.ToImmutableHashSet());
                }
            }

            // The method which performs rename, resolves the conflict locations and returns the result of the rename operation
            public async Task<MutableConflictResolution> ResolveConflictsAsync()
            {
                try
                {
                    // Process rename one project at a time to improve caching and reduce syntax tree serialization.
                    var documentsGroupedByTopologicallySortedProjectId = _symbolSessions.SelectManyAsArray(session => session.DocumentsIdsToBeCheckedForConflict)
                        .GroupBy(d => d.ProjectId)
                        .OrderBy(g => _topologicallySortedProjects.IndexOf(g.Key));

                    var conflictResolution = new MutableConflictResolution(
                        _baseSolution,
                        _renamedSpansTracker,
                        symbolToIsReplacementTextValid: _symbolSessions.ToImmutableDictionary(
                            keySelector: session => session.RenameLocationSet.Symbol,
                            elementSelector: session => session.ReplacementTextValid));

                    var intermediateSolution = conflictResolution.OldSolution;
                    foreach (var documentsByProject in documentsGroupedByTopologicallySortedProjectId)
                    {
                        var documentIdsThatGetsAnnotatedAndRenamed = new HashSet<DocumentId>(documentsByProject);
                        using (_baseSolution.Services.CacheService?.EnableCaching(documentsByProject.Key))
                        {
                            // Rename is going to be in 5 phases.
                            // 1st phase - Does a simple token replacement
                            // If the 1st phase results in conflict then we perform then:
                            //      2nd phase is to expand and simplify only the reference locations with conflicts
                            //      3rd phase is to expand and simplify all the conflict locations (both reference and non-reference)
                            // If there are unresolved Conflicts after the 3rd phase then in 4th phase, 
                            //      We complexify and resolve locations that were resolvable and for the other locations we perform the normal token replacement like the first the phase.
                            // If the OptionSet has RenameFile to true, we rename files with the type declaration
                            for (var phase = 0; phase < 4; phase++)
                            {
                                // Step 1:
                                // The rename process and annotation for the bookkeeping is performed in one-step
                                // The Process in short is,
                                // 1. If renaming a token which is no conflict then replace the token and make a map of the oldspan to the newspan
                                // 2. If we encounter a node that has to be expanded( because there was a conflict in previous phase), we expand it.
                                //    If the node happens to contain a token that needs to be renamed then we annotate it and rename it after expansion else just expand and proceed
                                // 3. Through the whole process we maintain a map of the oldspan to newspan. In case of expansion & rename, we map the expanded node and the renamed token
                                conflictResolution.UpdateCurrentSolution(await AnnotateAndRename_WorkerAsync(
                                    _baseSolution,
                                    conflictResolution.CurrentSolution,
                                    documentIdsThatGetsAnnotatedAndRenamed,
                                    _renamedSpansTracker).ConfigureAwait(false));

                                // Step 2: Check for conflicts in the renamed solution
                                var foundResolvableConflicts = await IdentifyConflictsAsync(
                                    documentIdsForConflictResolution: documentIdsThatGetsAnnotatedAndRenamed,
                                    allDocumentIdsInProject: documentsByProject,
                                    projectId: documentsByProject.Key,
                                    conflictResolution: conflictResolution).ConfigureAwait(false);

                                if (!foundResolvableConflicts || phase == 3)
                                {
                                    break;
                                }

                                if (phase == 0)
                                {
                                    _conflictLocations = conflictResolution.RelatedLocations
                                        .Where(loc => (documentIdsThatGetsAnnotatedAndRenamed.Contains(loc.DocumentId) && loc.Type == RelatedLocationType.PossiblyResolvableConflict && loc.IsReference))
                                        .Select(loc => new ConflictLocationInfo(loc))
                                        .ToSet();

                                    // If there were no conflicting locations in references, then the first conflict phase has to be skipped.
                                    if (_conflictLocations.Count == 0)
                                    {
                                        phase++;
                                    }
                                }

                                if (phase == 1)
                                {
                                    _conflictLocations = _conflictLocations.Concat(conflictResolution.RelatedLocations
                                        .Where(loc => documentIdsThatGetsAnnotatedAndRenamed.Contains(loc.DocumentId) && loc.Type == RelatedLocationType.PossiblyResolvableConflict)
                                        .Select(loc => new ConflictLocationInfo(loc)))
                                        .ToSet();
                                }

                                // Set the documents with conflicts that need to be processed in the next phase.
                                // Note that we need to get the conflictLocations here since we're going to remove some locations below if phase == 2
                                documentIdsThatGetsAnnotatedAndRenamed = new HashSet<DocumentId>(_conflictLocations.Select(l => l.DocumentId));

                                if (phase == 2)
                                {
                                    // After phase 2, if there are still conflicts then remove the conflict locations from being expanded
                                    var unresolvedLocations = conflictResolution.RelatedLocations
                                        .Where(l => (l.Type & RelatedLocationType.UnresolvedConflict) != 0)
                                        .Select(l => Tuple.Create(l.ComplexifiedTargetSpan, l.DocumentId)).Distinct();

                                    _conflictLocations = _conflictLocations.Where(l => !unresolvedLocations.Any(c => c.Item2 == l.DocumentId && c.Item1.Contains(l.OriginalIdentifierSpan))).ToSet();
                                }

                                // Clean up side effects from rename before entering the next phase
                                conflictResolution.ClearDocuments(documentIdsThatGetsAnnotatedAndRenamed);
                            }

                            // Step 3: Simplify the project
                            conflictResolution.UpdateCurrentSolution(await _renamedSpansTracker.SimplifyAsync(
                                conflictResolution.CurrentSolution,
                                documentsByProject,
                                _renameAnnotations,
                                _fallbackOptions,
                                _cancellationToken).ConfigureAwait(false));
                            intermediateSolution = await conflictResolution.RemoveAllRenameAnnotationsAsync(
                                intermediateSolution, documentsByProject, _renameAnnotations, _cancellationToken).ConfigureAwait(false);
                            conflictResolution.UpdateCurrentSolution(intermediateSolution);
                        }
                    }

                    foreach (var symbolSession in _symbolSessions)
                    {
                        // This rename could break implicit references of this symbol (e.g. rename MoveNext on a collection like type in a 
                        // foreach/for each statement
                        var renamedSymbolInNewSolution = await GetRenamedSymbolInCurrentSolutionAsync(symbolSession, conflictResolution).ConfigureAwait(false);

                        if (IsRenameValid(symbolSession.ReplacementTextValid, renamedSymbolInNewSolution))
                        {
                            await AddImplicitConflictsAsync(
                                renamedSymbolInNewSolution,
                                symbolSession.RenameLocationSet.Symbol,
                                symbolSession.RenameLocationSet.ImplicitLocations,
                                await conflictResolution.CurrentSolution.GetRequiredDocument(symbolSession.DocumentIdOfRenameSymbolDeclaration).GetRequiredSemanticModelAsync(_cancellationToken).ConfigureAwait(false),
                                symbolSession.RenameSymbolDeclarationLocation,
                                _renamedSpansTracker.GetAdjustedPosition(symbolSession.RenameSymbolDeclarationLocation.SourceSpan.Start, symbolSession.DocumentIdOfRenameSymbolDeclaration),
                                conflictResolution,
                                _cancellationToken).ConfigureAwait(false);
                        }
                    }

                    for (var i = 0; i < conflictResolution.RelatedLocations.Count; i++)
                    {
                        var relatedLocation = conflictResolution.RelatedLocations[i];
                        if (relatedLocation.Type == RelatedLocationType.PossiblyResolvableConflict)
                            conflictResolution.RelatedLocations[i] = relatedLocation.WithType(RelatedLocationType.UnresolvedConflict);
                    }

#if DEBUG

                    foreach (var symbolSession in _symbolSessions)
                    {
                        await DebugVerifyNoErrorsAsync(conflictResolution, symbolSession).ConfigureAwait(false);
                    }
#endif

                    // Step 5: Rename declaration files
                    foreach (var symbolSession in _symbolSessions)
                    {
                        if (symbolSession.RenameOptions.RenameFile)
                        {
                            var definitionLocations = symbolSession.RenameLocationSet.Symbol.Locations;
                            var definitionDocuments = definitionLocations
                                .Select(l => conflictResolution.OldSolution.GetDocument(l.SourceTree))
                                .Distinct();

                            if (definitionDocuments.Count() == 1 && symbolSession.ReplacementTextValid)
                            {
                                // At the moment, only single document renaming is allowed
                                conflictResolution.RenameDocumentToMatchNewSymbol(
                                    symbolSession.ReplacementText,
                                    definitionDocuments.Single());
                            }
                        }
                    }

                    return conflictResolution;
                }
                catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e, ErrorSeverity.Critical))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

#if DEBUG
            private async Task DebugVerifyNoErrorsAsync(MutableConflictResolution conflictResolution, SymbolSession symbolSession)
            {
                var documentIdErrorStateLookup = new Dictionary<DocumentId, bool>();
                var documents = symbolSession.DocumentsIdsToBeCheckedForConflict;

                // we only check for the documentIds we add annotations to, which is a subset of the ones we're going 
                // to change the syntax in.
                foreach (var documentId in documents)
                {
                    // remember if there were issues in the document prior to renaming it.
                    var originalDoc = conflictResolution.OldSolution.GetRequiredDocument(documentId);
                    documentIdErrorStateLookup.Add(documentId, await originalDoc.HasAnyErrorsAsync(_cancellationToken).ConfigureAwait(false));
                }

                // We want to ignore few error message introduced by rename because the user is wantedly doing it.
                var ignoreErrorCodes = new List<string>();
                ignoreErrorCodes.Add("BC30420"); // BC30420 - Sub Main missing in VB Project
                ignoreErrorCodes.Add("CS5001"); // CS5001 - Missing Main in C# Project

                // only check if rename thinks it was successful
                var symbol = symbolSession.RenameLocationSet.Symbol;
                if (conflictResolution.SymbolToIsReplacementTextValid[symbol] && conflictResolution.RelatedLocations.All(loc => (loc.Type & RelatedLocationType.UnresolvableConflict) == 0))
                {
                    foreach (var documentId in documents)
                    {
                        // only check documents that had no errors before rename (we might have 
                        // fixed them because of rename).  Also, don't bother checking if a custom
                        // callback was provided.  The caller might be ok with a rename that introduces
                        // errors.
                        if (!documentIdErrorStateLookup[documentId] && symbolSession.NonConflictSymbols == null)
                        {
                            await conflictResolution.CurrentSolution.GetRequiredDocument(documentId).VerifyNoErrorsAsync("Rename introduced errors in error-free code", _cancellationToken, ignoreErrorCodes).ConfigureAwait(false);
                        }
                    }
                }
            }
#endif

            /// <summary>
            /// Find conflicts in the new solution 
            /// </summary>
            private async Task<bool> IdentifyConflictsAsync(
                HashSet<DocumentId> documentIdsForConflictResolution,
                IEnumerable<DocumentId> allDocumentIdsInProject,
                ProjectId projectId,
                MutableConflictResolution conflictResolution)
            {
                try
                {
                    var affectedSymbolSessions = _projectIdToAffectSymbolSessions[projectId];

                    foreach (var symbolSession in affectedSymbolSessions)
                    {
                        symbolSession.DocumentOfRenameSymbolHasBeenRenamed |= documentIdsForConflictResolution.Contains(symbolSession.DocumentIdOfRenameSymbolDeclaration);

                        // Get the renamed symbol in complexified new solution
                        var renamedSymbolInNewSolution = await GetRenamedSymbolInCurrentSolutionAsync(symbolSession, conflictResolution).ConfigureAwait(false);

                        // if the text replacement is invalid, we just did a simple token replacement.
                        // Therefore we don't need more mapping information and can skip the rest of 
                        // the loop body.
                        if (!IsRenameValid(symbolSession.ReplacementTextValid, renamedSymbolInNewSolution))
                        {
                            foreach (var documentId in documentIdsForConflictResolution)
                            {
                                var newDocument = conflictResolution.CurrentSolution.GetRequiredDocument(documentId);
                                var syntaxRoot = await newDocument.GetRequiredSyntaxRootAsync(_cancellationToken).ConfigureAwait(false);

                                var nodesOrTokensWithConflictCheckAnnotations = GetNodesOrTokensToCheckForConflicts(syntaxRoot);
                                foreach (var (syntax, annotation) in nodesOrTokensWithConflictCheckAnnotations)
                                {
                                    if (annotation.IsRenameLocation)
                                    {
                                        conflictResolution.AddRelatedLocation(new RelatedLocation(
                                            annotation.OriginalSpan, documentId, RelatedLocationType.UnresolvedConflict));
                                    }
                                }
                            }

                            return false;
                        }

                        var reverseMappedLocations = new Dictionary<Location, Location>();

                        // If we were giving any non-conflict-symbols then ensure that we know what those symbols are in
                        // the current project post after our edits so far.
                        var currentProject = conflictResolution.CurrentSolution.GetRequiredProject(projectId);
                        var nonConflictSymbols = await GetNonConflictSymbolsAsync(symbolSession, currentProject).ConfigureAwait(false);

                        foreach (var documentId in documentIdsForConflictResolution)
                        {
                            var newDocument = conflictResolution.CurrentSolution.GetRequiredDocument(documentId);
                            var syntaxRoot = await newDocument.GetRequiredSyntaxRootAsync(_cancellationToken).ConfigureAwait(false);
                            var baseDocument = conflictResolution.OldSolution.GetRequiredDocument(documentId);
                            var baseSyntaxTree = await baseDocument.GetRequiredSyntaxTreeAsync(_cancellationToken).ConfigureAwait(false);
                            var baseRoot = await baseDocument.GetRequiredSyntaxRootAsync(_cancellationToken).ConfigureAwait(false);
                            SemanticModel? newDocumentSemanticModel = null;
                            var syntaxFactsService = newDocument.Project.LanguageServices.GetRequiredService<ISyntaxFactsService>();

                            // Get all tokens that need conflict check
                            var nodesOrTokensWithConflictCheckAnnotations = GetNodesOrTokensToCheckForConflicts(syntaxRoot);

                            var complexifiedLocationSpanForThisDocument =
                                _conflictLocations
                                .Where(t => t.DocumentId == documentId)
                                .Select(t => t.OriginalIdentifierSpan).ToSet();

                            foreach (var (syntax, annotation) in nodesOrTokensWithConflictCheckAnnotations)
                            {
                                var tokenOrNode = syntax;
                                var conflictAnnotation = annotation;
                                reverseMappedLocations[tokenOrNode.GetLocation()!] = baseSyntaxTree.GetLocation(conflictAnnotation.OriginalSpan);
                                var originalLocation = conflictAnnotation.OriginalSpan;
                                ImmutableArray<ISymbol> newReferencedSymbols = default;

                                var hasConflict = _renameAnnotations.HasAnnotation(tokenOrNode, RenameInvalidIdentifierAnnotation.Instance);
                                if (!hasConflict)
                                {
                                    newDocumentSemanticModel ??= await newDocument.GetRequiredSemanticModelAsync(_cancellationToken).ConfigureAwait(false);
                                    newReferencedSymbols = GetSymbolsInNewSolution(newDocument, newDocumentSemanticModel, conflictAnnotation, tokenOrNode);

                                    // The semantic correctness, after rename, for each token of interest in the
                                    // rename context is performed by getting the symbol pointed by each token 
                                    // and obtain the Symbol's First Ordered Location's  Span-Start and check to
                                    // see if it is the same as before from the base solution. During rename, 
                                    // the spans would have been modified and so we need to adjust the old position
                                    // to the new position for which we use the renameSpanTracker, which was tracking
                                    // & mapping the old span -> new span during rename
                                    hasConflict =
                                        !IsConflictFreeChange(newReferencedSymbols, nonConflictSymbols) &&
                                        await CheckForConflictAsync(conflictResolution, renamedSymbolInNewSolution, conflictAnnotation, newReferencedSymbols, symbolSession.OriginalText, symbolSession.ReplacementText).ConfigureAwait(false);

                                    if (!hasConflict && !conflictAnnotation.IsInvocationExpression)
                                        hasConflict = LocalVariableConflictPerLanguage((SyntaxToken)tokenOrNode, newDocument, newReferencedSymbols);
                                }

                                if (!hasConflict)
                                {
                                    if (conflictAnnotation.IsRenameLocation)
                                    {
                                        conflictResolution.AddRelatedLocation(
                                            new RelatedLocation(originalLocation,
                                            documentId,
                                            complexifiedLocationSpanForThisDocument.Contains(originalLocation) ? RelatedLocationType.ResolvedReferenceConflict : RelatedLocationType.NoConflict,
                                            isReference: true));
                                    }
                                    else
                                    {
                                        // if a complexified renameLocation was not a reference renameLocation, then it was a resolved conflict of a non reference renameLocation
                                        if (!conflictAnnotation.IsOriginalTextLocation && complexifiedLocationSpanForThisDocument.Contains(originalLocation))
                                        {
                                            conflictResolution.AddRelatedLocation(
                                                new RelatedLocation(originalLocation,
                                                documentId,
                                                RelatedLocationType.ResolvedNonReferenceConflict,
                                                isReference: false));
                                        }
                                    }
                                }
                                else
                                {
                                    var baseToken = baseRoot.FindToken(conflictAnnotation.OriginalSpan.Start, true);
                                    var complexifiedTarget = GetExpansionTargetForLocationPerLanguage(baseToken, baseDocument);
                                    conflictResolution.AddRelatedLocation(new RelatedLocation(
                                        originalLocation,
                                        documentId,
                                        complexifiedTarget != null ? RelatedLocationType.PossiblyResolvableConflict : RelatedLocationType.UnresolvableConflict,
                                        isReference: conflictAnnotation.IsRenameLocation,
                                        complexifiedTargetSpan: complexifiedTarget != null ? complexifiedTarget.Span : default));
                                }
                            }
                        }

                        // there are more conflicts that cannot be identified by checking if the tokens still reference the same
                        // symbol. These conflicts are mostly language specific. A good example is a member with the same name
                        // as the parent (yes I know, this is a simplification).
                        if (symbolSession.DocumentIdOfRenameSymbolDeclaration.ProjectId == projectId)
                        {
                            // Calculating declaration conflicts may require renameLocation mapping in documents
                            // that were not otherwise being processed in the current rename phase, so add
                            // the annotated spans in these documents to reverseMappedLocations.
                            foreach (var unprocessedDocumentIdWithPotentialDeclarationConflicts in allDocumentIdsInProject.Where(d => !documentIdsForConflictResolution.Contains(d)))
                            {
                                var newDocument = conflictResolution.CurrentSolution.GetRequiredDocument(unprocessedDocumentIdWithPotentialDeclarationConflicts);
                                var syntaxRoot = await newDocument.GetRequiredSyntaxRootAsync(_cancellationToken).ConfigureAwait(false);
                                var baseDocument = conflictResolution.OldSolution.GetRequiredDocument(unprocessedDocumentIdWithPotentialDeclarationConflicts);
                                var baseSyntaxTree = await baseDocument.GetRequiredSyntaxTreeAsync(_cancellationToken).ConfigureAwait(false);

                                var nodesOrTokensWithConflictCheckAnnotations = GetNodesOrTokensToCheckForConflicts(syntaxRoot);
                                foreach (var (syntax, annotation) in nodesOrTokensWithConflictCheckAnnotations)
                                {
                                    var tokenOrNode = syntax;
                                    var conflictAnnotation = annotation;
                                    reverseMappedLocations[tokenOrNode.GetLocation()!] = baseSyntaxTree.GetLocation(conflictAnnotation.OriginalSpan);
                                }
                            }

                            var referencedSymbols = symbolSession.RenameLocationSet.ReferencedSymbols;
                            var renameSymbol = symbolSession.RenameLocationSet.Symbol;
                            await AddDeclarationConflictsAsync(
                                renamedSymbolInNewSolution, renameSymbol, symbolSession.ReplacementText, referencedSymbols, conflictResolution, reverseMappedLocations, _cancellationToken).ConfigureAwait(false);
                        }
                    }

                    return conflictResolution.RelatedLocations.Any(r => r.Type == RelatedLocationType.PossiblyResolvableConflict);
                }
                catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e, ErrorSeverity.Critical))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            private async Task<ImmutableHashSet<ISymbol>?> GetNonConflictSymbolsAsync(SymbolSession symbolSession, Project currentProject)
            {
                var nonConflictSymbols = symbolSession.NonConflictSymbols;
                if (nonConflictSymbols == null)
                    return null;

                var compilation = await currentProject.GetRequiredCompilationAsync(_cancellationToken).ConfigureAwait(false);
                return ImmutableHashSet.CreateRange(
                    nonConflictSymbols.Select(s => s.GetSymbolKey().Resolve(compilation).GetAnySymbol()).WhereNotNull());
            }

            private static bool IsConflictFreeChange(
                ImmutableArray<ISymbol> symbols, ImmutableHashSet<ISymbol>? nonConflictSymbols)
            {
                if (nonConflictSymbols != null)
                {
                    foreach (var symbol in symbols)
                    {
                        // Reference not points at a symbol in the conflict-free list.  This is a conflict-free change.
                        if (nonConflictSymbols.Contains(symbol))
                            return true;
                    }
                }

                // Just do the default check.
                return false;
            }

            /// <summary>
            /// Gets the list of the nodes that were annotated for a conflict check 
            /// </summary>
            private IEnumerable<(SyntaxNodeOrToken syntax, RenameActionAnnotation annotation)> GetNodesOrTokensToCheckForConflicts(
                SyntaxNode syntaxRoot)
            {
                return syntaxRoot.DescendantNodesAndTokens(descendIntoTrivia: true)
                    .Where(_renameAnnotations.HasAnnotations<RenameActionAnnotation>)
                    .Select(s => (s, _renameAnnotations.GetAnnotations<RenameActionAnnotation>(s).Single()));
            }

            private async Task<bool> CheckForConflictAsync(
                MutableConflictResolution conflictResolution,
                ISymbol renamedSymbolInNewSolution,
                RenameActionAnnotation conflictAnnotation,
                ImmutableArray<ISymbol> newReferencedSymbols,
                string originalText,
                string replacementText)
            {
                try
                {
                    bool hasConflict;
                    var solution = conflictResolution.CurrentSolution;

                    if (conflictAnnotation.IsNamespaceDeclarationReference)
                    {
                        hasConflict = false;
                    }
                    else if (conflictAnnotation.IsMemberGroupReference)
                    {
                        if (!conflictAnnotation.RenameDeclarationLocationReferences.Any())
                        {
                            hasConflict = false;
                        }
                        else
                        {
                            // Ensure newReferencedSymbols contains at least one of the original referenced
                            // symbols, and allow any new symbols to be added to the set of references.

                            hasConflict = true;

                            var newLocationTasks = newReferencedSymbols.Select(async symbol => await GetSymbolLocationAsync(solution, symbol, _cancellationToken).ConfigureAwait(false));
                            var newLocations = (await Task.WhenAll(newLocationTasks).ConfigureAwait(false)).WhereNotNull().Where(loc => loc.IsInSource);
                            foreach (var originalReference in conflictAnnotation.RenameDeclarationLocationReferences.Where(loc => loc.IsSourceLocation))
                            {
                                var adjustedStartPosition = conflictResolution.GetAdjustedTokenStartingPosition(originalReference.TextSpan.Start, originalReference.DocumentId);
                                if (newLocations.Any(loc => loc.SourceSpan.Start == adjustedStartPosition))
                                {
                                    hasConflict = false;
                                    break;
                                }
                            }
                        }
                    }
                    else if (!conflictAnnotation.IsRenameLocation && conflictAnnotation.IsOriginalTextLocation && conflictAnnotation.RenameDeclarationLocationReferences.Length > 1 && newReferencedSymbols.Length == 1)
                    {
                        // an ambiguous situation was resolved through rename in non reference locations
                        hasConflict = false;
                    }
                    else if (newReferencedSymbols.Length != conflictAnnotation.RenameDeclarationLocationReferences.Length)
                    {
                        // Don't show conflicts for errors in the old solution that now bind in the new solution.
                        if (newReferencedSymbols.Length != 0 && conflictAnnotation.RenameDeclarationLocationReferences.Length == 0)
                        {
                            hasConflict = false;
                        }
                        else
                        {
                            hasConflict = true;
                        }
                    }
                    else
                    {
                        hasConflict = false;
                        var symbolIndex = 0;
                        foreach (var symbol in newReferencedSymbols)
                        {
                            if (conflictAnnotation.RenameDeclarationLocationReferences[symbolIndex].SymbolLocationsCount != symbol.Locations.Length)
                            {
                                hasConflict = true;
                                break;
                            }

                            var newLocation = await GetSymbolLocationAsync(solution, symbol, _cancellationToken).ConfigureAwait(false);

                            if (newLocation != null && conflictAnnotation.RenameDeclarationLocationReferences[symbolIndex].IsSourceLocation)
                            {
                                // renameLocation was in source before, but not after rename
                                if (!newLocation.IsInSource)
                                {
                                    hasConflict = true;
                                    break;
                                }

                                var renameDeclarationLocationReference = conflictAnnotation.RenameDeclarationLocationReferences[symbolIndex];
                                var newAdjustedStartPosition = conflictResolution.GetAdjustedTokenStartingPosition(renameDeclarationLocationReference.TextSpan.Start, renameDeclarationLocationReference.DocumentId);
                                if (newAdjustedStartPosition != newLocation.SourceSpan.Start)
                                {
                                    hasConflict = true;
                                    break;
                                }

                                if (conflictAnnotation.RenameDeclarationLocationReferences[symbolIndex].IsOverriddenFromMetadata)
                                {
                                    var overridingSymbol = await SymbolFinder.FindSymbolAtPositionAsync(solution.GetRequiredDocument(newLocation.SourceTree), newLocation.SourceSpan.Start, cancellationToken: _cancellationToken).ConfigureAwait(false);
                                    if (overridingSymbol != null && !Equals(renamedSymbolInNewSolution, overridingSymbol))
                                    {
                                        if (!overridingSymbol.IsOverride)
                                        {
                                            hasConflict = true;
                                            break;
                                        }
                                        else
                                        {
                                            var overriddenSymbol = overridingSymbol.GetOverriddenMember();
                                            if (overriddenSymbol == null || !overriddenSymbol.Locations.All(loc => loc.IsInMetadata))
                                            {
                                                hasConflict = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var newMetadataName = symbol.ToDisplayString(s_metadataSymbolDisplayFormat);
                                var oldMetadataName = conflictAnnotation.RenameDeclarationLocationReferences[symbolIndex].Name;
                                if (newLocation == null ||
                                    newLocation.IsInSource ||
                                    !HeuristicMetadataNameEquivalenceCheck(
                                        oldMetadataName,
                                        newMetadataName,
                                        originalText,
                                        replacementText))
                                {
                                    hasConflict = true;
                                    break;
                                }
                            }

                            symbolIndex++;
                        }
                    }

                    return hasConflict;
                }
                catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e, ErrorSeverity.Critical))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            private ImmutableArray<ISymbol> GetSymbolsInNewSolution(Document newDocument, SemanticModel newDocumentSemanticModel, RenameActionAnnotation conflictAnnotation, SyntaxNodeOrToken tokenOrNode)
            {
                var newReferencedSymbols = RenameUtilities.GetSymbolsTouchingPosition(tokenOrNode.Span.Start, newDocumentSemanticModel, newDocument.Project.Solution.Workspace.Services, _cancellationToken);

                if (conflictAnnotation.IsInvocationExpression)
                {
                    if (tokenOrNode.IsNode)
                    {
                        var invocationReferencedSymbols = SymbolsForEnclosingInvocationExpressionWorker((SyntaxNode)tokenOrNode!, newDocumentSemanticModel, _cancellationToken);
                        if (!invocationReferencedSymbols.IsDefault)
                            newReferencedSymbols = invocationReferencedSymbols;
                    }
                }

                // if there are more than one symbol, then remove the alias symbols.
                // When using (not declaring) an alias, the alias symbol and the target symbol are returned
                // by GetSymbolsTouchingPosition
                if (newReferencedSymbols.Length >= 2)
                    newReferencedSymbols = newReferencedSymbols.WhereAsArray(a => a.Kind != SymbolKind.Alias);

                return newReferencedSymbols;
            }

            private async Task<ISymbol> GetRenamedSymbolInCurrentSolutionAsync(
                SymbolSession symbolSession,
                MutableConflictResolution conflictResolution)
            {
                try
                {
                    // get the renamed symbol in complexified new solution
                    var renamedSymbolDeclarationLocationSpanStart = symbolSession.RenameSymbolDeclarationLocation.SourceSpan.Start;
                    var start = symbolSession.DocumentOfRenameSymbolHasBeenRenamed
                        ? conflictResolution.GetAdjustedTokenStartingPosition(renamedSymbolDeclarationLocationSpanStart, symbolSession.DocumentIdOfRenameSymbolDeclaration)
                        : renamedSymbolDeclarationLocationSpanStart;

                    var document = conflictResolution.CurrentSolution.GetRequiredDocument(symbolSession.DocumentIdOfRenameSymbolDeclaration);
                    var newSymbol = await SymbolFinder.FindSymbolAtPositionAsync(document, start, cancellationToken: _cancellationToken).ConfigureAwait(false);
                    return newSymbol;
                }
                catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e, ErrorSeverity.Critical))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            /// <summary>
            /// The method determines the set of documents that need to be processed for Rename and also determines
            ///  the possible set of names that need to be checked for conflicts.
            /// </summary>
            private static async Task FindDocumentsAndPossibleNameConflictsAsync(
                Solution solution,
                RenameLocations renameLocationSet,
                string originalText,
                string replacementText,
                PooledHashSet<DocumentId> documentsIdsToBeCheckedForConflictBuilder,
                List<string> possibleNameConflicts,
                CancellationToken cancellationToken)
            {
                try
                {
                    var symbol = renameLocationSet.Symbol;
                    var dependencyGraph = solution.GetProjectDependencyGraph();

                    var allRenamedDocuments = renameLocationSet.Locations.Select(loc => loc.Location.SourceTree!).Distinct().Select(solution.GetRequiredDocument);
                    documentsIdsToBeCheckedForConflictBuilder.AddRange(allRenamedDocuments.Select(d => d.Id));

                    var documentsFromAffectedProjects = RenameUtilities.GetDocumentsAffectedByRename(symbol, solution, renameLocationSet.Locations);
                    foreach (var language in documentsFromAffectedProjects.Select(d => d.Project.Language).Distinct())
                    {
                        solution.Workspace.Services.GetLanguageServices(language).GetService<IRenameRewriterLanguageService>()
                            ?.TryAddPossibleNameConflicts(symbol, replacementText, possibleNameConflicts);
                    }

                    await AddDocumentsWithPotentialConflictsAsync(
                        documentsFromAffectedProjects,
                        originalText,
                        replacementText,
                        documentsIdsToBeCheckedForConflictBuilder,
                        possibleNameConflicts,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e, ErrorSeverity.Critical))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            private static async Task AddDocumentsWithPotentialConflictsAsync(
                IEnumerable<Document> documents,
                string originalText,
                string replacementText,
                PooledHashSet<DocumentId> documentsIdsToBeCheckedForConflictBuilder,
                List<string> possibleNameConflicts,
                CancellationToken cancellationToken)
            {
                try
                {

                    foreach (var document in documents)
                    {
                        if (documentsIdsToBeCheckedForConflictBuilder.Contains(document.Id))
                            continue;

                        if (!document.SupportsSyntaxTree)
                            continue;

                        var info = await SyntaxTreeIndex.GetRequiredIndexAsync(document, cancellationToken).ConfigureAwait(false);
                        if (info.ProbablyContainsEscapedIdentifier(originalText))
                        {
                            documentsIdsToBeCheckedForConflictBuilder.Add(document.Id);
                            continue;
                        }

                        if (info.ProbablyContainsIdentifier(replacementText))
                        {
                            documentsIdsToBeCheckedForConflictBuilder.Add(document.Id);
                            continue;
                        }

                        foreach (var replacementName in possibleNameConflicts)
                        {
                            if (info.ProbablyContainsIdentifier(replacementName))
                            {
                                documentsIdsToBeCheckedForConflictBuilder.Add(document.Id);
                                break;
                            }
                        }
                    }
                }
                catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e, ErrorSeverity.Critical))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            // The rename process and annotation for the bookkeeping is performed in one-step
            private async Task<Solution> AnnotateAndRename_WorkerAsync(
                Solution originalSolution,
                Solution partiallyRenamedSolution,
                HashSet<DocumentId> documentIdsToRename,
                RenamedSpansTracker renameSpansTracker)
            {
                try
                {
                    foreach (var documentId in documentIdsToRename.ToList())
                    {
                        _cancellationToken.ThrowIfCancellationRequested();

                        var document = originalSolution.GetRequiredDocument(documentId);
                        var semanticModel = await document.GetRequiredSemanticModelAsync(_cancellationToken).ConfigureAwait(false);
                        var originalSyntaxRoot = await semanticModel.SyntaxTree.GetRootAsync(_cancellationToken).ConfigureAwait(false);

                        // Get all the symbols need to rename in this document.
                        var symbolsRenameParameters = GetRenameRewriterSymbolParametersForDocument(
                            _documentIdToAffectSymbolSessions[documentId],
                            documentId);

                        var conflictLocationSpans = _conflictLocations
                                                    .Where(t => t.DocumentId == documentId)
                                                    .Select(t => t.ComplexifiedSpan).ToSet();

                        // Annotate all nodes with a RenameLocation annotations to record old locations & old referenced symbols.
                        // Also annotate nodes that should get complexified (nodes for rename locations + conflict locations)
                        var parameters = new RenameRewriterParameters(
                            originalSolution,
                            renameSpansTracker,
                            document,
                            semanticModel,
                            originalSyntaxRoot,
                            symbolsRenameParameters,
                            conflictLocationSpans,
                            _renameAnnotations,
                            _cancellationToken);

                        var renameRewriterLanguageService = document.GetRequiredLanguageService<IRenameRewriterLanguageService>();
                        var newRoot = renameRewriterLanguageService.AnnotateAndRename(parameters);

                        if (newRoot == originalSyntaxRoot)
                        {
                            // Update the list for the current phase, some files with strings containing the original or replacement
                            // text may have been filtered out.
                            documentIdsToRename.Remove(documentId);
                        }
                        else
                        {
                            partiallyRenamedSolution = partiallyRenamedSolution.WithDocumentSyntaxRoot(documentId, newRoot, PreservationMode.PreserveIdentity);
                        }
                    }

                    return partiallyRenamedSolution;
                }
                catch (Exception e) when (FatalError.ReportAndPropagateUnlessCanceled(e))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            /// We try to rewrite all locations that are invalid candidate locations. If there is only
            /// one renameLocation it must be the correct one (the symbol is ambiguous to something else)
            /// and we always try to rewrite it.  If there are multiple locations, we only allow it
            /// if the candidate reason allows for it).
            private static bool ShouldIncludeLocation(ISet<RenameLocation> renameLocations, RenameLocation location)
            {
                if (location.IsRenameInStringOrComment)
                {
                    return false;
                }

                if (renameLocations.Count == 1)
                {
                    return true;
                }

                return RenameLocation.ShouldRename(location);
            }

            /// <summary>
            /// We try to compute the sub-spans to rename within the given <paramref name="containingLocationForStringOrComment"/>.
            /// If we are renaming within a string, the locations to rename are always within this containing string renameLocation
            /// and we can identify these sub-spans.
            /// However, if we are renaming within a comment, the rename locations can be anywhere in trivia,
            /// so we return null and the rename rewriter will perform a complete regex match within comment trivia
            /// and rename all matches instead of specific matches.
            /// </summary>
            private static ImmutableSortedSet<TextSpan>? GetSubSpansToRenameInStringAndCommentTextSpans(
                TextSpan containingLocationForStringOrComment,
                IEnumerable<RenameLocation> locationsToRename)
            {
                var builder = ImmutableSortedSet.CreateBuilder<TextSpan>();
                foreach (var renameLocation in locationsToRename)
                {
                    if (!containingLocationForStringOrComment.Contains(renameLocation.Location.SourceSpan))
                    {
                        // We found a renameLocation outside the 'containingLocationForStringOrComment',
                        // which is likely in trivia.
                        // Bail out from computing specific sub-spans and let the rename rewriter
                        // do a full regex match and replace.
                        return null;
                    }

                    // Compute the sub-span within 'containingLocationForStringOrComment' that needs to be renamed.
                    var offset = renameLocation.Location.SourceSpan.Start - containingLocationForStringOrComment.Start;
                    var length = renameLocation.Location.SourceSpan.Length;
                    var subSpan = new TextSpan(offset, length);
                    builder.Add(subSpan);
                }

                return builder.ToImmutable();
            }

            private static ImmutableHashSet<RenameRewriterSymbolParameters> GetRenameRewriterSymbolParametersForDocument(
                HashSet<SymbolSession> symbolSessions,
                DocumentId documentId)
            {
                using var _ = PooledHashSet<RenameRewriterSymbolParameters>.GetInstance(out var builder);
                foreach (var session in symbolSessions)
                {
                    builder.Add(session.ToRenameRewriterParametersForDocument(documentId));
                }

                return builder.ToImmutableHashSet();
            }
        }
    }
}
