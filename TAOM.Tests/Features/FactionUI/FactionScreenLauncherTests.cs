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

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. When the faction screen takes the culture stage, and what happens to Player Switcher
/// and to a pick around it. The paths proven here return before any screen is built.
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
    private IPresetAppearanceAdapter _appearance = null!;
    private FactionPresetService _presets = null!;
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
        _appearance = Substitute.For<IPresetAppearanceAdapter>();
        _presets = new FactionPresetService(_appearance);
        _sut = new FactionScreenLauncher(
            _settings,
            new FactionScreenCatalog(config, sprites, Substitute.For<ITextLocalizerAdapter>(), Substitute.For<IFactionSelectionService>(), _logger),
            sprites,
            _files,
            _presets,
            new FactionRoster(Substitute.For<IFactionRosterAdapter>(), config),
            new FactionScreenWidgets(),
            _playerSwitcher,
            _logger);
    }

    private static readonly Dictionary<string, RegionData> NoRegions = new();
    private static readonly Dictionary<string, FactionData> NoFactions = new();

    [TestMethod]
    public void TryLoad_WithTheScreenSwitchedOff_LeavesTheFactionMapAndGivesPlayerSwitcherBack()
    {
        _settings.Value = new FactionUISettings(true, true, true, true, true, true, factionScreen: false);

        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        _playerSwitcher.Received().SetSuppressedForCharacterCreation(false);
        _playerSwitcher.DidNotReceive().SetSuppressedForCharacterCreation(true);
        _playerSwitcher.DidNotReceiveWithAnyArgs().DisableForSession(default!);
    }

    [TestMethod]
    public void TryLoad_WithNoPlayableFaction_LeavesTheFactionMapWarnsAndGivesPlayerSwitcherBack()
    {
        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("no playable factions")));
        _playerSwitcher.Received().SetSuppressedForCharacterCreation(false);
        _playerSwitcher.DidNotReceive().SetSuppressedForCharacterCreation(true);
    }

    [TestMethod]
    public void TryLoad_WithTheScreenPrefabMissing_LeavesTheFactionMapWarnsOnceAndGivesPlayerSwitcherBack()
    {
        // A damaged install: loading the movie by name would build an empty screen with nothing to confirm.
        _files.MissingPrefabs.Add(FrontEndMovieService.FactionScreenMovie);

        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));
        Assert.IsNull(_sut.TryLoad(null!, null!, NoRegions, NoFactions));

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(FrontEndMovieService.FactionScreenMovie) && m.Contains("faction map")));
        _playerSwitcher.Received().SetSuppressedForCharacterCreation(false);
        _playerSwitcher.DidNotReceive().SetSuppressedForCharacterCreation(true);
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
        _playerSwitcher.DidNotReceive().SetSuppressedForCharacterCreation(true);
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
    public void ResetForGameEnd_DropsThePickAndGivesPlayerSwitcherBack()
    {
        var hero = new object();
        _appearance.Resolve(hero, true).Returns(new object());
        _presets.SelectHero(hero);

        _sut.ResetForGameEnd();

        Assert.IsFalse(_presets.HasPick);
        _playerSwitcher.Received().SetSuppressedForCharacterCreation(false);
    }

    [TestMethod]
    public void OnCultureStageClosed_WithNoScreenOpen_DoesNothing()
    {
        _sut.OnCultureStageClosed();

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }
}
