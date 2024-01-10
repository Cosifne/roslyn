// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace Microsoft.VisualStudio.LanguageServices.Options
{
    internal sealed class VisualStudioUnifiedSettingsOptionPersister
    {
        private readonly ISettingsManager _unifiedSettingsManager;

        public VisualStudioUnifiedSettingsOptionPersister(ISettingsManager unifiedSettingsManager)
        {
        }

        public Task PersistAsync(string path)
        {


        }
    }
}
