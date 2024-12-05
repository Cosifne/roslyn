' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.VisualStudio.LanguageServices.Options.VisualStudioOptionStorage

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests.UnifiedSettings
    Friend Class FeatureFlagSetting
        Public Property FeatureFlagOption As IOption2
        Public Property OptionValueWhenExperimentIsOn As String

        Public Sub New(featureFlagOption As IOption2, optionValueWhenExperimentIsOn As String)
            Me.FeatureFlagOption = featureFlagOption
            Me.OptionValueWhenExperimentIsOn = optionValueWhenExperimentIsOn
        End Sub

        Public Function GetStoragePath() As String
            Dim visualStudioStorage = Storages(FeatureFlagOption.Definition.ConfigName)
            Dim featureFlagStorage = DirectCast(visualStudioStorage, FeatureFlagStorage)
            Return featureFlagStorage.FlagName
        End Function
    End Class
End Namespace
