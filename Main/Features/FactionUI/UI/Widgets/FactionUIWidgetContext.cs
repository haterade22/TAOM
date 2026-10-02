using TAOM.Features.FactionUI.CharacterCreation;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>
/// What the themed screens' custom widgets need from TAOM's services (#704). Gauntlet builds widgets by
/// reflection with only a <c>UIContext</c>, so they cannot take constructor dependencies; this is set
/// once from <c>SubModule.OnSubModuleLoad</c>. Null leaves a widget doing nothing extra.
/// </summary>
public static class FactionUIWidgetContext
{
    internal static NarrativeThemeIconMap? ThemeIcons { get; private set; }

    public static void Initialize(NarrativeThemeIconMap themeIcons) => ThemeIcons = themeIcons;
}
