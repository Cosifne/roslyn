// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Test.Utilities.PullMemberUp;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.PullMemberUp
{
    public abstract class CSharpPullMemberUpCodeActionTest : PullMemberUpCodeActionTest
    {
        protected override string GetLanguage() => LanguageNames.CSharp;

        protected override TestWorkspace CreateWorkspaceFromFile(string initialMarkup, TestParameters parameters)
            => TestWorkspace.CreateCSharp(initialMarkup, parameters.parseOptions, parameters.compilationOptions);

        protected override ParseOptions GetScriptOptions() => new CSharpParseOptions(kind: SourceCodeKind.Script);
    }
}
