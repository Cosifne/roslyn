﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.  

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.CodeRefactorings.PullMemberUp.Dialog
{
    internal interface IPullMemberUpOptionsService : IWorkspaceService
    {
        PullMemberDialogResult GetPullTargetAndMembers(ISymbol selectedNodeSymbol, IEnumerable<ISymbol> members, Dictionary<ISymbol, Lazy<ImmutableList<ISymbol>>> lazyDependentsMap);
    }
}
