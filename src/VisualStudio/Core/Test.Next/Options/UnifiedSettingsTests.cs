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
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.CodeAnalysis.UnitTests;
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

        private static readonly ImmutableArray<IOption2> s_optionsSharedAdvancedPage = ImmutableArray.Create<IOption2>(
            RemoteHostOptionsStorage.OOP64Bit);

        [Theory]
        [InlineData("csharp")]
        [InlineData("visualBasic")]
        [InlineData("csharpAndVisualBasic")]
        public async Task TestAdvancedUnifiedSettings(string languageName)
        {
            var assembly = typeof(UnifiedSettingsTests).Assembly;
            var registrationFileName = typeof(UnifiedSettingsTests).Assembly.GetManifestResourceNames().Single(name => name.EndsWith($"{languageName}AdvancedSettings.registration.json"));

            using var fileStream = assembly.GetManifestResourceStream(registrationFileName);
            using var jsonDocument = await JsonDocument.ParseAsync(fileStream).ConfigureAwait(false);

            var propertyNames = jsonDocument.RootElement.EnumerateObject().SelectAsArray(property => property.Name);
            var optionGroup = GetGroupName(languageName);
            Assert.Contains(optionGroup, propertyNames);
            Assert.Contains("properties", propertyNames);

            if (languageName is "csharp" or "visualBasic")
            {
                VerifyPerLanguageOptions(jsonDocument, languageName, s_perLanguageOptionsInAdvancedOptionPage);
            }
        }

        private static void VerifyPerLanguageOptions(JsonDocument actualJsonDocument, string languageName, ImmutableArray<IPerLanguageValuedOption> expectedOptionsInSettings)
        {
            var optionGroup = GetGroupName(languageName);
            var actualOptionInJson = actualJsonDocument.RootElement.GetProperty("properties").EnumerateObject();

            Assert.Equal(expectedOptionsInSettings.Length, actualOptionInJson.Count());
            foreach (var option in expectedOptionsInSettings)
            {
                // 1. Verify property name

            }
        }

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

        private static string GetGroupName(string languageName)
            => languageName switch
            {
                "csharp" => "textEditor.c#.advanced",
                "visualBasic" => "textEditor.visualBasic.advanced",
                "csharpAndVisualBasic" => "textEditor.c#AndVisualBasic.advanced",
                _ => throw ExceptionUtilities.UnexpectedValue(languageName)
            };

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
