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
        private class PullMemberUpWithDialogCodeAction : CodeActionWithOptions
        {
            private readonly ISymbol _selectedNodeSymbol;

            private readonly Document _contextDocument;

            public override string Title => "A very cool name";

            internal PullMemberUpWithDialogCodeAction(
                Document document,
                ISymbol selectedNodeSymbol)
            {
                _contextDocument = document;
                _selectedNodeSymbol = selectedNodeSymbol;
            }

            public override object GetOptions(CancellationToken cancellationToken)
            {
                var pullMemberUpService = _contextDocument.Project.Solution.Workspace.Services.GetService<IPullMemberUpOptionsService>();
                return pullMemberUpService.GetPullTargetAndMembers(_selectedNodeSymbol);
            }
            
            protected async override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(object options, CancellationToken cancellationToken)
            {
                if (options is PullMembersUpAnalysisResult result)
                {
                    // TODO: Calculate changed solution and code action
                    return null;
                }
                else
                {
                    return new CodeActionOperation[0];
                }
            }
        }
    }
}
