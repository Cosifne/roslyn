// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CodeAnalysis.Editor.CSharp.BraceCompletion;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Test.Utilities.BraceCompletion;
using Microsoft.VisualStudio.Commanding;
using Roslyn.Test.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.BraceCompletion
{
    public class BraceCompletionCommandHandlerTests : AbstractBraceCompletionCommandHanderTest
    {
        protected override TestWorkspace CreateWorkspace(string code)
            => TestWorkspace.CreateCSharp(code);

        protected override ICommandHandler GetCommandHandler(TestWorkspace workspace)
        {
            var handlers = workspace.ExportProvider.GetExportedValues<ICommandHandler>();
            return handlers.OfType<BraceCompletionCommandHandler>().Single();
        }

        [WpfFact]
        public void TestEmptyClass()
        {
            Test(@"
public clas$$s Bar
", @"
public class Bar
{
    $$
}");
        }

        [WpfFact]
        public void TestMultipleClasses()
        {
            Test(@"
public class B$$ar2
public class Bar
{
}",
                @"
public class Bar2
{
    $$
}
public class Bar
{
}");
        }

        [WpfFact]
        public void TestNestedClasses()
        {
            Test(@"
public class Bar
{
    public class B$$ar2
}",
                @"
public class Bar
{
    public class Bar2
    {
        $$
    }
}");
        }

        [WpfFact]
        public void TestEmptyNamespace()
        {
            Test(@"
namespace Bar$$
", @"
namespace Bar
{
    $$
}");
        }

        [WpfFact]
        public void TestEmptyStruct()
        {
            Test(@"
public stru$$ct Bar
", @"
public struct Bar
{
    $$
}");
        }

        [WpfFact]
        public void TestEmptyRecord()
        {
            Test(@"
public reco$$rd Bar
", @"
public record Bar
{
    $$
}");
        }

        [WpfFact]
        public void TestMethod()
        {
            Test(@"
public class Bar
{
    void Ma$$in()
}",
                @"
public class Bar
{
    void Main()
    {
        $$
    }
}");

        }

        [WpfFact]
        public void TestLocalFunction()
        {
            Test(@"
public class Bar
{
    void Main()
    {
        void Loca$$l()
    }
}",
                @"
public class Bar
{
    void Main()
    {
        void Local()
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestGetAccessorForProperty()
        {
            Test(@"
public class Bar
{
    public int Foo
    {
        get$$
    }
}", @"
public class Bar
{
    public int Foo
    {
        get
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestSetAccessorForProperty()
        {
            Test(@"
public class Bar
{
    public int Foo
    {
        get$$
    }
}", @"
public class Bar
{
    public int Foo
    {
        get
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestAddAccessorForEvent()
        {
            Test(@"
using System;
public class Bar
{
    public event EventHandler A
    {
        add$$
    }
}", @"
using System;
public class Bar
{
    public event EventHandler A
    {
        add
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestRemoveAccessorForEvent()
        {
            Test(@"
using System;
public class Bar
{
    public event EventHandler A
    {
        add {}
        remove$$
    }
}", @"
using System;
public class Bar
{
    public event EventHandler A
    {
        add {}
        remove
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestGetAccessorForIndexer()
        {
            Test(@"
public class Bar
{
    public int this[int i]
    {
        get$$
    }
}", @"
public class Bar
{
    public int this[int i]
    {
        get
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestSetAccessorForIndexer()
        {
            Test(@"
public class Bar
{
    public int this[int i]
    {
        get => 1;
        set$$
    }
}", @"
public class Bar
{
    public int this[int i]
    {
        get => 1;
        set
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestEventDeclaration()
        {
            Test(@"
using System;
public class Bar
{
    public event EventHandler Foo$$
}", @"
using System;
public class Bar
{
    public event EventHandler Foo
    {
        $$
    }
}");
        }

        [WpfFact]
        public void TestIndexerDeclaration()
        {
            Test(@"
public class Bar
{
    public int thi$$s[int i]
}", @"
public class Bar
{
    public int this[int i]
    {
        $$
    }
}");
        }

        [WpfFact]
        public void TestAddBraceForObjectCreationExpression()
        {
            Test(@"
public class Bar
{
    public void M()
    {
        var f = new Foo()$$
    }
}
public class Foo
{
    public int HH { get; set; }
    public int PP { get; set; }
}",
                @"
public class Bar
{
    public void M()
    {
        var f = new Foo()
        {
            $$
        }
    }
}
public class Foo
{
    public int HH { get; set; }
    public int PP { get; set; }
}");
        }

        [WpfFact]
        public void TestRemoveBraceForObjectCreationExpression()
        {
            Test(@"
public class Bar
{
    public void M()
    {
        var f = new Foo() { HH = 1,$$ PP = 2 };
    }
}
public class Foo
{
    public int HH { get; set; }
    public int PP { get; set; }
}",
                @"
public class Bar
{
    public void M()
    {
        var f = new Foo();
    }
}
public class Foo
{
    public int HH { get; set; }
    public int PP { get; set; }
}");
        }

        [WpfFact]
        public void TestIfStatement()
        {
            Test(@"
public class Bar
{
    public void Main(bool x)
    {
        if$$ (x)
        var x = 1;
    }
}", @"
public class Bar
{
    public void Main(bool x)
    {
        if (x)
        {
            $$
        }
        var x = 1;
    }
}");
        }

        [WpfFact]
        public void TestDoStatement()
        {
            Test(@"
public class Bar
{
    public void Main()
    {
        do$$
    }
}", @"
public class Bar
{
    public void Main()
    {
        do
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestSingleElseStatement()
        {
            Test(@"
public class Bar
{
    public void Fo()
    {
        if (true)
        {
        }
        else$$
    }
}", @"
public class Bar
{
    public void Fo()
    {
        if (true)
        {
        }
        else
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestElseIfStatement()
        {
            Test(@"
public class Bar
{
    public void Fo()
    {
        if (true)
        {
        }
        e$$lse if (false)
    }
}", @"
public class Bar
{
    public void Fo()
    {
        if (true)
        {
        }
        else if (false)
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestForEachStatement()
        {
            Test(@"
public class Bar
{
    public void Fo()
    {
        foreach (var x $$in """")
    }
}", @"
public class Bar
{
    public void Fo()
    {
        foreach (var x in """")
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestLockStatement()
        {
            Test(@"
public class Bar
{
    object o = new object();
    public void Fo()
    {
        lock$$(o)
    }
}", @"
public class Bar
{
    object o = new object();
    public void Fo()
    {
        lock(o)
        {
            $$
        }
    }
}");
        }

        [WpfFact]
        public void TestUsingStatement()
        {
            Test(@"
using System;
public class Bar
{
    public void Fo()
    {
        usi$$ng (var d = new D())
    }
}
public class D : IDisposable
{
    public void Dispose()
    {}
}", @"
using System;
public class Bar
{
    public void Fo()
    {
        using (var d = new D())
        {
            $$
        }
    }
}
public class D : IDisposable
{
    public void Dispose()
    {}
}");
        }

        [WpfFact]
        public void TestWhileStatement()
        {
            Test(@"
public class Bar
{
    public void Fo()
    {
        while (tr$$ue)
    }
}", @"
public class Bar
{
    public void Fo()
    {
        while (true)
        {
            $$
        }
    }
}");
        }
    }
}
