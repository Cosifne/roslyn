// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.LanguageServices.Implementation.MoveStaticMembers;
using Roslyn.Utilities;
using Xunit;

namespace Roslyn.VisualStudio.Next.UnitTests.Options
{
    public class UnifiedSettingsTests
    {
        private static readonly ImmutableArray<IPerLanguageValuedOption> s_perLanguageOptionsInAdvancedOptionPage = ImmutableArray.Create<IPerLanguageValuedOption>(
            SolutionCrawlerOptionsStorage.BackgroundAnalysisScopeOption,
            SolutionCrawlerOptionsStorage.CompilerDiagnosticsScopeOption);

        private static readonly ImmutableArray<IOption2> s_optionsInAdvancedPage = ImmutableArray.Create<IOption2>(
            RemoteHostOptionsStorage.OOP64Bit
         );

        [Theory]
        [InlineData("csharp")]
        [InlineData("visualBasic")]
        [InlineData("csharpAndvisualBasic")]
        public async Task TestAdvancedUnifiedSettings(string groupName)
        {
            var assembly = typeof(UnifiedSettingsTests).Assembly;
            var all = typeof(UnifiedSettingsTests).Assembly.GetManifestResourceNames();
            var registrationFileName = typeof(UnifiedSettingsTests).Assembly.GetManifestResourceNames().Single(name => name.EndsWith($"{groupName}AdvancedSettings.registration.json"));

            using var fileStream = assembly.GetManifestResourceStream(registrationFileName);
            using var jsonDocument = await JsonDocument.ParseAsync(fileStream).ConfigureAwait(false);

            var propertyNames = jsonDocument.RootElement.EnumerateObject().SelectAsArray(property => property.Name);

            Assert.Contains("properties", propertyNames);
            Assert.Contains(groupName switch
            {
                "csharp" => "textEditor.c#.advanced",
                "visualBasic" => "textEditor.visualBasic.advanced",
                "csharpAndVisualBasic" => "textEditor.c#AndVisualBasic.advanced",
                _ => throw ExceptionUtilities.UnexpectedValue(groupName)
            }, propertyNames);
        }

        //private static void VerifyPerLangaugeOptions(JsonDocument actualJsonDocument, string languageName, ImmutableArray<IPerLanguageValuedOption> expectedOptionsInSettings)
        //{
        //    foreach (var jsonProperty in actualJsonDocument.RootElement.EnumerateObject())
        //    {
        //        if (jsonProperty.Name is "properties")
        //        {
        //            VerifyProperties(jsonProperty.Value, CSharpAdvancedCatalogName, s_migratedOptionsInAdvancedOptionPage);
        //        }
        //        else if (jsonProperty.Name is CSharpAdvancedCatalogName)
        //        {
        //            VerifyCatalog(jsonProperty.Value, Guids.CSharpOptionPageAdvancedIdString);
        //        }
        //        else
        //        {
        //            Assert.True(false, "Unexpected element in the Unified Settings Json");
        //        }
        //    }
        //}

        private static void VerifyOptions(JsonDocument jsonDocument, ImmutableArray<IOption2> expectedOptionsInSettings)
        {

        }

        //private static void VerifyProperties(
        //    JsonElement jsonElement, string expectedCatalog)
        //{
        //    var expectedOptionsInSettingsDictionary = expectedOptionsInSettings.ToDictionary(
        //        keySelector: GetCamelCaseName,
        //        elementSelector: option => option);

        //    foreach (var actualProperty in jsonElement.EnumerateObject())
        //    {
        //        var propertyName = actualProperty.Name;
        //        Assert.StartsWith(expectedCatalog, propertyName);
        //        var propertyNameListedInRegistrationFile = propertyName[(propertyName.LastIndexOf(".") + 1)..];
        //        if (expectedOptionsInSettingsDictionary.TryGetValue(propertyNameListedInRegistrationFile, out var expectedOption))
        //        {
        //            VerifyOption(actualProperty.Value, expectedOption);
        //        }
        //        else
        //        {
        //            Contract.Fail($"Option in the unifiedSettings registration file is not tested. Please add the option to test list if you onboard new option to unifiedSettings. PropertyName: {propertyNameListedInRegistrationFile}.");
        //        }
        //    }
        //}

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
