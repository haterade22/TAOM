using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704, Mike's decision of 2026-10-01: the front-end images load when a themed screen opens
/// and are released once the player has left it, instead of loading all of them at startup and holding
/// them for the whole session (about 600 MB uncompressed in Kysaro's module). Each themed screen holds
/// the image groups it draws; a group goes <see cref="FrontEndSpriteService.ReleaseDelayTicks"/> ticks
/// after nothing holds it.
/// </summary>
[TestClass]
public class FrontEndSpriteServiceTests
{
    private const string Root = @"C:\Game\Modules\TAOM";
    private const string VanillaBowIcon = @"SPGeneral\Skills\gui_skills_icon_bow";
    private const FrontEndImageGroups CharacterCreation = FrontEndImageGroups.CharacterCreation;
    private const FrontEndImageGroups MainMenu = FrontEndImageGroups.MainMenu;
    private const FrontEndImageGroups FactionArt = FrontEndImageGroups.FactionArt;

    private FakeFrontEndResourceAdapter _adapter = null!;
    private FactionUIPaths _paths = null!;
    private IModLogger _logger = null!;
    private FrontEndSpriteService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        _paths = new FactionUIPaths(pathService);
        _adapter = new FakeFrontEndResourceAdapter();

        AddSprite("cc_panel_bg");
        AddSprite("mm_logo");
        AddSprite("fs_confirm");
        AddSprite("cc_card");
        _adapter.AddFile(Path.Combine(_paths.RuntimeSprites, "cc_card.nine"), "4 4 4 4");
        AddSprite("ld_bar_stone");
        AddSprite("fs_reveal_aragorn");
        AddSprite("fs_portrait_gondor");
        AddSprite("gui_skills_icon_bow");
        _adapter.AddFile(Path.Combine(_paths.RuntimeFonts, "FS_Garamond", "FS_Garamond.png"));
        _adapter.AddFile(SkillIconMap, "{ \"gui_skills_icon_bow\": \"SPGeneral\\\\Skills\\\\gui_skills_icon_bow\" }");

