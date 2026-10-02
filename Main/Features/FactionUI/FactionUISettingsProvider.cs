namespace TAOM.Features.FactionUI;

/// <summary>
/// The only place the front-end toggles are read from MCM (#704). MCM builds its settings after every
/// module's <c>OnSubModuleLoad</c>, so an early read falls back to the compiled defaults. The read sits
/// behind a virtual so the services that consult it are testable offline (the house pattern of
/// <c>PlayerSwitchPolicyProvider</c>).
/// </summary>
public class FactionUISettingsProvider
{
    public FactionUISettings Current => ReadSettings();

    protected virtual FactionUISettings ReadSettings()
    {
        var settings = TaomSettings.Instance;
        return settings == null ? FactionUISettings.Default : From(settings);
    }

    /// <summary>Thirteen booleans of one type: named so a swapped pair cannot compile unnoticed.</summary>
    internal static FactionUISettings From(TaomSettings settings) => new(
        customMainMenu: settings.FrontEndCustomMainMenu,
        hideGameVersion: settings.FrontEndHideGameVersion,
        menuVideo: settings.FrontEndMenuVideo,
        muteMenuMusic: settings.FrontEndMuteMenuMusic,
        customLoadingImages: settings.FrontEndCustomLoadingImages,
        skillIcons: settings.FrontEndSkillIcons,
        customFaceGen: settings.FrontEndCustomFaceGen,
        customNarrativeStage: settings.FrontEndCustomNarrativeStage,
        customReviewStage: settings.FrontEndCustomReviewStage,
        customBannerEditor: settings.FrontEndCustomBannerEditor,
        customClanNaming: settings.FrontEndCustomClanNaming,
        customOptionsStage: settings.FrontEndCustomOptionsStage,
        factionScreen: settings.FrontEndFactionScreen);
}
