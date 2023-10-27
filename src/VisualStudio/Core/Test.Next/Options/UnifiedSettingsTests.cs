// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.PlatformUI;
using Xunit;

namespace Roslyn.VisualStudio.Next.UnitTests.Options
{
    public class UnifiedSettingsTests
    {
        private static readonly ImmutableArray<IOption2> s_migratedOptionsInAdvancedOptionPage = ImmutableArray.Create<IOption2>(
            SolutionCrawlerOptionsStorage.BackgroundAnalysisScopeOption,
            SolutionCrawlerOptionsStorage.CompilerDiagnosticsScopeOption);
        private const string CSharpAdvancedCatalogName = "textEditor.c#.advanced";

        [Fact]
        public async Task TestAdvancedUnifiedSettings()
        {
            var assembly = typeof(UnifiedSettingsTests).Assembly;
            var registrationFileName = typeof(UnifiedSettingsTests).Assembly.GetManifestResourceNames().Single(name => name.EndsWith("csharpAdvancedSettings.registration.json"));
            using var fileStream = assembly.GetManifestResourceStream(registrationFileName);
            using var document = await JsonDocument.ParseAsync(fileStream);
            foreach (var jsonProperty in document.RootElement.EnumerateObject())
            {
                if (jsonProperty.Name is "properties")
                {
                    VerifyProperties(jsonProperty.Value, CSharpAdvancedCatalogName, s_migratedOptionsInAdvancedOptionPage));
                }
                else if (jsonProperty.Name is CSharpAdvancedCatalogName)
                {
                    VerifyCatalog(jsonProperty.Value, Guids.CSharpOptionPageAdvancedIdString);
                }
                else
                {
                    Assert.True(false, "Unexpected element in the Unified Settings Json");
                }
            }
        }

        private static void VerifyProperties(JsonElement jsonElement, string expectedCatalog, ImmutableArray<IOption2> expectedOptions)
        {
            var allSettingsInJson = jsonElement.EnumerateObject();

        }

        private static void VerifyCatalog(JsonElement unifiedSettingCatalog, string legacyOptionPageId)
        {
            if (unifiedSettingCatalog.TryGetProperty("legacyOptionPageId", out var value))
            {
                Assert.Equal(legacyOptionPageId, value.ToString());
            }
            else
            {
                Assert.True(false, "Unexpected element in the Unified Settings Json");
            }
        }
    }
}