        _logger = Substitute.For<IModLogger>();
        _sut = new FrontEndSpriteService(_adapter, _paths, _logger);
    }

    private string SkillIconMap => Path.Combine(_paths.ConfigDirectory, "sprite_overrides.json");

    private void AddSprite(string name) => _adapter.AddFile(Path.Combine(_paths.RuntimeSprites, name + ".png"));

    private string AddLoadingScreen(string name)
    {
        var file = Path.Combine(_paths.LoadingScreens, name + ".png");
        _adapter.AddFile(file);
        return file;
    }

    private void TickPastReleaseDelay(FrontEndImageGroups held = FrontEndImageGroups.None)
    {
        for (var i = 0; i <= FrontEndSpriteService.ReleaseDelayTicks; i++)
            _sut.Tick(held);
    }

    // ── Loading a group ───────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Acquire_CharacterCreation_LoadsItsFrameButNotTheMenuOrTheArt()
    {
        _sut.Acquire(CharacterCreation);

        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_confirm"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("mm_logo"), "the main menu's images are its own group");
        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_reveal_aragorn"), "art loads per image, as shown");
        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_portrait_gondor"));
    }

    [TestMethod]
    public void Acquire_MainMenu_LoadsOnlyTheMenuImages()
    {
        _sut.Acquire(MainMenu);

        Assert.IsTrue(_adapter.IsSpriteRegistered("mm_logo"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("cc_panel_bg"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_confirm"));
    }

    [TestMethod]
    public void Acquire_RegistersTheNinePatchTwinWhenTheSpriteHasANineFile()
    {
        _sut.Acquire(CharacterCreation);

        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_card_9"));
        Assert.AreEqual(new NinePatch(4, 4, 4, 4), _adapter.RegisteredNinePatches["cc_card_9"]);
        Assert.IsNull(_adapter.RegisteredNinePatches["cc_card"]);
    }

    [TestMethod]
    public void Acquire_CharacterCreation_ReloadsItsBrushFilesAfterTheSpritesExist()
    {
        _sut.Acquire(CharacterCreation);

        CollectionAssert.IsSubsetOf(new[] { "TAOMCharCreation", "TAOMFactionScreen" }, _adapter.BrushFileLoads);
        CollectionAssert.DoesNotContain(_adapter.BrushFileLoads, "TAOMMainMenu");
    }

    [TestMethod]
    public void Acquire_MainMenu_ReloadsOnlyTheMenuBrushFile()
    {
        _sut.Acquire(MainMenu);

        CollectionAssert.Contains(_adapter.BrushFileLoads, "TAOMMainMenu");
        CollectionAssert.DoesNotContain(_adapter.BrushFileLoads, "TAOMCharCreation");
    }

    [TestMethod]
    public void Acquire_AlsoRegistersTheResidentFrameAndFonts()
    {
        _sut.Acquire(MainMenu);

        Assert.IsTrue(_adapter.IsSpriteRegistered("ld_bar_stone"));
        CollectionAssert.Contains(_adapter.FontsRegistered, "FS_Garamond");
    }

    [TestMethod]
    public void Acquire_Twice_LoadsTheImagesOnce()
    {
        _sut.Acquire(CharacterCreation);
        var loadsAfterFirst = _adapter.LoadedTextures.Count;
        var brushLoadsAfterFirst = _adapter.BrushFileLoads.Count;

        _sut.Acquire(CharacterCreation);

        Assert.AreEqual(loadsAfterFirst, _adapter.LoadedTextures.Count);
        Assert.AreEqual(brushLoadsAfterFirst, _adapter.BrushFileLoads.Count, "nothing new was registered, so no brush rebinds");
    }

    [TestMethod]
    public void Acquire_WhenTheUiIsNotReady_DoesNothing()
    {
        _adapter.UiReady = false;

        _sut.Acquire(CharacterCreation | MainMenu);

        Assert.AreEqual(0, _adapter.LoadedTextures.Count);
    }

    // ── Releasing a group ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Tick_GroupStillHeld_KeepsItsImagesForever()
    {
        _sut.Acquire(CharacterCreation);

        TickPastReleaseDelay(held: CharacterCreation);
        TickPastReleaseDelay(held: CharacterCreation);

        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
        Assert.AreEqual(0, _adapter.ReleasedTextures.Count);
    }

    [TestMethod]
    public void Tick_GroupNoLongerHeld_KeepsItUntilTheDelayHasPassed()
    {
        _sut.Acquire(CharacterCreation);

        _sut.Tick(FrontEndImageGroups.None);

        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"), "the next stage's movie may load in a moment");
        Assert.AreEqual(0, _adapter.ReleasedTextures.Count);
    }

    [TestMethod]
    public void Tick_GroupNoLongerHeldForTheDelay_UnregistersAndReleasesIt()
    {
        _sut.Acquire(CharacterCreation);

        TickPastReleaseDelay();

        Assert.IsFalse(_adapter.IsSpriteRegistered("cc_panel_bg"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("cc_card_9"));
        Assert.AreEqual(_adapter.LoadedTextures.Count(f => f.Contains("cc_") || f.Contains("fs_confirm")),
            _adapter.ReleasedTextures.Count);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("front-end images released")));
    }

    [TestMethod]
    public void Tick_MenuLeftWhileCharacterCreationIsHeld_ReleasesOnlyTheMenu()
    {
        _sut.Acquire(MainMenu);
        _sut.Acquire(CharacterCreation);

        TickPastReleaseDelay(held: CharacterCreation);

        Assert.IsFalse(_adapter.IsSpriteRegistered("mm_logo"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
    }

    [TestMethod]
    public void Tick_FactionScreenLeftForTheNextStage_ReleasesTheArtButKeepsTheFrame()
    {
        _sut.Acquire(CharacterCreation | FactionArt);
        Assert.IsTrue(_sut.EnsureArt("fs_reveal_aragorn"));

        TickPastReleaseDelay(held: CharacterCreation);

        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_reveal_aragorn"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
    }

    [TestMethod]
    public void Tick_HeldAgainBeforeTheDelayRanOut_KeepsTheImages()
    {
        _sut.Acquire(CharacterCreation);
        for (var i = 0; i < FrontEndSpriteService.ReleaseDelayTicks - 1; i++)
            _sut.Tick(FrontEndImageGroups.None);

        _sut.Tick(CharacterCreation);
        for (var i = 0; i < FrontEndSpriteService.ReleaseDelayTicks - 1; i++)
            _sut.Tick(FrontEndImageGroups.None);

        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
        Assert.AreEqual(0, _adapter.ReleasedTextures.Count);
    }

    [TestMethod]
    public void Tick_AfterARelease_KeepsTheResidentFrameTheFontsAndTheSkillIcons()
    {
        _sut.ApplySkillIcons();
        _sut.Acquire(CharacterCreation | MainMenu);

        TickPastReleaseDelay();

        Assert.IsTrue(_adapter.IsSpriteRegistered("ld_bar_stone"));
        Assert.IsTrue(_adapter.IsSpriteRegistered(VanillaBowIcon));
        CollectionAssert.Contains(_adapter.FontsRegistered, "FS_Garamond");
    }

    [TestMethod]
    public void Acquire_AfterARelease_LoadsTheImagesAgainAndRebindsTheBrushes()
    {
        _sut.Acquire(CharacterCreation);
        TickPastReleaseDelay();
        var brushLoadsBefore = _adapter.BrushFileLoads.Count;

        _sut.Acquire(CharacterCreation);

        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
        Assert.IsTrue(_adapter.BrushFileLoads.Count > brushLoadsBefore);
    }

    // ── Art ───────────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void EnsureArt_LoadsOneImageOnce()
    {
        _sut.Acquire(CharacterCreation | FactionArt);

        Assert.IsTrue(_sut.EnsureArt("fs_portrait_gondor"));
        Assert.IsTrue(_sut.EnsureArt("fs_portrait_gondor"));

        Assert.AreEqual(1, _adapter.LoadedTextures.Count(f => f.Contains("fs_portrait_gondor")));
    }

    [TestMethod]
    public void EnsureArt_ForAnImageThatDoesNotExist_ReturnsFalse()
    {
        _sut.Acquire(CharacterCreation | FactionArt);

        Assert.IsFalse(_sut.EnsureArt("fs_reveal_nobody"));
    }

    [TestMethod]
    public void EnsureArt_BeforeTheUiIsReady_ReturnsFalseAndLoadsNothing()
    {
        _adapter.UiReady = false;

        Assert.IsFalse(_sut.EnsureArt("fs_reveal_aragorn"));
        Assert.AreEqual(0, _adapter.LoadedTextures.Count);
    }

    [TestMethod]
    public void EnsureArt_ForAFrameImageAlreadyLoaded_ReportsItWithoutASecondLoad()
    {
        _sut.Acquire(CharacterCreation);
        var loads = _adapter.LoadedTextures.Count;

        Assert.IsTrue(_sut.EnsureArt("cc_panel_bg"));
        Assert.AreEqual(loads, _adapter.LoadedTextures.Count);
    }

    [TestMethod]
    public void EnsureArt_ForAResidentOrSkillIconName_OnlyReportsWhetherItIsRegistered()
    {
        Assert.IsFalse(_sut.EnsureArt("ld_bar_stone"), "resident images are registered by EnsureResident, not here");
        Assert.IsFalse(_sut.EnsureArt("gui_skills_icon_bow"));

        _sut.EnsureResident();

        Assert.IsTrue(_sut.EnsureArt("ld_bar_stone"));
        Assert.AreEqual(0, _adapter.LoadedTextures.Count(f => f.Contains("gui_skills_icon_bow")));
    }

    [TestMethod]
    public void EnsureArt_WhenNothingHoldsTheArt_ReleasesItAfterTheDelay()
    {
        Assert.IsTrue(_sut.EnsureArt("fs_reveal_aragorn"));

        TickPastReleaseDelay();

        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_reveal_aragorn"));
    }

    [TestMethod]
    public void EnsureArt_AFrameImageNothingAcquiredYet_LoadsItIntoItsGroup()
    {
        Assert.IsTrue(_sut.EnsureArt("fs_confirm"));

        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_confirm"));
        TickPastReleaseDelay();
        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_confirm"), "it goes with its group");
    }

    // ── Resident images and fonts ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void EnsureResident_RegistersTheFrameTheFontsAndTheLoadingBrushes()
    {
        _sut.EnsureResident();

        Assert.IsTrue(_adapter.IsSpriteRegistered("ld_bar_stone"));
        CollectionAssert.Contains(_adapter.FontsRegistered, "FS_Garamond");
        CollectionAssert.Contains(_adapter.BrushFileLoads, "TAOMLoading");
        Assert.IsFalse(_adapter.IsSpriteRegistered("cc_panel_bg"), "the frame groups wait for a themed screen");
    }

    [TestMethod]
    public void EnsureResident_Twice_DoesNotReloadTheLoadingBrushesOrListTheFontsAgain()
    {
        _sut.EnsureResident();
        var listings = _adapter.ListDirectoriesCalls;

        _sut.EnsureResident();

        Assert.AreEqual(1, _adapter.BrushFileLoads.Count(b => b == "TAOMLoading"));
        Assert.AreEqual(listings, _adapter.ListDirectoriesCalls);
    }

    // ── Skill icons ───────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplySkillIcons_RegistersEachIconUnderTheVanillaName()
    {
        _sut.ApplySkillIcons();

        Assert.IsTrue(_adapter.IsSpriteRegistered(VanillaBowIcon));
    }

    [TestMethod]
    public void ApplySkillIcons_Twice_ReadsTheMapAndLoadsTheIconsOnce()
    {
        _sut.ApplySkillIcons();
        _sut.ApplySkillIcons();

        Assert.AreEqual(1, _adapter.ReadCounts[SkillIconMap]);
        Assert.AreEqual(1, _adapter.LoadedTextures.Count(f => f.Contains("gui_skills_icon_bow")));
    }

    [TestMethod]
    public void ApplySkillIcons_AMappingWhoseImageIsMissing_IsSkippedWithAWarning()
    {
        _adapter.Files[SkillIconMap] = "{ \"gui_skills_icon_absent\": \"SPGeneral\\\\Skills\\\\gui_skills_icon_absent\" }";

        _sut.ApplySkillIcons();

        Assert.IsFalse(_adapter.IsSpriteRegistered(@"SPGeneral\Skills\gui_skills_icon_absent"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("gui_skills_icon_absent")));
    }

    [TestMethod]
    public void ApplySkillIcons_AMappingWhoseImageIsMissing_IsReportedOnceNotAtEveryMainMenu()
    {
        _adapter.Files[SkillIconMap] = "{ \"gui_skills_icon_absent\": \"SPGeneral\\\\Skills\\\\gui_skills_icon_absent\" }";

        _sut.ApplySkillIcons();
        _sut.ApplySkillIcons();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("no image gui_skills_icon_absent")));
    }

    [TestMethod]
    public void ApplySkillIcons_AnySkippedEntry_EndsWithOneSummaryWarning()
    {
        _adapter.Files[SkillIconMap] =
            "{ \"gui_skills_icon_absent\": \"SPGeneral\\\\Skills\\\\x\", \"gui_skills_icon_bow\": 3, " +
            "\"gui_skills_icon_bow_too\": \"\" }";

        _sut.ApplySkillIcons();
        _sut.ApplySkillIcons();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("3 entries") && s.Contains("skipped")));
    }

    [DataTestMethod]
    [DataRow("{ \"gui_skills_icon_bow\": 3 }", "gui_skills_icon_bow")]
    [DataRow("{ \"gui_skills_icon_bow\": \"\" }", "gui_skills_icon_bow")]
    [DataRow("{ \"\": \"SPGeneral\\\\Skills\\\\x\" }", "empty")]
    public void ApplySkillIcons_AnEntryThatIsNotANameToAName_IsSkippedWithAWarning(string json, string expectedInWarning)
    {
        _adapter.Files[SkillIconMap] = json;

        _sut.ApplySkillIcons();

        Assert.IsFalse(_adapter.Registered.Keys.Any(k => k.StartsWith("SPGeneral", StringComparison.Ordinal)));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("sprite_overrides.json") && s.Contains(expectedInWarning)));
    }

    [TestMethod]
    public void ApplySkillIcons_NotesAreIgnoredWithoutAWarning()
    {
        _adapter.Files[SkillIconMap] = "{ \"_readme\": \"notes\", \"gui_skills_icon_bow\": \"SPGeneral\\\\Skills\\\\gui_skills_icon_bow\" }";

        _sut.ApplySkillIcons();

        Assert.IsTrue(_adapter.IsSpriteRegistered(VanillaBowIcon));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplySkillIcons_WithMalformedJson_RegistersNothingAndWarns()
    {
        _adapter.Files[SkillIconMap] = "{ not json";

        _sut.ApplySkillIcons();

        Assert.IsFalse(_adapter.Registered.Keys.Any(k => k.StartsWith("SPGeneral", StringComparison.Ordinal)));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not valid JSON")));
    }

    // ── Loading screens ───────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShowLoadingImage_RegistersTheImageUnderItsLoadingName()
    {
        var name = _sut.ShowLoadingImage(AddLoadingScreen("loading_erebor"));

        Assert.AreEqual(FrontEndSpriteService.LoadingSpritePrefix + "loading_erebor", name);
        Assert.IsTrue(_adapter.IsSpriteRegistered(name!));
    }

    [TestMethod]
    public void ShowLoadingImage_ReleasesThePreviousImageAfterTheDelay()
    {
        var first = AddLoadingScreen("loading_erebor");
        var second = AddLoadingScreen("loading_urukhai");
        _sut.ShowLoadingImage(first);

        _sut.ShowLoadingImage(second);
        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_loading_loading_erebor"), "the old image may still be on screen this frame");
        TickPastReleaseDelay();

        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_loading_loading_erebor"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_loading_loading_urukhai"));
        Assert.AreEqual(1, _adapter.ReleasedTextures.Count);
    }

    [TestMethod]
    public void ShowLoadingImage_TheSameImageTwice_KeepsIt()
    {
        var file = AddLoadingScreen("loading_erebor");
        _sut.ShowLoadingImage(file);

        _sut.ShowLoadingImage(file);
        TickPastReleaseDelay();

        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_loading_loading_erebor"));
        Assert.AreEqual(0, _adapter.ReleasedTextures.Count);
    }

    [TestMethod]
    public void ShowLoadingImage_AnImageStillAwaitingRelease_IsShownAgainWithoutAReload()
    {
        var first = AddLoadingScreen("loading_erebor");
        var second = AddLoadingScreen("loading_urukhai");
        _sut.ShowLoadingImage(first);
        _sut.ShowLoadingImage(second);

        _sut.ShowLoadingImage(first);
        TickPastReleaseDelay();

        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_loading_loading_erebor"));
        Assert.AreEqual(1, _adapter.LoadedTextures.Count(f => f.Contains("loading_erebor")));
        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_loading_loading_urukhai"));
    }

    [TestMethod]
    public void ShowLoadingImage_WhenTheTextureFailsToLoad_ReturnsNull()
    {
        var file = AddLoadingScreen("loading_erebor");
        _adapter.FailingTextures.Add(file);

        Assert.IsNull(_sut.ShowLoadingImage(file));
    }

    [TestMethod]
    public void ReleaseLoadingImage_ReleasesTheHeldImageAfterTheDelay()
    {
        _sut.ShowLoadingImage(AddLoadingScreen("loading_erebor"));

        _sut.ReleaseLoadingImage();
        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_loading_loading_erebor"), "it can still be on screen this frame");
        TickPastReleaseDelay();

        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_loading_loading_erebor"));
        Assert.AreEqual(1, _adapter.ReleasedTextures.Count);
    }

    [TestMethod]
    public void Tick_AnImageWhoseReleaseThrows_IsNotRetriedEveryTick()
    {
        var file = AddLoadingScreen("loading_erebor");
        _sut.ShowLoadingImage(file);
        _sut.ReleaseLoadingImage();
        _adapter.TexturesThatThrowOnRelease.Add(file);

        var throws = 0;
        for (var i = 0; i <= FrontEndSpriteService.ReleaseDelayTicks + 5; i++)
        {
            try
            {
                _sut.Tick(FrontEndImageGroups.None);
            }
            catch (InvalidOperationException)
            {
                throws++;
            }
        }

        Assert.AreEqual(1, throws);
    }

    // ── The engine rebuilding its resources ───────────────────────────────────────────────────

    [TestMethod]
    public void OnEngineResourcesRefreshed_PutsBackEveryLoadedImageFontAndBrushFile()
    {
        _sut.Acquire(CharacterCreation | FactionArt);
        Assert.IsTrue(_sut.EnsureArt("fs_reveal_aragorn"));
        _sut.ShowLoadingImage(AddLoadingScreen("loading_erebor"));
        _adapter.SimulateEngineRefresh();
        var loads = _adapter.LoadedTextures.Count;

        _sut.OnEngineResourcesRefreshed();

        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_card_9"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_reveal_aragorn"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("ld_bar_stone"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("fs_loading_loading_erebor"));
        CollectionAssert.Contains(_adapter.FontsRegistered, "FS_Garamond");
        CollectionAssert.IsSubsetOf(new[] { "TAOMLoading", "TAOMCharCreation", "TAOMFactionScreen" }, _adapter.BrushFileLoads);
        CollectionAssert.DoesNotContain(_adapter.BrushFileLoads, "TAOMMainMenu", "the menu group was never loaded");
        Assert.AreEqual(loads, _adapter.LoadedTextures.Count, "the textures are still alive; only the names were lost");
    }

    [TestMethod]
    public void OnEngineResourcesRefreshed_PutsTheSkillIconsBackOverTheVanillaOnesTheReloadRestored()
    {
        _sut.ApplySkillIcons();
        var ours = _adapter.Registered[VanillaBowIcon];
        _adapter.SimulateEngineRefresh(VanillaBowIcon);

        _sut.OnEngineResourcesRefreshed();

        Assert.AreSame(ours, _adapter.Registered[VanillaBowIcon]);
    }

    [TestMethod]
    public void OnEngineResourcesRefreshed_BeforeAnythingWasLoaded_DoesNothing()
    {
        _sut.OnEngineResourcesRefreshed();

        Assert.AreEqual(0, _adapter.LoadedTextures.Count);
        Assert.AreEqual(0, _adapter.BrushFileLoads.Count);
    }
}
