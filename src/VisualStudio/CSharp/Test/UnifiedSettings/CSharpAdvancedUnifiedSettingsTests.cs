// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.InlineDiagnostics;
using Microsoft.CodeAnalysis.Editor.Shared.Options;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.LanguageServices.UnitTests.UnifiedSettings;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Roslyn.VisualStudio.CSharp.UnitTests.UnifiedSettings
{
    public class CSharpAdvancedUnifiedSettingsTests : UnifiedSettingsTests
    {
        internal override ImmutableArray<(string unifiedSettingsPath, IOption2 roslynOption)> OnboardedOptions
            => [("textEditor.csharp.advanced.analysis.analyzerDiagnosticsScope", SolutionCrawlerOptionsStorage.BackgroundAnalysisScopeOption),
                ("textEditor.csharp.advanced.analysis.compilerDiagnosticsScope", SolutionCrawlerOptionsStorage.CompilerDiagnosticsScopeOption),
                ("textEditor.csharp.advanced.analysis.enableInlineDiagnostics", InlineDiagnosticsOptionsStorage.EnableInlineDiagnostics),
                ("textEditor.csharp.advanced.analysis.inlineDiagnosticsLocation", InlineDiagnosticsOptionsStorage.Location),
                ("textEditor.csharpAndVisualBasic.advanced.analysis.codeAnalysisInSeparateProcess", RemoteHostOptionsStorage.OOP64Bit),
                ("textEditor.csharpAndVisualBasic.advanced.analysis.reloadChangedAnalyzerReferences", WorkspaceConfigurationOptionsStorage.ReloadChangedAnalyzerReferences),
                ("textEditor.csharpAndVisualBasic.advanced.analysis.offerRemoveUnusedReferences", FeatureOnOffOptions.OfferRemoveUnusedReferences),
                ("textEditor.csharpAndVisualBasic.advanced.analysis.enableFileLoggingForDiagnostics", VisualStudioLoggingOptionsStorage.EnableFileLoggingForDiagnostics),
                ("textEditor.csharpAndVisualBasic.advanced.analysis.skipAnalyzersForImplicitlyTriggeredBuilds", FeatureOnOffOptions.SkipAnalyzersForImplicitlyTriggeredBuilds)];

        internal override object[] GetEnumOptionValues(IOption2 option)
        {
            if (option.Equals(SolutionCrawlerOptionsStorage.BackgroundAnalysisScopeOption))
            {
                // BackgroundAnalysisScope contains six enum value but we only show four in option page
                return [BackgroundAnalysisScope.VisibleFilesAndOpenFilesWithPreviouslyReportedDiagnostics,
                    BackgroundAnalysisScope.OpenFiles,
                    BackgroundAnalysisScope.FullSolution,
                    BackgroundAnalysisScope.Minimal];
            }

            return base.GetEnumOptionValues(option);
        }

        [Fact]
        public async Task AdvancedPageTest()
        {
            using var registrationFileStream = typeof(CSharpIntellisenseUnifiedSettingsTests).GetTypeInfo().Assembly.GetManifestResourceStream("Roslyn.VisualStudio.CSharp.UnitTests.csharpSettings.registration.json");
            using var reader = new StreamReader(registrationFileStream);
            var registrationFile = await reader.ReadToEndAsync().ConfigureAwait(false);
            var registrationJsonObject = JObject.Parse(registrationFile, new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });

            var optionPageId = registrationJsonObject.SelectToken("$.categories['textEditor.csharp.advanced'].legacyOptionPageId");
            Assert.Equal(Guids.CSharpOptionPageAdvancedIdString, optionPageId!.ToString());

            using var pkgdefFileStream = typeof(CSharpIntellisenseUnifiedSettingsTests).GetTypeInfo().Assembly.GetManifestResourceStream("Roslyn.VisualStudio.CSharp.UnitTests.PackageRegistration.pkgdef");
            using var pkgdefReader = new StreamReader(pkgdefFileStream);
            var pkgdefFile = await pkgdefReader.ReadToEndAsync().ConfigureAwait(false);
            TestUnifiedSettingsCategory(
                registrationJsonObject,
                categoryBasePaths: ["textEditor.csharp.advanced", "textEditor.csharpAndVisualBasic.advanced"],
                languageName: LanguageNames.CSharp,
                pkgdefFile);
        }
    }
}
