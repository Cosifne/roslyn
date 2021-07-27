// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.InheritanceMargin;

namespace AnalyzerRunner
{
    public class InheritanceMarginRunner
    {
        private readonly Workspace _workspace;

        public InheritanceMarginRunner(Workspace workspace)
        {
            _workspace = workspace;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var allProjects = _workspace.CurrentSolution.Projects;
            var collector = new Dictionary<Project, string>();

            foreach (var project in allProjects)
            {
                var allDocuments = project.Documents.ToArray();
                foreach (var document in allDocuments)
                {
                    var inheritanceMarginService = document.GetLanguageService<IInheritanceMarginService>();
                }
            }
        }
    }
}
