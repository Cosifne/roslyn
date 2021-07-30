// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET472
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.InheritanceMargin;
using Microsoft.CodeAnalysis.Host.Mef;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Xml.Schema;
using Roslyn.Utilities;

namespace AnalyzerRunner
{
    internal class InheritanceMarginRunner
    {
        private readonly Workspace _workspace;

        public InheritanceMarginRunner(Workspace workspace)
        {
            _workspace = workspace;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var mefProvider = (IMefHostExportProvider)_workspace.Services.HostServices;

            var allProjects = _workspace.CurrentSolution.Projects.ToList();
            Console.WriteLine("Project Number: {allProjects.Count}.");
            var infoBuilder = new Dictionary<Project, ProjectMarginInfo>();

            var taskArray = new Task<Dictionary<Document, DocumentMarginInfo>>[allProjects.Count];

            for (int i = 0; i < allProjects.Count; i++)
            {
                var project = allProjects[i];
                Console.WriteLine($@"Start processing project number : {i + 1}, project Name: {project.Name}");

                var t = Task.Run(async () =>
                {
                    var projectBuilder = new Dictionary<Document, DocumentMarginInfo>();
                    foreach (var document in project.Documents)
                    {
                        var serivce = document.GetRequiredLanguageService<IInheritanceMarginService>();
                        var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                        var inheritanceMarginItems = await serivce.GetInheritanceMemberItemsAsync(document, root.FullSpan, cancellationToken).ConfigureAwait(false);
                        projectBuilder[document] = Create(inheritanceMarginItems);
                    }
                    return projectBuilder;
                });

                taskArray[i] = t;
            }

            var result = await Task.WhenAll(taskArray).ConfigureAwait(false);
            for (int i = 0; i < result.Length; i++)
            {
                infoBuilder[allProjects[i]] = new ProjectMarginInfo(result[i]);
            }

            PrintHelper(infoBuilder);
        }

        internal static string PrintItemHelper(InheritanceRelationship relationship)
        {
            var s_relationships_Shown_As_I_Up_Arrow
                = ImmutableArray<InheritanceRelationship>.Empty
                .Add(InheritanceRelationship.ImplementedInterface)
                .Add(InheritanceRelationship.InheritedInterface)
                .Add(InheritanceRelationship.ImplementedMember);

            var s_relationships_Shown_As_I_Down_Arrow
                = ImmutableArray<InheritanceRelationship>.Empty
                .Add(InheritanceRelationship.ImplementingType)
                .Add(InheritanceRelationship.ImplementingMember);

            var s_relationships_Shown_As_O_Up_Arrow
                = ImmutableArray<InheritanceRelationship>.Empty
                .Add(InheritanceRelationship.BaseType)
                .Add(InheritanceRelationship.OverriddenMember);

            var s_relationships_Shown_As_O_Down_Arrow
                = ImmutableArray<InheritanceRelationship>.Empty
                .Add(InheritanceRelationship.DerivedType)
                .Add(InheritanceRelationship.OverridingMember);

            if (s_relationships_Shown_As_I_Up_Arrow.Any(flag => relationship.HasFlag(flag))
                && s_relationships_Shown_As_O_Down_Arrow.Any(flag => relationship.HasFlag(flag)))
            {
                return "I↑O↓";
            }

            if (s_relationships_Shown_As_I_Up_Arrow.Any(flag => relationship.HasFlag(flag))
                && s_relationships_Shown_As_O_Up_Arrow.Any(flag => relationship.HasFlag(flag)))
            {
                return "IO↑";
            }

            if (s_relationships_Shown_As_I_Up_Arrow.Any(flag => relationship.HasFlag(flag)))
            {
                return "I↑";
            }

            if (s_relationships_Shown_As_I_Down_Arrow.Any(flag => relationship.HasFlag(flag)))
            {
                return "I↓";
            }

            if (s_relationships_Shown_As_O_Down_Arrow.Any(flag => relationship.HasFlag(flag)))
            {
                return "O↓";
            }

            if (s_relationships_Shown_As_O_Up_Arrow.Any(flag => relationship.HasFlag(flag)))
            {
                return "O↑";
            }

            throw new ArgumentException(relationship.ToString());
        }

