// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp.Dialog;
using Microsoft.CodeAnalysis.PullMemberUp;

namespace Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp
{
    internal abstract partial class AbstractPullMemberUpRefactoringProvider
    {
        internal class PullMemberUpWithDialogCodeAction : CodeActionWithOptions
        {
            private readonly ISymbol _selectedMember;

            private readonly Document _document;

            public override string Title => "A very cool name tbd";

            internal PullMemberUpWithDialogCodeAction(
                Document document,
                ISymbol selectedMember)
            {
                _document = document;
                _selectedMember = selectedMember;
            }

            public override object GetOptions(CancellationToken cancellationToken)
            {
                var pullMemberUpOptionService = _document.Project.Solution.Workspace.Services.GetService<IPullMemberUpOptionsService>();
                return pullMemberUpOptionService.GetPullMemberUpAnalysisResultFromDialogBox(_selectedMember, _document);
            }
            
            protected async override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(object options, CancellationToken cancellationToken)
            {
                if (options is PullMembersUpAnalysisResult result)
                {
                    var changedSolution = await MembersPuller.Instance.PullMembersUpAsync(result, _document, cancellationToken);
                    return new CodeActionOperation[1] { new ApplyChangesOperation(changedSolution) };
                }
                else
                {
                    return new CodeActionOperation[0];
                }
            }
        }
    }
}
