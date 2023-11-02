// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.VisualStudio.LanguageServices;
using Roslyn.Utilities;
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
                    VerifyProperties(jsonProperty.Value, CSharpAdvancedCatalogName, s_migratedOptionsInAdvancedOptionPage);
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

        private static void VerifyProperties(
            JsonElement jsonElement, string expectedCatalog, ImmutableArray<IOption2> expectedOptionsInSettings)
        {
            var expectedOptionsInSettingsDictionary = expectedOptionsInSettings.ToDictionary(
                keySelector: GetCamelCaseName,
                elementSelector: option => option);

            foreach (var actualProperty in jsonElement.EnumerateObject())
            {
                var propertyName = actualProperty.Name;
                Assert.StartsWith(expectedCatalog, propertyName);
                var propertyNameListedInRegistrationFile = propertyName[(propertyName.LastIndexOf(".") + 1)..];
                if (expectedOptionsInSettingsDictionary.TryGetValue(propertyNameListedInRegistrationFile, out var expectedOption))
                {
                    VerifyOption(actualProperty.Value, expectedOption);
                }
                else
                {
                    Contract.Fail($"Option in the unifiedSettings registration file is not tested. Please add the option to test list if you onboard new option to unifiedSettings. PropertyName: {propertyNameListedInRegistrationFile}.");
                }
            }
        }

        private static void VerifyOption(JsonElement actualProperty, IOption2 expectedOption)
        {
            var actualTypeValue = actualProperty.GetProperty("type").ToString();
            var type = expectedOption.Definition.Type;

            if (type.IsEnum)
            {
                // Enum is a string in json.
                Assert.Equal("string", actualTypeValue);
                VerifyEnum(actualProperty, expectedOption);
            }

            if (type == typeof(int))
            {
                // TODO: When int is added
            }

            if (type == typeof(bool))
            {
                // TODO: When bool is added
            }

            if (type == typeof(string))
            {
                // TODO: When string is added
            }
        }

        private static void VerifyEnum(JsonElement actualProperty, IOption2 expectedOption)
        {
            var enumType = expectedOption.Definition.Type;
            var allEnumValues = enumType.GetEnumNames();
            var enumProperty = actualProperty.GetProperty("enum");
            // All string value in the json file should be in the Enum,
            // but some of the enum value might not be in the json.
            foreach (var actualValue in enumProperty.EnumerateArray())
            {
                Assert.Contains(actualValue.ToString(), allEnumValues);
            }
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

        private static string GetCamelCaseName(IOption2 option)
        {
            using var _ = PooledStringBuilder.GetInstance(out var builder);

            var names = option.Definition.ConfigName.Split('_');
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (i == 0)
                {
                    builder.Append(name.ToLower());
                }
                else
                {
                    builder.Append(char.ToUpper(name[0]));
                    builder.Append(name[1..].ToLower());
                }
            }

            return builder.ToString();
        }
    }
}
