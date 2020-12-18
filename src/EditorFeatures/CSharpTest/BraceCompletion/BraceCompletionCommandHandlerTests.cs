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
        public void TestClass()
        {
            Test(@"
public class Bar$$
", @"
public class Bar
{
    $$
}");
        }
    }
}
