using System;
using DryIoc;
using TAOM.Adapters;
using TAOM.Features.FactionMap.Hooks;
using TAOM.Features.FactionUI.CharacterCreation;
using TAOM.Features.FactionUI.FactionScreen;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.FactionUI.Resources;
using TAOM.Features.FactionUI.UI;

namespace TAOM.Features.FactionUI;

/// <summary>Kysaro's themed front end (#704): menu, splash, loading screens, the character-creation
/// screens, and the faction and hero picker.</summary>
public static class FactionUIIoC
{
    public static void RegisterFactionUIFeature(IContainer container)
    {
        container.Register<IFrontEndResourceAdapter, FrontEndResourceAdapter>(Reuse.Singleton);
        container.Register<IMenuMusicAdapter, MenuMusicAdapter>(Reuse.Singleton);
        container.Register<IFrontEndStateAdapter, FrontEndStateAdapter>(Reuse.Singleton);
        container.Register<ITextLocalizerAdapter, TextLocalizerAdapter>(Reuse.Singleton);
        container.Register<IPresetAppearanceAdapter, PresetAppearanceAdapter>(Reuse.Singleton);
        container.Register<IFactionRosterAdapter, FactionRosterAdapter>(Reuse.Singleton);

        container.Register<FactionUIPaths>(Reuse.Singleton);
        container.Register<FactionUISettingsProvider>(Reuse.Singleton);
        container.Register<FrontEndSpriteService>(Reuse.Singleton);
        container.Register<FrontEndMovieService>(Reuse.Singleton);
        container.RegisterDelegate(
            r => new MenuMediaService(
                r.Resolve<IFrontEndResourceAdapter>(),
                r.Resolve<FactionUIPaths>(),
                r.Resolve<FactionUISettingsProvider>(),
                new Random()),
            Reuse.Singleton);
        container.RegisterDelegate(
            r => new LoadingImageService(
                r.Resolve<FrontEndSpriteService>(),
                r.Resolve<IFrontEndResourceAdapter>(),
                r.Resolve<FactionUIPaths>(),
                r.Resolve<FactionUISettingsProvider>(),
                new Random()),
            Reuse.Singleton);

        container.Register<FaceGenCameraConfigProvider>(Reuse.Singleton);
        container.Register<FaceGenCameraService>(Reuse.Singleton);
        container.Register<NarrativeThemeIconMap>(Reuse.Singleton);
        container.Register<FactionPresetService>(Reuse.Singleton);
        container.Register<FactionScreenConfigProvider>(Reuse.Singleton);
        container.Register<FactionScreenCatalog>(Reuse.Singleton);
        container.Register<FactionRoster>(Reuse.Singleton);

        container.Register<MainMenuWidgets>(Reuse.Singleton);
        container.Register<CharacterCreationWidgets>(Reuse.Singleton);
        container.Register<FactionScreenWidgets>(Reuse.Singleton);
        container.Register<FrontEndScreenEffects>(Reuse.Singleton);
        // One launcher: FactionMap's culture stage reaches it through the seam, SubModule.OnGameEnd directly.
        container.Register<FactionScreenLauncher>(Reuse.Singleton);
        container.RegisterMapping<ICultureStageMovieOverride, FactionScreenLauncher>();
        container.Register<FactionUITicker>(Reuse.Singleton);
    }
}
