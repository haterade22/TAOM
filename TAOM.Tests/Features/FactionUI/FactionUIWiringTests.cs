using System;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Hooks;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.CharacterCreation;
using TAOM.Features.FactionUI.FactionScreen;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.FactionUI.Resources;
using TAOM.Features.FactionUI.UI;
using TAOM.Features.PlayerSwitcher;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Resolves the feature through a real DryIoc container (the PlayerSwitcherWiringTests
/// precedent). SubModule resolves these services inside a try block that keeps the vanilla menus, so
/// a registration gap would not crash anything: it would quietly switch the whole front end off. The
/// engine adapters are substitutes; this test is about the container, not the engine.
/// </summary>
[TestClass]
public class FactionUIWiringTests
{
    private static IContainer NewContainer()
    {
        var container = new Container();
        FactionUIIoC.RegisterFactionUIFeature(container);

        // Registrations other features own.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IFactionSelectionService>());
        container.RegisterInstance(Substitute.For<IPlayerSwitchPolicyProvider>());
        container.RegisterInstance(Substitute.For<INarrativeDataProvider>());

        container.RegisterInstance(Substitute.For<IFrontEndResourceAdapter>(), IfAlreadyRegistered.Replace);
        container.RegisterInstance(Substitute.For<IMenuMusicAdapter>(), IfAlreadyRegistered.Replace);
        container.RegisterInstance(Substitute.For<IFrontEndStateAdapter>(), IfAlreadyRegistered.Replace);
        container.RegisterInstance(Substitute.For<ITextLocalizerAdapter>(), IfAlreadyRegistered.Replace);
        container.RegisterInstance(Substitute.For<IPresetAppearanceAdapter>(), IfAlreadyRegistered.Replace);
        container.RegisterInstance(Substitute.For<IFactionRosterAdapter>(), IfAlreadyRegistered.Replace);
        return container;
    }

    [TestMethod]
    public void TheFeatureRegistration_RegistersEveryAdapterItsServicesTake()
    {
        // NewContainer replaces the adapters with substitutes, which would hide a missing registration.
        var container = new Container();
        FactionUIIoC.RegisterFactionUIFeature(container);

        foreach (var adapter in new[]
                 {
                     typeof(IFrontEndResourceAdapter), typeof(IMenuMusicAdapter), typeof(IFrontEndStateAdapter),
                     typeof(ITextLocalizerAdapter), typeof(IPresetAppearanceAdapter), typeof(IFactionRosterAdapter),
                 })
        {
            Assert.IsTrue(container.IsRegistered(adapter),
                adapter.Name + " is not registered: SubModule's FactionUI init would throw and keep the vanilla menus");
        }
    }

    [TestMethod]
    public void IoC_RegistersPlayerSwitcherAndTheFactionUIBeforeFactionMap()
    {
        // FactionMapIoC resolves its culture-stage hook while registering; the hook takes the faction
        // screen's launcher, which takes Player Switcher's policy provider. Registered after it, launch throws.
        var code = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        var factionMap = code.IndexOf("FactionMapIoC.RegisterFactionMapFeature(container);", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, factionMap, "FactionMap's registration call was not found in IoC.cs");

        foreach (var earlier in new[] { "RegisterPlayerSwitcherFeature(container);", "FactionUIIoC.RegisterFactionUIFeature(container);" })
        {
            var at = code.IndexOf(earlier, StringComparison.Ordinal);
            Assert.IsTrue(at >= 0 && at < factionMap, earlier + " must come before FactionMap's registration in IoC.cs");
        }
    }

    [TestMethod]
    public void EveryServiceSubModuleResolves_IsBuiltFromTheFeatureRegistration()
    {
        var container = NewContainer();

        // The Patch95 initialization in SubModule.OnSubModuleLoad, and its OnGameEnd reset.
        Assert.IsNotNull(container.Resolve<FrontEndMovieService>());
        Assert.IsNotNull(container.Resolve<FrontEndSpriteService>());
        Assert.IsNotNull(container.Resolve<MenuMediaService>());
        Assert.IsNotNull(container.Resolve<LoadingImageService>());
        Assert.IsNotNull(container.Resolve<FrontEndScreenEffects>());
        Assert.IsNotNull(container.Resolve<FaceGenCameraService>());
        Assert.IsNotNull(container.Resolve<FactionPresetService>());
        Assert.IsNotNull(container.Resolve<NarrativeThemeIconMap>());
        Assert.IsNotNull(container.Resolve<FactionUITicker>());
        Assert.IsNotNull(container.Resolve<FactionScreenLauncher>());
    }

    [TestMethod]
    public void TheCultureStageSeamAndTheGameEndReset_ShareOneLauncher()
    {
        var container = NewContainer();

        Assert.AreSame<object>(container.Resolve<FactionScreenLauncher>(), container.Resolve<ICultureStageMovieOverride>(),
            "the culture stage opens the screen through the seam and OnGameEnd resets the launcher directly; " +
            "two instances means the reset clears a launcher nobody showed");
    }

    [TestMethod]
    public void ThePatchesAndTheTicker_ShareOneSpriteService()
    {
        // The movie service holds an image group and the ticker releases it: on two sprite services the
        // release would run against a service that never loaded anything.
        var container = NewContainer();

        Assert.AreSame(container.Resolve<FrontEndSpriteService>(), container.Resolve<FrontEndSpriteService>());
        Assert.AreSame(container.Resolve<FactionPresetService>(), container.Resolve<FactionPresetService>(),
            "the faction screen records the pick and the face generator applies it: one service");
    }
}
