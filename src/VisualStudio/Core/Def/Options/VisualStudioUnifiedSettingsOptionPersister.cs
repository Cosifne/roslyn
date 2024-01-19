// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

extern alias InteractiveHost;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using InteractiveHost::Roslyn.Utilities;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace Microsoft.VisualStudio.LanguageServices.Options
{
    internal sealed class VisualStudioUnifiedSettingsOptionPersister
    {
        private readonly ISettingsManager _unifiedSettingsManager;
        private readonly ISettingsReader _settingsReader;
        private readonly ISettingsWriter _settingsWriter;

        public VisualStudioUnifiedSettingsOptionPersister(ISettingsManager unifiedSettingsManager)
        {
            _settingsReader = unifiedSettingsManager.GetReader();
            _settingsWriter = unifiedSettingsManager.GetWriter(nameof(VisualStudioUnifiedSettingsOptionPersister));
        }

        public bool TryFetch(string path, OptionKey2 optionKey, out object? value)
        {
            var optionType = optionKey.Option.Type;
            var isSingleValueType = optionType == typeof(bool) || optionType == typeof(string) || optionType == typeof(int) || optionType.IsEnum || optionType == typeof(long);
            if (isSingleValueType)
            {
               var settingRetrieval = _settingsReader.GetValue<object>(path);
               if (settingRetrieval.Outcome == SettingRetrievalOutcome.Success)
               {
                   value = settingRetrieval.Value;
                   return true;
               }
               else
               {
                   FatalError.ReportNonFatalError(
                       new RoslynUnifiedSettingsReadException(path, settingRetrieval.Outcome, settingRetrieval.Message));
               }
            }
            else
            {
                // Two reasons when this might get reached
                // 1. Option type onboarded to unified settings is not listed in the check above. Consider either add to single value type
                // or array type when onboard new options.
                // 2. Option type onboarded is a nullable type. This is not supported in unified settings. We use that in many place to indicate
                // the option is in experiment mode. In this case, change the type to non-nullable and use `alternateDefault` in registration file
                // to let unified settings fetch the value if it's in experimental mode.
                throw ExceptionUtilities.UnexpectedValue(optionType);
            }

            value = null;
            return false;
        }

        public Task PersistAsync(string path, OptionKey2 optionKey, object? value)
        {
        }

        private class RoslynUnifiedSettingsReadException(string optionPath, SettingRetrievalOutcome outcome, string errorMessage) : Exception
        {

        }
    }
}
