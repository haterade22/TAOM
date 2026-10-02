using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.FactionScreen;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.FactionUI.Resources;
using TAOM.Features.FactionUI.UI;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. When the faction screen takes the culture stage, and what happens to Player Switcher's
/// panel and to a pick around it. The paths proven here return before any screen is built.
/// </summary>
[TestClass]
public class FactionScreenLauncherTests
{
    private const string Root = @"C:\Game\Modules\TAOM";

    private sealed class StubSettings : FactionUISettingsProvider
    {
        public FactionUISettings Value { get; set; } = FactionUISettings.Default;

        protected override FactionUISettings ReadSettings() => Value;
    }

    private StubSettings _settings = null!;
    private FakeFrontEndResourceAdapter _files = null!;
    private IPlayerSwitchPolicyProvider _playerSwitcher = null!;
    private PlayerSwitchSessionStore _session = null!;
    private IHeroPickerService _heroes = null!;
    private IPresetAppearanceAdapter _appearance = null!;
    private FactionPresetService _presets = null!;
    private FactionPickService _picks = null!;
    private IModLogger _logger = null!;
    private FactionScreenLauncher _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        var paths = new FactionUIPaths(pathService);
        _files = new FakeFrontEndResourceAdapter();
        _logger = Substitute.For<IModLogger>();
        var config = new FactionScreenConfigProvider(_files, paths, _logger);
        var sprites = new FrontEndSpriteService(_files, paths, _logger);
        _settings = new StubSettings();
        _playerSwitcher = Substitute.For<IPlayerSwitchPolicyProvider>();
        _playerSwitcher.Current.Returns(PlayerSwitchPolicy.Default);
        _session = new PlayerSwitchSessionStore();
        _heroes = Substitute.For<IHeroPickerService>();
        var identity = Substitute.For<IPlayerIdentityAdapter>();
        identity.CanReassignPlayerClan.Returns(true);
        identity.StartupClanIsDisposable.Returns(true);
        identity.IsSwitchable(Arg.Any<string>()).Returns(true);
        _appearance = Substitute.For<IPresetAppearanceAdapter>();
        _presets = new FactionPresetService(_appearance);
        _picks = new FactionPickService(_presets, new System.Lazy<IHeroPickerService>(() => _heroes), _playerSwitcher, _session, identity, _logger);
        _sut = new FactionScreenLauncher(
            _settings,
            new FactionScreenCatalog(config, sprites, Substitute.For<ITextLocalizerAdapter>(), Substitute.For<IFactionSelectionService>(), _logger),
            sprites,
            _files,
            _picks,
            new FactionRoster(Substitute.For<IFactionRosterAdapter>(), config),
            new FactionScreenWidgets(),
            _playerSwitcher,
            _logger);
    }

    private static readonly Dictionary<string, RegionData> NoRegions = new();
    private static readonly Dictionary<string, FactionData> NoFactions = new();

    private void PickATakeover()
    {
        var hero = new object();
        _appearance.Resolve(hero, true).Returns(new object());
        _heroes.FindTakeover("lord_M1_1", "mirkwood", Arg.Any<PlayerSwitchPolicy>())
            .Returns(new HeroPickRow("lord_M1_1", "Thranduil", HeroPickerGroup.RulingHouse, 0, false, true, true));
        _picks.Pick(new RosterEntry(hero, "lord_M1_1", "Thranduil", isHero: true, heroId: "lord_M1_1"), "mirkwood");
        Assert.IsTrue(_picks.IsTakeover);
    }

    [TestMethod]
    public void TryLoad_WithTheScreenSwitchedOff_LeavesTheFactionMapAndShowsPlayerSwitchersPanel()
    {
        _settings.Value = new FactionUISettings(true, true, true, true, true, true, factionScreen: false);

        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        _playerSwitcher.Received().SetPickerHidden(false);
        _playerSwitcher.DidNotReceive().SetPickerHidden(true);
        _playerSwitcher.DidNotReceiveWithAnyArgs().DisableForSession(default!);
    }

    [TestMethod]
    public void TryLoad_WithNoPlayableFaction_LeavesTheFactionMapWarnsAndShowsPlayerSwitchersPanel()
    {
        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("no playable factions")));
        _playerSwitcher.Received().SetPickerHidden(false);
        _playerSwitcher.DidNotReceive().SetPickerHidden(true);
    }

    [TestMethod]
    public void TryLoad_WithTheScreenPrefabMissing_LeavesTheFactionMapWarnsOnceAndShowsPlayerSwitchersPanel()
    {
        // A damaged install: loading the movie by name would build an empty screen with nothing to confirm.
        _files.MissingPrefabs.Add(FrontEndMovieService.FactionScreenMovie);

        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));
        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(FrontEndMovieService.FactionScreenMovie) && m.Contains("faction map")));
        _playerSwitcher.Received().SetPickerHidden(false);
        _playerSwitcher.DidNotReceive().SetPickerHidden(true);
    }

    [TestMethod]
    public void TryLoad_AfterTheScreenFailedOnce_LeavesTheFactionMapWithoutTryingAgain()
    {
        // Null faction-map data makes the catalog throw: the failure path. Each failed build of the screen
        // would stay referenced by the engine's resource factories, so it is not attempted again.
        Assert.IsNull(_sut.TryLoad(null!, null!, null!, null!));
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("faction screen failed")));

        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("no playable factions")));
        _playerSwitcher.DidNotReceive().SetPickerHidden(true);
    }

    [TestMethod]
    public void TryLoad_Declining_PutsBackTheLookAnEarlierPickCopied()
    {
        // A pick copied at the face generator, then the screen declines on the way back (its toggle turned
        // off in mid-creation): no faction-screen pick may reach the finalize handler.
        var hero = new object();
        var look = new object();
        _appearance.Resolve(hero, true).Returns(new object());
        _appearance.CaptureLook().Returns(look);
        _presets.SelectHero(hero);
        _presets.OnFaceGeneratorOpening();
        _settings.Value = new FactionUISettings(true, true, true, true, true, true, factionScreen: false);

        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        Assert.IsFalse(_presets.HasPick);
        _appearance.Received(1).RestoreLook(look);
    }

    [TestMethod]
    public void TryLoad_Declining_DropsATakeover()
    {
        // Back from the career menu with the screen switched off meanwhile: Player Switcher must not hand
        // over a hero the player can no longer see picked.
        PickATakeover();
        _settings.Value = new FactionUISettings(true, true, true, true, true, true, factionScreen: false);

        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        Assert.IsFalse(_picks.IsTakeover);
        Assert.IsFalse(_session.HasSelection, "Player Switcher's handover would still take the hero over");
    }

    [TestMethod]
    public void ResetForGameEnd_DropsThePickAndShowsPlayerSwitchersPanel()
    {
        var hero = new object();
        _appearance.Resolve(hero, true).Returns(new object());
        _presets.SelectHero(hero);

        _sut.ResetForGameEnd();

        Assert.IsFalse(_presets.HasPick);
        _playerSwitcher.Received().SetPickerHidden(false);
    }

    [TestMethod]
    public void ResetForGameEnd_ForgetsATakeover()
    {
        PickATakeover();

        _sut.ResetForGameEnd();

        Assert.IsFalse(_picks.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
    }

    [TestMethod]
    public void OnCultureStageClosed_WithNoScreenOpen_DoesNothing()
    {
        _sut.OnCultureStageClosed();

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }
}
