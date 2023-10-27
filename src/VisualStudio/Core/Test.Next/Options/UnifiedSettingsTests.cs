// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Xunit;

namespace Roslyn.VisualStudio.Next.UnitTests.Options
{
    public class UnifiedSettingsTests
    {
        private readonly ImmutableArray<IOption2> _migratedOptionsInAdvancedOptionPage = ImmutableArray.Create<IOption2>(
            SolutionCrawlerOptionsStorage.BackgroundAnalysisScopeOption,
            SolutionCrawlerOptionsStorage.CompilerDiagnosticsScopeOption);

        [Fact]
        public async Task TestAdvancedUnifiedSettings()
        {
            var assembly = typeof(UnifiedSettingsTests).Assembly;
            var registrationFileName = typeof(UnifiedSettingsTests).Assembly.GetManifestResourceNames().Single(name => name.EndsWith("csharpAdvancedSettings.registration.json"));
            using var fileStream = assembly.GetManifestResourceStream(registrationFileName);
            var reader = new StreamReader(fileStream);
            var file = await reader.ReadToEndAsync();
        }
    }
}
