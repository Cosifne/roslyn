// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PullMemberUp;

namespace Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp
{
    internal class PullMembersUpAnalysisBuilder
    {
        internal static PullMembersUpAnalysisResult BuildAnalysisResult(
            INamedTypeSymbol destination,
            ImmutableArray<ISymbol> members)
        {
            var membersAnalysisResult = members.SelectAsArray(member =>
            {
                if (destination.TypeKind == TypeKind.Interface)
                {
                    var changeOriginalToPublic = member.DeclaredAccessibility != Accessibility.Public;
                    var changeOriginalToNonStatic = member.IsStatic;
                    return new MemberAnalysisResult(member, changeOriginalToPublic, changeOriginalToNonStatic);
                }
                else
                {
                    return new MemberAnalysisResult(member, changeOriginalToPublic: false, changeOriginalToNonStatic: false);
                }
            });

<<<<<<< HEAD
            return new PullMembersUpAnalysisResult(destination, membersAnalysisResult);
=======
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

        internal MemberAnalysisResult(ISymbol member, bool changeOriginToPublic = false, bool changeOriginToNonStatic = false)
        {
            Member = member;
            ChangeOriginToPublic = changeOriginToPublic;
            ChangeOriginToNonStatic = changeOriginToNonStatic;
        }
    }

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
<<<<<<< HEAD
                (acc, result) => acc || result.ChangeOriginToNonPublic || result.ChangeOriginToNonStatic);
>>>>>>> Fix most UI issues
=======
                (acc, result) => acc || result.ChangeOriginToPublic || result.ChangeOriginToNonStatic);
>>>>>>> Make the UI can resize proper
        }
    }
}
