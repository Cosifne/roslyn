' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Options

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests.UnifiedSettings
    ''' <summary>
    ''' Helper class to record all the unified settings information in unit test
    ''' </summary>
    Friend Class UnifiedSettingsOption

        ''' <summary>
        ''' JSON path of the setting in registration.json.
        ''' </summary>
        Public Property UnifiedSettingsPath As String

        ''' <summary>
        ''' Onboarded Roslyn option.
        ''' </summary>
        Public Property RoslynOption As IOption2

        ''' <summary>
        ''' FeatureFlag option and the option value when the featureFlag is on, if the onboarded option is in experiment.
        ''' </summary>
        Public Property FeatureFlagSetting As FeatureFlagSetting

        ''' <summary>
        ''' If the option needs restart VS to take effect.
        ''' </summary>
        Public Property RequireRestart As Boolean

        ''' <summary>
        ''' Title of this onboarded option.
        ''' </summary>
        Public Property Title As String

        ''' <summary>
        ''' The description label of each enum value if the type of the option is enum.
        ''' </summary>
        Public Property EnumLabels As ImmutableArray(Of String)

        ''' <summary>
        ''' Extra messages shown in settings page.
        ''' </summary>
        Public Property Messages As ImmutableArray(Of String)

        ''' <summary>
        ''' Shown locations If the option is shared between VB and C#.
        ''' </summary>
        Public Property Placements As ImmutableArray(Of String)

        ''' <summary>
        ''' The enableWhen condition, when set to true, this onboarded option will be enabled in unifiedSettings page.
        ''' </summary>
        Public Property EnabledWhenOptionIsTrue As IOption2

        Public Sub New(unifiedSettingsPath As String, roslynOption As IOption2, featureFlagSetting As FeatureFlagSetting, requireRestart As Boolean, title As String, enumLabels As ImmutableArray(Of String), messages As ImmutableArray(Of String), placements As ImmutableArray(Of String), enabledWhenOptionIsTrue As IOption2)
            Me.UnifiedSettingsPath = unifiedSettingsPath
            Me.RoslynOption = roslynOption
            Me.FeatureFlagSetting = featureFlagSetting
            Me.RequireRestart = requireRestart
            Me.Title = title
            Me.EnumLabels = enumLabels
            Me.Messages = messages
            Me.Placements = placements
            Me.EnabledWhenOptionIsTrue = enabledWhenOptionIsTrue
        End Sub
    End Class
End Namespace
