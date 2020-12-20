// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    }
}
