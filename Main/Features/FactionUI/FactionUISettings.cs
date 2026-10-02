namespace TAOM.Features.FactionUI;

/// <summary>
/// The player's front-end toggles (#704). Kysaro's module read these from <c>mainmenu.json</c>;
/// merged into TAOM they are MCM settings, defaulting to what his file shipped.
/// </summary>
public sealed class FactionUISettings
{
    public static readonly FactionUISettings Default = new(
        customMainMenu: true,
        hideGameVersion: true,
        menuVideo: true,
        muteMenuMusic: true,
        customLoadingImages: true,
        skillIcons: true);

    public FactionUISettings(
        bool customMainMenu,
        bool hideGameVersion,
        bool menuVideo,
        bool muteMenuMusic,
        bool customLoadingImages,
        bool skillIcons,
        bool customFaceGen = true,
        bool customNarrativeStage = true,
        bool customReviewStage = true,
        bool customBannerEditor = true,
        bool customClanNaming = true,
        bool customOptionsStage = true,
        bool factionScreen = true)
    {
        CustomMainMenu = customMainMenu;
        HideGameVersion = hideGameVersion;
        MenuVideo = menuVideo;
        MuteMenuMusic = muteMenuMusic;
        CustomLoadingImages = customLoadingImages;
        SkillIcons = skillIcons;
        CustomFaceGen = customFaceGen;
        CustomNarrativeStage = customNarrativeStage;
        CustomReviewStage = customReviewStage;
        CustomBannerEditor = customBannerEditor;
        CustomClanNaming = customClanNaming;
        CustomOptionsStage = customOptionsStage;
        FactionScreen = factionScreen;
    }

    public bool CustomMainMenu { get; }

    public bool HideGameVersion { get; }

    public bool MenuVideo { get; }

    /// <summary>Silences vanilla's menu theme on the main menu, where the menu video carries its own audio.</summary>
    public bool MuteMenuMusic { get; }

    public bool CustomLoadingImages { get; }

    /// <summary>Kysaro's skill icons in place of vanilla's, everywhere in the game. Read at each main
    /// menu: on applies there, off applies from the next launch, since vanilla's icons are not kept.</summary>
    public bool SkillIcons { get; }

    public bool CustomFaceGen { get; }

    /// <summary>The six backstory screens, which share one vanilla movie.</summary>
    public bool CustomNarrativeStage { get; }

    public bool CustomReviewStage { get; }

    public bool CustomBannerEditor { get; }

    public bool CustomClanNaming { get; }

    public bool CustomOptionsStage { get; }

    /// <summary>Kysaro's faction and hero picker in place of TAOM's faction map. When it is shown it
    /// replaces Player Switcher for that character creation.</summary>
    public bool FactionScreen { get; }
}
