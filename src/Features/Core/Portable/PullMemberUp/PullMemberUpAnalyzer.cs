﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PullMemberUp;

namespace Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp
{
    internal class PullMembersUpAnalysisBuilder
    {
<<<<<<< HEAD
        internal static PullMembersUpAnalysisResult BuildAnalysisResult(
            INamedTypeSymbol destination,
            ImmutableArray<ISymbol> members)
        {
            var membersAnalysisResult = members.SelectAsArray(member =>
=======
        internal static AnalysisResult BuildAnalysisResult(
            INamedTypeSymbol targetSymbol,
            IEnumerable<(ISymbol member, bool makeAbstract)> selectedMembersAndOption)
        {
            var memberResult = selectedMembersAndOption.Select(selection =>
>>>>>>> Resolve comments
            {
                if (destination.TypeKind == TypeKind.Interface)
                {
<<<<<<< HEAD
                    var changeOriginalToPublic = member.DeclaredAccessibility != Accessibility.Public;
                    var changeOriginalToNonStatic = member.IsStatic;
                    return new MemberAnalysisResult(member, changeOriginalToPublic, changeOriginalToNonStatic);
=======
                    return new MemberAnalysisResult(
                        selection.member,
                        selection.member.DeclaredAccessibility != Accessibility.Public,
                        selection.member.IsStatic);
>>>>>>> Resolve comments
                }
                else
                {
                    return new MemberAnalysisResult(member, changeOriginalToPublic: false, changeOriginalToNonStatic: false);
                }
            });

            if (targetSymbol.TypeKind == TypeKind.Interface)
            {
                return new AnalysisResult(false, targetSymbol, memberResult);
            }
            else
            {
                var changeTargetToAbstract = 
                    !targetSymbol.IsAbstract &&
                    selectedMembersAndOption.Aggregate(false, (acc, selection) => acc || selection.member.IsAbstract || selection.makeAbstract);
                return new AnalysisResult(changeTargetToAbstract, targetSymbol, memberResult);
            }
        }
    }

    internal class MemberAnalysisResult
    {
        public ISymbol Member { get; }

        public bool ChangeOriginToPublic { get; }

        public bool ChangeOriginToNonStatic { get; }

        public bool MakeAbstract { get; }

        internal MemberAnalysisResult(ISymbol member, bool changeOriginToPublic = false, bool changeOriginToNonStatic = false, bool makeAbstract = false)
        {
            Member = member;
            ChangeOriginToPublic = changeOriginToPublic;
            ChangeOriginToNonStatic = changeOriginToNonStatic;
            MakeAbstract = makeAbstract;
        }
    }

    /// <summary>
    /// This is class contains all the operations to be done on members and target in order to pull members up to target
    /// </summary>
    internal class AnalysisResult
    {
        public bool ChangeTargetAbstract { get; }

        public INamedTypeSymbol Target { get; }

        public IEnumerable<MemberAnalysisResult> MembersAnalysisResults { get; }

        public bool IsValid { get; }

        internal AnalysisResult(
            bool changeTargetAbstract,
            INamedTypeSymbol target,
            IEnumerable<MemberAnalysisResult> membersAnalysisResults)
        {
            ChangeTargetAbstract = changeTargetAbstract;
            Target = target;
            MembersAnalysisResults = membersAnalysisResults;
            IsValid = !MembersAnalysisResults.Aggregate(
                ChangeTargetAbstract,
                (acc, result) => acc || result.ChangeOriginToPublic || result.ChangeOriginToNonStatic || result.MakeAbstract);
        }
    }
}
