// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Features.RQName;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.FindSymbols.Finders;
using Microsoft.CodeAnalysis.FindSymbols.FindReferences;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.InheritanceMargin
{
    internal static class InheritanceMarginServiceHelper
    {
        private static readonly SymbolDisplayFormat s_displayFormat = new(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeContainingType |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        public static async ValueTask<ImmutableArray<SerializableInheritanceMarginItem>> GetInheritanceMemberItemAsync(
            Solution solution,
            ProjectId projectId,
            ImmutableArray<(SymbolKey symbolKey, int lineNumber)> symbolKeyAndLineNumbers,
            CancellationToken cancellationToken)
        {
            var remoteClient = await RemoteHostClient.TryGetClientAsync(solution.Workspace, cancellationToken).ConfigureAwait(false);
            if (remoteClient != null)
            {
                // Here the line number is also passed to the remote process. It is done in this way because
                // when a set of symbols is passed to remote process, those without inheritance targets would not be returned.
                // To match the returned inheritance targets to the line number, we need set an 'Id' when calling the remote process,
                // however, given the line number is just an int, setting up an int 'Id' for an int is quite useless, so just passed it to the remote process.
                var result = await remoteClient.TryInvokeAsync<IRemoteInheritanceMarginService, ImmutableArray<SerializableInheritanceMarginItem>>(
                    solution,
                    (remoteInheritanceMarginService, solutionInfo, cancellationToken) =>
                        remoteInheritanceMarginService.GetInheritanceMarginItemsAsync(solutionInfo, projectId, symbolKeyAndLineNumbers, cancellationToken),
                    cancellationToken).ConfigureAwait(false);

                if (!result.HasValue)
                {
                    return ImmutableArray<SerializableInheritanceMarginItem>.Empty;
                }

                return result.Value;
            }
            else
            {
                return await GetInheritanceMemberItemInProcAsync(solution, projectId, symbolKeyAndLineNumbers, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async ValueTask<ImmutableArray<SerializableInheritanceMarginItem>> GetInheritanceMemberItemInProcAsync(
            Solution solution,
            ProjectId projectId,
            ImmutableArray<(SymbolKey symbolKey, int lineNumber)> symbolKeyAndLineNumbers,
            CancellationToken cancellationToken)
        {
            var project = solution.GetRequiredProject(projectId);
            var compilation = await project.GetRequiredCompilationAsync(cancellationToken).ConfigureAwait(false);
            using var _ = ArrayBuilder<SerializableInheritanceMarginItem>.GetInstance(out var builder);
            foreach (var (symbolKey, lineNumber) in symbolKeyAndLineNumbers)
            {
                var symbol = symbolKey.Resolve(compilation, cancellationToken: cancellationToken).Symbol;

                if (symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    await AddInheritanceMemberItemsForNamedTypeAsync(solution, namedTypeSymbol, lineNumber, builder, cancellationToken).ConfigureAwait(false);
                }

                if (symbol is IEventSymbol or IPropertySymbol or IMethodSymbol)
                {
                    await AddInheritanceMemberItemsForMembersAsync(solution, symbol, lineNumber, builder, cancellationToken).ConfigureAwait(false);
                }
            }

            return builder.ToImmutable();
        }

        private static async ValueTask AddInheritanceMemberItemsForNamedTypeAsync(
            Solution solution,
            INamedTypeSymbol memberSymbol,
            int lineNumber,
            ArrayBuilder<SerializableInheritanceMarginItem> builder,
            CancellationToken cancellationToken)
        {
            // Get all base types.
            var allBaseSymbols = BaseTypeFinder.FindBaseTypesAndInterfaces(memberSymbol);

            // Filter out
            // 1. System.Object. (otherwise margin would be shown for all classes)
            // 2. System.ValueType. (otherwise margin would be shown for all structs)
            // 3. System.Enum. (otherwise margin would be shown for all enum)
            // 4. Error type.
            // For example, if user has code like this,
            // class Bar : ISomethingIsNotDone { }
            // The interface has not been declared yet, so don't show this error type to user.
            var baseSymbols = allBaseSymbols
                .WhereAsArray(symbol => !symbol.IsErrorType() && symbol.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum));

            // Get all derived types
            var allDerivedSymbols = await GetDerivedTypesAndImplementationsAsync(
                solution,
                memberSymbol,
                cancellationToken).ConfigureAwait(false);

            // Ensure the user won't be able to see symbol outside the solution for derived symbols.
            // For example, if user is viewing 'IEnumerable interface' from metadata, we don't want to tell
            // the user all the derived types under System.Collections
            var derivedSymbols = allDerivedSymbols.WhereAsArray(symbol => symbol.Locations.Any(l => l.IsInSource));

            if (baseSymbols.Any() || derivedSymbols.Any())
            {
                if (memberSymbol.TypeKind == TypeKind.Interface)
                {
                    var item = await CreateInheritanceMemberItemForInterfaceAsync(
                        solution,
                        memberSymbol,
                        lineNumber,
                        baseSymbols: baseSymbols.CastArray<ISymbol>(),
                        derivedTypesSymbols: derivedSymbols.CastArray<ISymbol>(),
                        cancellationToken).ConfigureAwait(false);
                    builder.AddIfNotNull(item);
                }
                else
                {
                    Debug.Assert(memberSymbol.TypeKind is TypeKind.Class or TypeKind.Struct);
                    var item = await CreateInheritanceItemForClassAndStructureAsync(
                        solution,
                        memberSymbol,
                        lineNumber,
                        baseSymbols: baseSymbols.CastArray<ISymbol>(),
                        derivedTypesSymbols: derivedSymbols.CastArray<ISymbol>(),
                        cancellationToken).ConfigureAwait(false);
                    builder.AddIfNotNull(item);
                }
            }
        }

        private static async ValueTask AddInheritanceMemberItemsForMembersAsync(
            Solution solution,
            ISymbol memberSymbol,
            int lineNumber,
            ArrayBuilder<SerializableInheritanceMarginItem> builder,
            CancellationToken cancellationToken)
        {
            if (memberSymbol.ContainingSymbol.IsInterfaceType())
            {
                // Go down the inheritance chain to find all the implementing targets.
                var allImplementingSymbols = await GetImplementingSymbolsForTypeMemberAsync(solution, memberSymbol, cancellationToken).ConfigureAwait(false);

                // For all implementing symbols, make sure it is in source.
                // For example, if the user is viewing IEnumerable from metadata,
                // then don't show the derived overriden & implemented types in System.Collections
                var implementingSymbols = allImplementingSymbols.WhereAsArray(symbol => symbol.Locations.Any(l => l.IsInSource));

                if (implementingSymbols.Any())
                {
                    var item = await CreateInheritanceMemberItemForInterfaceMemberAsync(solution,
                        memberSymbol,
                        lineNumber,
                        implementingMembers: implementingSymbols,
                        cancellationToken).ConfigureAwait(false);
                    builder.AddIfNotNull(item);
                }
            }
            else
            {
                // Go down the inheritance chain to find all the overriding targets.
                var allOverridingSymbols = await SymbolFinder.FindOverridesArrayAsync(memberSymbol, solution, cancellationToken: cancellationToken).ConfigureAwait(false);

                // Go up the inheritance chain to find all overridden targets
                var overriddenSymbols = GetOverriddenSymbols(memberSymbol);

                // Go up the inheritance chain to find all the implemented targets.
                var implementedSymbols = GetImplementedSymbolsForTypeMember(memberSymbol, overriddenSymbols);

                // For all overriding symbols, make sure it is in source.
                // For example, if the user is viewing System.Threading.SynchronizationContext from metadata,
                // then don't show the derived overriden & implemented method in the default implementation for System.Threading.SynchronizationContext in metadata
                var overridingSymbols = allOverridingSymbols.WhereAsArray(symbol => symbol.Locations.Any(l => l.IsInSource));

                if (overridingSymbols.Any() || overriddenSymbols.Any() || implementedSymbols.Any())
                {
                    var item = await CreateInheritanceMemberItemForClassOrStructMemberAsync(solution,
                        memberSymbol,
                        lineNumber,
                        implementedMembers: implementedSymbols,
                        overridingMembers: overridingSymbols,
                        overriddenMembers: overriddenSymbols,
                        cancellationToken).ConfigureAwait(false);
                    builder.AddIfNotNull(item);
                }
            }
        }

        private static async ValueTask<SerializableInheritanceMarginItem> CreateInheritanceMemberItemForInterfaceAsync(
            Solution solution,
            INamedTypeSymbol interfaceSymbol,
            int lineNumber,
            ImmutableArray<ISymbol> baseSymbols,
            ImmutableArray<ISymbol> derivedTypesSymbols,
            CancellationToken cancellationToken)
        {
            var baseSymbolItems = await baseSymbols
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(
                    solution,
                    symbol,
                    InheritanceRelationship.InheritedInterface,
                    cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            var derivedTypeItems = await derivedTypesSymbols
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(solution,
                    symbol,
                    InheritanceRelationship.ImplementingType,
                    cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            return new SerializableInheritanceMarginItem(
                lineNumber,
                interfaceSymbol.ToDisplayParts(s_displayFormat).ToTaggedText(),
                interfaceSymbol.GetGlyph(),
                baseSymbolItems.Concat(derivedTypeItems));
        }

        private static async ValueTask<SerializableInheritanceMarginItem> CreateInheritanceMemberItemForInterfaceMemberAsync(
            Solution solution,
            ISymbol memberSymbol,
            int lineNumber,
            ImmutableArray<ISymbol> implementingMembers,
            CancellationToken cancellationToken)
        {
            var implementedMemberItems = await implementingMembers
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(
                    solution,
                    symbol,
                    InheritanceRelationship.ImplementingMember,
                    cancellationToken), cancellationToken).ConfigureAwait(false);

            return new SerializableInheritanceMarginItem(
                lineNumber,
                memberSymbol.ToDisplayParts(s_displayFormat).ToTaggedText(),
                memberSymbol.GetGlyph(),
                implementedMemberItems);
        }

        private static async ValueTask<SerializableInheritanceMarginItem> CreateInheritanceItemForClassAndStructureAsync(
            Solution solution,
            INamedTypeSymbol memberSymbol,
            int lineNumber,
            ImmutableArray<ISymbol> baseSymbols,
            ImmutableArray<ISymbol> derivedTypesSymbols,
            CancellationToken cancellationToken)
        {
            // If the target is an interface, it would be shown as 'Inherited interface',
            // and if it is an class/struct, it whould be shown as 'Base Type'
            var baseSymbolItems = await baseSymbols
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(
                    solution,
                    symbol,
                    symbol.IsInterfaceType() ? InheritanceRelationship.ImplementedInterface : InheritanceRelationship.BaseType,
                    cancellationToken), cancellationToken).ConfigureAwait(false);

            var derivedTypeItems = await derivedTypesSymbols
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(solution,
                    symbol,
                    InheritanceRelationship.DerivedType,
                    cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            return new SerializableInheritanceMarginItem(
                lineNumber,
                memberSymbol.ToDisplayParts(s_displayFormat).ToTaggedText(),
                memberSymbol.GetGlyph(),
                baseSymbolItems.Concat(derivedTypeItems));
        }

        private static async ValueTask<SerializableInheritanceMarginItem> CreateInheritanceMemberItemForClassOrStructMemberAsync(
            Solution solution,
            ISymbol memberSymbol,
            int lineNumber,
            ImmutableArray<ISymbol> implementedMembers,
            ImmutableArray<ISymbol> overridingMembers,
            ImmutableArray<ISymbol> overriddenMembers,
            CancellationToken cancellationToken)
        {
            var implementedMemberItems = await implementedMembers
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(
                    solution,
                    symbol,
                    InheritanceRelationship.ImplementedMember,
                    cancellationToken), cancellationToken).ConfigureAwait(false);

            var overridenMemberItems = await overriddenMembers
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(
                    solution,
                    symbol,
                    InheritanceRelationship.OverriddenMember,
                    cancellationToken), cancellationToken).ConfigureAwait(false);

            var overridingMemberItems = await overridingMembers
                .SelectAsArray(symbol => symbol.OriginalDefinition)
                .Distinct()
                .SelectAsArrayAsync((symbol, _) => CreateInheritanceItemAsync(
                    solution,
                    symbol,
                    InheritanceRelationship.OverridingMember,
                    cancellationToken), cancellationToken).ConfigureAwait(false);

            return new SerializableInheritanceMarginItem(
                lineNumber,
                memberSymbol.ToDisplayParts(s_displayFormat).ToTaggedText(),
                memberSymbol.GetGlyph(),
                implementedMemberItems.Concat(overridenMemberItems).Concat(overridingMemberItems));
        }

        private static async ValueTask<SerializableInheritanceTargetItem> CreateInheritanceItemAsync(
            Solution solution,
            ISymbol targetSymbol,
            InheritanceRelationship inheritanceRelationship,
            CancellationToken cancellationToken)
        {
            var symbolInSource = await SymbolFinder.FindSourceDefinitionAsync(targetSymbol, solution, cancellationToken).ConfigureAwait(false);
            targetSymbol = symbolInSource ?? targetSymbol;

            // Right now the targets are not shown in a classified way.
            var definition = await targetSymbol.ToNonClassifiedDefinitionItemAsync(
                solution,
                includeHiddenLocations: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var displayName = targetSymbol.ToDisplayString(s_displayFormat);

            return new SerializableInheritanceTargetItem(
                inheritanceRelationship,
                // Id is used by FAR service for caching, it is not used in inheritance margin
                SerializableDefinitionItem.Dehydrate(id: 0, definition),
                targetSymbol.GetGlyph(),
                displayName);
        }

        private static ImmutableArray<ISymbol> GetImplementedSymbolsForTypeMember(
            ISymbol memberSymbol,
            ImmutableArray<ISymbol> overriddenSymbols)
        {
            if (memberSymbol is IMethodSymbol or IEventSymbol or IPropertySymbol)
            {
                using var _ = ArrayBuilder<ISymbol>.GetInstance(out var builder);

                // 1. Get the direct implementing symbols in interfaces.
                var directImplementingSymbols = memberSymbol.ExplicitOrImplicitInterfaceImplementations();
                builder.AddRange(directImplementingSymbols);

                // 2. Also add the direct implementing symbols for the overridden symbols.
                // For example:
                // interface IBar { void Foo(); }
                // class Bar : IBar { public override void Foo() { } }
                // class Bar2 : Bar { public override void Foo() { } }
                // For 'Bar2.Foo()',  we need to find 'IBar.Foo()'
                foreach (var symbol in overriddenSymbols)
                {
                    builder.AddRange(symbol.ExplicitOrImplicitInterfaceImplementations());
                }

                return builder.ToImmutableArray();
            }

            return ImmutableArray<ISymbol>.Empty;
        }

        /// <summary>
        /// For the <param name="memberSymbol"/>, get all the implementing symbols.
        /// </summary>
        private static async Task<ImmutableArray<ISymbol>> GetImplementingSymbolsForTypeMemberAsync(
            Solution solution,
            ISymbol memberSymbol,
            CancellationToken cancellationToken)
        {
            if (memberSymbol is IMethodSymbol or IEventSymbol or IPropertySymbol
                && memberSymbol.ContainingSymbol.IsInterfaceType())
            {
                using var _ = ArrayBuilder<ISymbol>.GetInstance(out var builder);
                // 1. Find all direct implementations for this member
                var implementationSymbols = await SymbolFinder.FindMemberImplementationsArrayAsync(
                    memberSymbol,
                    solution,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                builder.AddRange(implementationSymbols);

                // 2. Continue searching the overriden symbols. For example:
                // interface IBar { void Foo(); }
                // class Bar : IBar { public virtual void Foo() { } }
                // class Bar2 : IBar { public override void Foo() { } }
                // For 'IBar.Foo()', we need to find 'Bar2.Foo()'
                foreach (var implementationSymbol in implementationSymbols)
                {
                    builder.AddRange(await SymbolFinder.FindOverridesArrayAsync(implementationSymbol, solution, cancellationToken: cancellationToken).ConfigureAwait(false));
                }

                return builder.ToImmutableArray();
            }

            return ImmutableArray<ISymbol>.Empty;
        }

        /// <summary>
        /// Get overridden members the <param name="memberSymbol"/>.
        /// </summary>
        private static ImmutableArray<ISymbol> GetOverriddenSymbols(ISymbol memberSymbol)
        {
            if (memberSymbol is INamedTypeSymbol)
            {
                return ImmutableArray<ISymbol>.Empty;
            }
            else
            {
                using var _ = ArrayBuilder<ISymbol>.GetInstance(out var builder);
                for (var overriddenMember = memberSymbol.GetOverriddenMember();
                    overriddenMember != null;
                    overriddenMember = overriddenMember.GetOverriddenMember())
                {
                    builder.Add(overriddenMember.OriginalDefinition);
                }

                return builder.ToImmutableArray();
            }
        }

        /// <summary>
        /// Get the derived interfaces and derived classes for <param name="typeSymbol"/>.
        /// </summary>
        private static async Task<ImmutableArray<INamedTypeSymbol>> GetDerivedTypesAndImplementationsAsync(
            Solution solution,
            INamedTypeSymbol typeSymbol,
            CancellationToken cancellationToken)
        {
            if (typeSymbol.IsInterfaceType())
            {
                var allDerivedInterfaces = await SymbolFinder.FindDerivedInterfacesArrayAsync(
                    typeSymbol,
                    solution,
                    transitive: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var allImplementations = await SymbolFinder.FindImplementationsArrayAsync(
                    typeSymbol,
                    solution,
                    transitive: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                return allDerivedInterfaces.Concat(allImplementations);
            }
            else
            {
                return await SymbolFinder.FindDerivedClassesArrayAsync(
                    typeSymbol,
                    solution,
                    transitive: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<DefinitionItem> ToDefinitionItemAsync(
            ISymbol definition,
            ImmutableArray<Location> locations,
            Solution solution,
            bool isPrimary,
            bool includeHiddenLocations,
            bool includeClassifiedSpans,
            FindReferencesSearchOptions options,
            CancellationToken cancellationToken)
        {
            // Ensure we're working with the original definition for the symbol. I.e. When we're 
            // creating definition items, we want to create them for types like Dictionary<TKey,TValue>
            // not some random instantiation of that type.  
            //
            // This ensures that the type will both display properly to the user, as well as ensuring
            // that we can accurately resolve the type later on when we try to navigate to it.
            if (!definition.IsTupleField())
            {
                // In an earlier implementation of the compiler APIs, tuples and tuple fields symbols were definitions
                // We pretend this is still the case
                definition = definition.OriginalDefinition;
            }

            var displayParts = definition.ToDisplayParts(s_displayFormat).ToTaggedText();
            var nameDisplayParts = definition.ToDisplayParts(s_displayFormat).ToTaggedText();

            var tags = GlyphTags.GetTags(definition.GetGlyph());
            var displayIfNoReferences = definition.ShouldShowWithNoReferenceLocations(
                options, showMetadataSymbolsWithoutReferences: false);

            var properties = GetProperties(definition, isPrimary);

            // If it's a namespace, don't create any normal location.  Namespaces
            // come from many different sources, but we'll only show a single 
            // root definition node for it.  That node won't be navigable.
            using var sourceLocations = TemporaryArray<DocumentSpan>.Empty;
            if (definition.Kind != SymbolKind.Namespace)
            {
                foreach (var location in locations)
                {
                    if (location.IsInMetadata)
                    {
                        return DefinitionItem.CreateMetadataDefinition(
                            tags, displayParts, nameDisplayParts, solution,
                            definition, properties, displayIfNoReferences);
                    }
                    else if (location.IsInSource)
                    {
                        if (!location.IsVisibleSourceLocation() &&
                            !includeHiddenLocations)
                        {
                            continue;
                        }

                        var document = solution.GetDocument(location.SourceTree);
                        if (document != null)
                        {
                            var documentLocation = !includeClassifiedSpans
                                ? new DocumentSpan(document, location.SourceSpan)
                                : await ClassifiedSpansAndHighlightSpanFactory.GetClassifiedDocumentSpanAsync(
                                    document, location.SourceSpan, cancellationToken).ConfigureAwait(false);

                            sourceLocations.Add(documentLocation);
                        }
                    }
                }
            }

            if (sourceLocations.Count == 0)
            {
                // If we got no definition locations, then create a sentinel one
                // that we can display but which will not allow navigation.
                return DefinitionItem.CreateNonNavigableItem(
                    tags, displayParts,
                    DefinitionItem.GetOriginationParts(definition),
                    properties, displayIfNoReferences);
            }

            var displayableProperties = AbstractReferenceFinder.GetAdditionalFindUsagesProperties(definition);

            return DefinitionItem.Create(
                tags, displayParts, sourceLocations.ToImmutableAndClear(),
                nameDisplayParts, properties, displayableProperties, displayIfNoReferences);
        }

        private static ImmutableDictionary<string, string> GetProperties(ISymbol definition, bool isPrimary)
        {
            var properties = ImmutableDictionary<string, string>.Empty;

            if (isPrimary)
            {
                properties = properties.Add(DefinitionItem.Primary, "");
            }

            var rqName = RQNameInternal.From(definition);
            if (rqName != null)
            {
                properties = properties.Add(DefinitionItem.RQNameKey1, rqName);
            }

            if (definition?.IsConstructor() == true)
            {
                // If the symbol being considered is a constructor include the containing type in case
                // a third party wants to navigate to that.
                rqName = RQNameInternal.From(definition.ContainingType);
                if (rqName != null)
                {
                    properties = properties.Add(DefinitionItem.RQNameKey2, rqName);
                }
            }

            return properties;
        }

        public static Task<DefinitionItem> ToNonClassifiedDefinitionItemAsync(
            this ISymbol definition,
            Solution solution,
            bool includeHiddenLocations,
            CancellationToken cancellationToken)
        {
            return ToDefinitionItemAsync(
                definition, solution, isPrimary: false, includeHiddenLocations, includeClassifiedSpans: false,
                options: FindReferencesSearchOptions.Default.With(unidirectionalHierarchyCascade: true), cancellationToken: cancellationToken);
        }

        public static Task<DefinitionItem> ToClassifiedDefinitionItemAsync(
            this ISymbol definition,
            Solution solution,
            bool isPrimary,
            bool includeHiddenLocations,
            FindReferencesSearchOptions options,
            CancellationToken cancellationToken)
        {
            return ToDefinitionItemAsync(
                definition, solution, isPrimary,
                includeHiddenLocations, includeClassifiedSpans: true,
                options, cancellationToken);
        }

        public static Task<DefinitionItem> ToClassifiedDefinitionItemAsync(
            this SymbolGroup group, Solution solution, bool isPrimary, bool includeHiddenLocations, FindReferencesSearchOptions options, CancellationToken cancellationToken)
        {
            // Make a single definition item that knows about all the locations of all the symbols in the group.
            var allLocations = group.Symbols.SelectMany(s => s.Locations).ToImmutableArray();
            return ToDefinitionItemAsync(group.Symbols.First(), allLocations, solution, isPrimary, includeHiddenLocations, includeClassifiedSpans: true, options, cancellationToken);
        }

        private static Task<DefinitionItem> ToDefinitionItemAsync(
            ISymbol definition, Solution solution, bool isPrimary, bool includeHiddenLocations, bool includeClassifiedSpans, FindReferencesSearchOptions options, CancellationToken cancellationToken)
        {
            return ToDefinitionItemAsync(definition, definition.Locations, solution, isPrimary, includeHiddenLocations, includeClassifiedSpans, options, cancellationToken);
        }
    }
}
