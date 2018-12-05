using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp.Dialog;
using Microsoft.CodeAnalysis.CSharp.CodeRefactorings.PullMemberUp;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities.PullMemberUp;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.PullMemberUp
{
    public class CSharpPullMemberUpViaDialogTest : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, TestParameters parameters) =>
            new CSharpPullMemberUpCodeRefactoringProvider(parameters.fixProviderData as IPullMemberUpOptionsService);

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

        #region destination interface
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullPartialMethodUpToInterfaceViaDialog()
        {
            var testText = @"
using System;
namespace PushUpTest
{
    partial interface IInterface
    {
    }

    public partial class TestClass : IInterface
    {
        partial void Bar[||]Bar()
    }

    public partial class TestClass
    {
        partial void BarBar()
        {}
    }

    partial interface IInterface
    {
    }
}";
            var expected = @"
using System;
namespace PushUpTest
{
    partial interface IInterface
    {
        void BarBar();
    }

    public partial class TestClass : IInterface
    {
        void BarBar()
    }

    public partial class TestClass
    {
        partial void BarBar()
        {}
    }

    partial interface IInterface
    {
    }
}";

            await TestWithPullMemberDialogAsync(testText, expected);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullMultipleNonPublicMethodsToInterfaceViaDialog()
        {
            var testText = @"
using System;
namespace PushUpTest
{
    interface IInterface
    {
    }

    public class TestClass : IInterface
    {
        public void TestMethod()
        {
            System.Console.WriteLine(""Hello World"");
        }

        protected void F[||]oo(int i)
        {
            // do awesome things
        }

        private string Bar(string x)
        {}
    }
}";
            var expected = @"
using System;
namespace PushUpTest
{
    interface IInterface
    {
        string Bar(string x);
        void Foo(int i);
        void TestMethod();
    }

    public class TestClass : IInterface
    {
        public void TestMethod()
        {
            System.Console.WriteLine(""Hello World"");
        }

        public void Foo(int i)
        {
            // do awesome things
        }

        public string Bar(string x)
        {}
    }
}";
            await TestWithPullMemberDialogAsync(testText, expected);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullMultipleNonPublicEventsToInterface()
        {
            var testText = @"
using System;
namespace PushUpTest
{
    interface IInterface
    {
    }

    public class TestClass : IInterface
    {
        private event EventHandler Event1, Eve[||]nt2, Event3;
    }
}";

            var expected = @"
using System;
namespace PushUpTest
{
    interface IInterface
    {
        event EventHandler Event1;
        event EventHandler Event2;
        event EventHandler Event3;
    }

    public class TestClass : IInterface
    {
        public event EventHandler Event1;
        public event EventHandler Event2;
        public event EventHandler Event3;
    }
}";
            await TestWithPullMemberDialogAsync(testText, expected);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullDifferentMembersFromClassToPartialInterfaceViaDialog()
        {
            var testText = @"
using System;
namespace PushUpTest
{
    partial interface IInterface
    {
    }

    public class TestClass : IInterface
    {
        public int th[||]is[int i]
        {
            get => j = value;
        }

        private void BarBar()
        {}
        
        protected static event EventHandler event1, event2;

        internal static int Foo
        {
            get; set;
        }
    }
    partial interface IInterface
    {
    }
}";

    var expected = @"
using System;
namespace PushUpTest
{
    partial interface IInterface
    {
        int this[int i] { get; }

        int Foo { get; set; }

        event EventHandler event1;

        event EventHandler event2;

        void BarBar();
    }

    public class TestClass : IInterface
    {
        public int this[int i]
        {
            get => j = value;
        }

        public void BarBar()
        {}

        public event EventHandler event1;

        public event EventHandler event2;

        public int Foo
        {
            get; set;
        }
    }
    partial interface IInterface
    {
    }
}";
            await TestWithPullMemberDialogAsync(testText, expected, index : 1);
        }

        #endregion destination interface

        #region destination class

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullMethodWithAbstractOptionToClass()
        {
            var testText = @"
namespace PushUpTest
{
    public class Base
    {
    }

    public class TestClass : Base
    {
        public void TestMeth[||]od()
        {
            System.Console.WriteLine(""Hello World"");
        }
    }
}";

            var expected = @"
namespace PushUpTest
{
    public abstract class Base
    {
        public abstract void TestMethod();
    }

    public class TestClass : Base
    {
        public void TestMeth[||]od()
        {
            System.Console.WriteLine(""Hello World"");
        }
    }
}";
            await TestWithPullMemberDialogAsync(testText, expected, new (string, bool)[] { ("TestMethod", true) }, index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullAbstractMethodToClass()
        {
            var testText = @"
namespace PushUpTest
{
    public class Base
    {
    }

    public abstract class TestClass : Base
    {
        public abstract void TestMeth[||]od();
    }
}";

            var expected = @"
namespace PushUpTest
{
    public abstract class Base
    {
        public abstract void TestMethod();
    }

    public abstract class TestClass : Base
    {
        public abstract void TestMethod();
    }
}";
            await TestWithPullMemberDialogAsync(testText, expected, new (string, bool)[] { ("TestMethod", true) }, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullMultipleEventsToClass()
        {
            var testText = @"
using System;
namespace PushUpTest
{
    public class Base2
    {
    }

    public class Testclass2 : Base2
    {
        private static event EventHandler Event1, Eve[||]nt3, Event4;
    }
}";
            var expected = @"
using System;
namespace PushUpTest
{
    public class Base2
    {
        private static event EventHandler Event1;
        private static event EventHandler Event3;
        private static event EventHandler Event4;
    }

    public class Testclass2 : Base2
    {
    }
}";
            await TestWithPullMemberDialogAsync(testText, expected, index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsPullMemberUp)]
        public async Task PullAbstractEventToClass()
        {
            var testText = @"
using System;
namespace PushUpTest
{
    public class Base2
    {
    }

    public abstract class Testclass2 : Base2
    {
        private abstract static event EventHandler Event1, Eve[||]nt3, Event4;
    }
}";
            var expected = @"
using System;
namespace PushUpTest
{
    public abstract class Base2
    {
        private static abstract event EventHandler Event3;
    }

    public abstract class Testclass2 : Base2
    {
        private abstract static event EventHandler Event1, Event4;
    }
}";

            await TestWithPullMemberDialogAsync(testText, expected, new (string, bool)[] { ("Event3", false) });
        }

        #endregion destination class
    }
}