        internal static void PrintKindHelper(MemberInfo[] memberInfos)
        {
            var resultCollector = new SortedDictionary<string, int>();
            foreach (var memberInfo in memberInfos)
            {
                var relationship = memberInfo.TargetInfo.Keys.Aggregate((k1, k2) => k1 | k2);
                if (resultCollector.TryGetValue(PrintItemHelper(relationship), out var result))
                {
                    resultCollector[PrintItemHelper(relationship)] = result + 1;
                }
                else
                {
                    resultCollector[PrintItemHelper(relationship)] = 1;
                }
            }

            foreach (var (relationship, numberCount) in resultCollector)
            {
                Console.WriteLine("\t\t\t"+ $"icon: {relationship}, total number: {numberCount}");
            }
        }

        internal static void PrintHelper(Dictionary<Project, ProjectMarginInfo> solutionInfo)
        {
            var allDocumentInfo = solutionInfo.SelectMany(kvp => kvp.Value.DocumentInfos.Select(kvp2 => kvp2.Value)).ToArray();
            var allMarginInfo = allDocumentInfo.SelectMany(
                docInfo => docInfo.MarginInTheDocument.Values.SelectMany(member => member.MembersOnThisLine)).ToArray();
            var spiliter = string.Join("", Enumerable.Repeat("-", 60));
            var indentation = "\t";
            Console.WriteLine(spiliter);
            Console.WriteLine($"Total margin number: {allDocumentInfo.Select(docInfo => docInfo.MarginInThisDocument).Sum()}");
//            Solution -> project -> Total count for the number of margin.

//Margin has one targeting direction -> Margin has only one item

//-> Margin has more than two items

//Margin two targeting direction -> Each direction has only one item

//-> Each direction has more than two items

            Console.WriteLine(spiliter);
            var marginsContainsOneMember = allDocumentInfo.SelectMany(docInfo => docInfo.MarginContainsOneMember).ToArray();
            Console.WriteLine($"Margin contains one member: {marginsContainsOneMember.Length}");
            Console.WriteLine(indentation + $"Number of Margin contains one direction target: {marginsContainsOneMember.Select(marginInfo => marginInfo.MembersOnThisLine[0]).Where(memberInfo => memberInfo.TargetInfo.Count == 1).Count()}");

            var marginContainsOneDirectionAndOneTarget = marginsContainsOneMember
                .Select(marginInfo => marginInfo.MembersOnThisLine[0])
                .Where(memberInfo => memberInfo.TargetInfo.Count == 1 && memberInfo.TargetInfo.Single().Value.Length == 1).ToArray();
            Console.WriteLine(indentation + indentation + $"Margin contains one direction and one target: {marginContainsOneDirectionAndOneTarget.Length}");
            PrintKindHelper(marginContainsOneDirectionAndOneTarget);

            var marginContainsOneDirectionAndTwoTargets = marginsContainsOneMember
                .Select(marginInfo => marginInfo.MembersOnThisLine[0]).Where(memberInfo =>
                    memberInfo.TargetInfo.Count == 1 && memberInfo.TargetInfo.Single().Value.Length == 2).ToArray();
            Console.WriteLine(indentation + indentation + $"Margin contains one direction and two targets: {marginContainsOneDirectionAndTwoTargets.Length}");
            PrintKindHelper(marginContainsOneDirectionAndTwoTargets);

            var marginContainsThreeOrMoreTargets = marginsContainsOneMember
                .Select(marginInfo => marginInfo.MembersOnThisLine[0]).Where(memberInfo =>
                    memberInfo.TargetInfo.Count == 1 && memberInfo.TargetInfo.Single().Value.Length > 2).ToArray();
            Console.WriteLine(indentation + indentation + $"Margin contains one direction and three or more targets: {marginContainsThreeOrMoreTargets.Length}");
            PrintKindHelper(marginContainsThreeOrMoreTargets);

            var marginContainsTwoDirectionTargets = marginsContainsOneMember
                .Select(marginInfo => marginInfo.MembersOnThisLine[0])
                .Where(memberInfo => memberInfo.TargetInfo.Count == 2).ToArray();
            Console.WriteLine(indentation + $"Number of Margin contains two direction targets: {marginContainsTwoDirectionTargets.Length}");

            var marginContainsTwoDirectionTargetWithOneOnEachDirection = marginContainsTwoDirectionTargets
                .Where(member => member.TargetInfo.All(kvp => kvp.Value.Length == 1)).ToArray();
            Console.WriteLine(indentation + indentation + $"Number of Margin contains two direction targets and each direction has only one targets: {marginContainsTwoDirectionTargetWithOneOnEachDirection.Length}");
            PrintKindHelper(marginContainsTwoDirectionTargetWithOneOnEachDirection);

            var marginContainsThreeDirectionTargets = marginsContainsOneMember
                .Select(marginInfo => marginInfo.MembersOnThisLine[0])
                .Where(memberInfo => memberInfo.TargetInfo.Count == 3).ToArray();
            Console.WriteLine(indentation + $"Number of Margin contains three or more direction targets: {marginContainsThreeDirectionTargets.Length}");
            PrintKindHelper(marginContainsThreeDirectionTargets);

            Console.WriteLine($"Margin contains two members (example: two events declared on the same line): {allDocumentInfo.Select(docInfo => docInfo.MarginContainsTwoMembers.Length).Sum()}");
            Console.WriteLine($"Margin contains three or more members: {allDocumentInfo.Select(docInfo => docInfo.MarginContainsThreeOrMoreMembers.Length).Sum()}");
            Console.WriteLine(spiliter);
        }

