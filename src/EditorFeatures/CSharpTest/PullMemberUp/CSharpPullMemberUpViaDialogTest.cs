using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.Test.Utilities.PullMemberUp;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.PullMemberUp
{
    public class CSharpPullMemberUpViaDialogTest : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, TestParameters parameters)
        {
            throw new System.NotImplementedException();
        }

        internal Task TestWithPullMemberDialogAsync(
            string initialMarkUp,
            string expectedResult,
            IEnumerable<(string, bool)> selection = null,
            string target = null,
            int index = 0,
            CodeActionPriority? priority = null,
            TestParameters parameters = default)
        {
            var service = new TestPullMemberUpService(selection, target);

            return TestInRegularAndScript1Async(
                initialMarkUp, expectedResult,
                index, priority,
                parameters.WithFixProviderData(service));
        }
    }
}
