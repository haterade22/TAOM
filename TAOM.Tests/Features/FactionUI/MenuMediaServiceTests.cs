using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.Menus;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. The main-menu video, the startup splash and the menu-music latch. The latch follows the
/// TAOM latch rules: the setting decides only whether to silence, never whether a silence already in
/// force is lifted, so turning the option off mid-menu cannot leave the music stuck off.
/// </summary>
[TestClass]
public class MenuMediaServiceTests
{
    private const string Root = @"C:\Game\Modules\TAOM";

    private sealed class StubSettings : FactionUISettingsProvider
    {
        public FactionUISettings Value { get; set; } = FactionUISettings.Default;

        protected override FactionUISettings ReadSettings() => Value;
    }

    private FakeFrontEndResourceAdapter _adapter = null!;
    private FactionUIPaths _paths = null!;
    private StubSettings _settings = null!;
    private MenuMediaService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        _paths = new FactionUIPaths(pathService);
        _adapter = new FakeFrontEndResourceAdapter();
        _adapter.AddFile(Path.Combine(_paths.MenuVideos, "sauron", "sauron_pc.ivf"));
        _adapter.AddFile(Path.Combine(_paths.MenuVideos, "sauron", "sauron.ogg"));
        _adapter.AddFile(_paths.SplashVideo);
        _adapter.AddFile(_paths.SplashAudio);
        _settings = new StubSettings();
        _sut = new MenuMediaService(_adapter, _paths, _settings, new Random(1));
    }

    [TestMethod]
    public void ChooseMenuVideo_VanillaMenuVideo_IsReplacedByAModuleVideoAndItsAudio()
    {
        var choice = _sut.ChooseMenuVideo(@"C:\Game\Videos\initial_menu\main_pc.ivf");

        Assert.IsNotNull(choice);
        Assert.AreEqual(Path.Combine(_paths.MenuVideos, "sauron", "sauron_pc.ivf"), choice!.Value.Video);
        Assert.AreEqual(Path.Combine(_paths.MenuVideos, "sauron", "sauron.ogg"), choice.Value.Audio);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow(@"C:\Game\Videos\campaign_intro.ivf")]
    public void ChooseMenuVideo_NotTheMenuVideo_IsLeftAlone(string? requested)
    {
        Assert.IsNull(_sut.ChooseMenuVideo(requested));
    }

    [TestMethod]
    public void ChooseMenuVideo_OurOwnVideo_IsLeftAlone()
    {
        Assert.IsNull(_sut.ChooseMenuVideo(Path.Combine(_paths.MenuVideos, "sauron", "sauron_pc.ivf")));
    }

    [TestMethod]
    public void MenuVideos_LiveOutsideTheFolderVanillaPicksItsOwnFrom()
    {
        // MBInitialScreenBase.RefreshScene picks at random from every active module's
        // Videos/initial_menu (v1.5.3 :131-161); ours must not be among them, or switching the menu
        // video off would still show it now and then, with the vanilla theme over its soundtrack.
        StringAssert.DoesNotMatch(_paths.MenuVideos, new System.Text.RegularExpressions.Regex("initial_menu"));
    }

    [TestMethod]
    public void ChooseMenuVideo_Twice_ListsTheVideoFoldersOnce()
    {
        _sut.ChooseMenuVideo(@"C:\Game\Videos\initial_menu\main_pc.ivf");
        var listings = _adapter.ListDirectoriesCalls;

        _sut.ChooseMenuVideo(@"C:\Game\Videos\initial_menu\main_pc.ivf");
        _sut.OnActivateMenuMode(isMainMenu: true);

        Assert.AreEqual(listings, _adapter.ListDirectoriesCalls);
    }

    [TestMethod]
    public void ChooseMenuVideo_WithTheOptionOff_IsLeftAlone()
    {
        _settings.Value = new FactionUISettings(true, true, menuVideo: false, true, true, true);

        Assert.IsNull(_sut.ChooseMenuVideo(@"C:\Game\Videos\initial_menu\main_pc.ivf"));
    }

    [TestMethod]
    public void ChooseMenuVideo_AFolderMissingItsAudio_IsSkipped()
    {
        _adapter.AddFile(Path.Combine(_paths.MenuVideos, "broken", "broken_pc.ivf"));

        for (var i = 0; i < 20; i++)
        {
            var choice = _sut.ChooseMenuVideo(@"C:\Game\Videos\initial_menu\main_pc.ivf");
            Assert.IsFalse(choice!.Value.Video.Contains("broken"));
        }
    }

    [TestMethod]
    public void ChooseSplash_TaleWorldsLogoVideo_IsReplacedWhenBothFilesExist()
    {
        var choice = _sut.ChooseSplash(@"C:\Game\Videos\TWLogo_and_Partners.ivf");

        Assert.AreEqual(_paths.SplashVideo, choice!.Value.Video);
        Assert.AreEqual(_paths.SplashAudio, choice.Value.Audio);
    }

    [TestMethod]
    public void ChooseSplash_WithoutTheAudioFile_IsLeftAlone()
    {
        _adapter.Files.Remove(_paths.SplashAudio);

        Assert.IsNull(_sut.ChooseSplash(@"C:\Game\Videos\TWLogo_and_Partners.ivf"));
    }

    [TestMethod]
    public void ChooseSplash_AnyOtherVideo_IsLeftAlone()
    {
        Assert.IsNull(_sut.ChooseSplash(@"C:\Game\Videos\campaign_intro.ivf"));
    }

    [TestMethod]
    public void OnActivateMenuMode_OnTheMainMenu_SilencesVanillaMusicUnderOurVideo()
    {
        Assert.IsTrue(_sut.OnActivateMenuMode(isMainMenu: true));
        Assert.IsTrue(_sut.IsMenuMusicSilenced);
    }

    [TestMethod]
    public void OnActivateMenuMode_OutsideTheMainMenu_LetsVanillaPlay()
    {
        Assert.IsFalse(_sut.OnActivateMenuMode(isMainMenu: false));
    }

    [TestMethod]
    public void OnActivateMenuMode_WithMutingOff_LetsVanillaPlay()
    {
        _settings.Value = new FactionUISettings(true, true, true, muteMenuMusic: false, true, true);

        Assert.IsFalse(_sut.OnActivateMenuMode(isMainMenu: true));
    }

    [TestMethod]
    public void OnActivateMenuMode_WithTheMenuVideoOff_LetsVanillaPlay()
    {
        _settings.Value = new FactionUISettings(true, true, menuVideo: false, true, true, true);

        Assert.IsFalse(_sut.OnActivateMenuMode(isMainMenu: true));
    }

    [TestMethod]
    public void OnActivateMenuMode_WithNoMenuVideoInstalled_LetsVanillaPlay()
    {
        _adapter.Files.Clear();
        _adapter.Directories.Clear();

        Assert.IsFalse(_sut.OnActivateMenuMode(isMainMenu: true));
    }

    [TestMethod]
    public void OnTick_LeavingTheMainMenuWhileSilenced_LiftsTheSilenceOnce()
    {
        _sut.OnActivateMenuMode(isMainMenu: true);

        Assert.IsFalse(_sut.OnTick(isMainMenu: true));
        Assert.IsTrue(_sut.OnTick(isMainMenu: false));
        Assert.IsFalse(_sut.OnTick(isMainMenu: false));
    }

    [TestMethod]
    public void OnTick_TurningMutingOffMidMenu_StillLiftsTheSilenceOnLeaving()
    {
        _sut.OnActivateMenuMode(isMainMenu: true);
        _settings.Value = new FactionUISettings(true, true, true, muteMenuMusic: false, true, true);

        Assert.IsTrue(_sut.OnTick(isMainMenu: false));
    }
}