        internal static DocumentMarginInfo Create(ImmutableArray<InheritanceMarginItem> items)
        {
            var itemsByLineNumber = items
                .GroupBy(item => item.LineNumber);
            var documentInfo = new Dictionary<int, MarginInfo>();
            foreach (var grouping in itemsByLineNumber)
            {
                documentInfo[grouping.Key] = CreateMarginInfo(grouping.Select(i => i).ToImmutableArray());
            }

            return new DocumentMarginInfo(documentInfo);
        }

        internal static MarginInfo CreateMarginInfo(ImmutableArray<InheritanceMarginItem> items)
        {
            return new MarginInfo(items.SelectAsArray(CreateMemberInfo));
        }

        internal static MemberInfo CreateMemberInfo(InheritanceMarginItem item)
        {
            var infoDictionary = item.TargetItems.GroupBy(target => target.RelationToMember)
                .ToDictionary(
                keySelector: grouping => grouping.Key,
                elementSelector: grouping => CreateTargetInfo(grouping.ToImmutableArray()));
            return new MemberInfo(item.Glyph, infoDictionary);
        }

        internal static ImmutableArray<TargetInfo> CreateTargetInfo(ImmutableArray<InheritanceTargetItem> items)
        {
            return Enumerable.Repeat(new TargetInfo(), items.Length).ToImmutableArray();
        }
    }

    internal class ProjectMarginInfo
    {
        public Dictionary<Document, DocumentMarginInfo> DocumentInfos { get; }
        public int MarginCountInTheProject => DocumentInfos.Values.Select(documentInfo => documentInfo.MarginInThisDocument).Count();

        public ProjectMarginInfo(Dictionary<Document, DocumentMarginInfo> values)
        {
            this.DocumentInfos = values;
        }
    }

    internal class DocumentMarginInfo
    {
        public Dictionary<int, MarginInfo> MarginInTheDocument { get; }
        public int MarginInThisDocument => MarginInTheDocument.Count;
        public ImmutableArray<MarginInfo> MarginContainsOneMember => MarginInTheDocument.Values
            .Where(marginInfo => marginInfo.MembersOnThisLine.Length == 1).ToImmutableArray();

        public ImmutableArray<MarginInfo> MarginContainsTwoMembers => MarginInTheDocument.Values
            .Where(marginInfo => marginInfo.MembersOnThisLine.Length == 2).ToImmutableArray();

        public ImmutableArray<MarginInfo> MarginContainsThreeOrMoreMembers => MarginInTheDocument.Values
            .Where(marginInfo => marginInfo.MembersOnThisLine.Length > 2).ToImmutableArray();

        public DocumentMarginInfo(Dictionary<int, MarginInfo> values)
        {
            MarginInTheDocument = values;
        }
    }

    internal class MarginInfo
    {
        public ImmutableArray<MemberInfo> MembersOnThisLine { get; }

        public MarginInfo(ImmutableArray<MemberInfo> membersOnThisLine)
        {
            MembersOnThisLine = membersOnThisLine;
        }
    }

    internal class MemberInfo
    {
        public Glyph MemberKind { get; }
        public Dictionary<InheritanceRelationship, ImmutableArray<TargetInfo>> TargetInfo { get; }
        public int TotalTargetCount => TargetInfo.SelectMany(kvp => kvp.Value).Count();

        public Dictionary<InheritanceRelationship, int> CountByRelationship => TargetInfo.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Length);

        public MemberInfo(
            Glyph memberKind,
            Dictionary<InheritanceRelationship, ImmutableArray<TargetInfo>> targetInfo)
        {
            MemberKind = memberKind;
            TargetInfo = targetInfo;
        }
    }

    internal class TargetInfo
    {
    }
}

#endif
