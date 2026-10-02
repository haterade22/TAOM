using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Which vanilla screens get Kysaro's movie, and the bookkeeping that ties a themed
/// screen's lifetime to the images it draws: a swap holds its image groups from just before the movie
/// loads for as long as the engine keeps that movie, however the engine releases it.
/// </summary>
[TestClass]
public class FrontEndMovieServiceTests
{
    private const string Root = @"C:\Game\Modules\TAOM";
    private const FrontEndImageGroups CharacterCreation = FrontEndImageGroups.CharacterCreation;

    private sealed class StubSettings : FactionUISettingsProvider
    {
        public FactionUISettings Value { get; set; } = FactionUISettings.Default;

        public int Reads { get; private set; }

        protected override FactionUISettings ReadSettings()
        {
            Reads++;
            return Value;
        }
    }

    private FakeFrontEndResourceAdapter _adapter = null!;
    private StubSettings _settings = null!;
    private IFrontEndStateAdapter _state = null!;
    private IModLogger _logger = null!;
    private FrontEndMovieService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        var paths = new FactionUIPaths(pathService);
        _adapter = new FakeFrontEndResourceAdapter();
        _adapter.AddFile(Path.Combine(paths.RuntimeSprites, "mm_logo.png"));
        _adapter.AddFile(Path.Combine(paths.RuntimeSprites, "cc_panel_bg.png"));
        _adapter.AddFile(Path.Combine(paths.RuntimeSprites, "ld_bar_stone.png"));
        _adapter.AddFile(Path.Combine(paths.RuntimeSprites, "gui_skills_icon_bow.png"));
        _adapter.AddFile(Path.Combine(paths.ConfigDirectory, "sprite_overrides.json"),
            "{ \"gui_skills_icon_bow\": \"SPGeneral\\\\Skills\\\\gui_skills_icon_bow\" }");
        _logger = Substitute.For<IModLogger>();
        var sprites = new FrontEndSpriteService(_adapter, paths, _logger);
        _settings = new StubSettings();
        _state = Substitute.For<IFrontEndStateAdapter>();
        _state.IsInCharacterCreation().Returns(true);
        _sut = new FrontEndMovieService(sprites, _settings, _state, _adapter, _logger);
    }

    private object LoadThemed(string movie, string viewModel)
    {
        var swap = _sut.BeginLoad(movie, viewModel);
        var handle = new object();
        _sut.LoadSucceeded(swap!, handle);
        return handle;
    }

    // ── Which screens are themed ──────────────────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow("FaceGen", "FaceGenVM", FrontEndMovieService.FaceGenMovie)]
    [DataRow("CharacterCreationNarrativeStage", "CharacterCreationNarrativeStageVM", FrontEndMovieService.NarrativeMovie)]
    [DataRow("CharacterCreationReviewStage", "CharacterCreationReviewStageVM", FrontEndMovieService.ReviewMovie)]
    [DataRow("BannerEditor", "BannerEditorVM", FrontEndMovieService.BannerEditorMovie)]
    [DataRow("CharacterCreationClanNamingStage", "CharacterCreationClanNamingStageVM", FrontEndMovieService.ClanNamingMovie)]
    [DataRow("CharacterCreationOptionsStage", "CharacterCreationOptionsStageVM", FrontEndMovieService.OptionsMovie)]
    public void BeginLoad_CharacterCreationStages_SwapToTheThemedMovieAndHoldTheirFrame(string movie, string viewModel, string expected)
    {
        var swap = _sut.BeginLoad(movie, viewModel);

        Assert.AreEqual(expected, swap!.TargetName);
        Assert.AreEqual(CharacterCreation, swap.Holds);
        Assert.IsTrue(_adapter.IsSpriteRegistered("cc_panel_bg"));
    }

    [TestMethod]
    public void BeginLoad_FactionScreen_HoldsTheFrameAndTheFactionArt()
    {
        var swap = _sut.BeginLoad(FrontEndMovieService.FactionScreenMovie, "FactionScreenVM");

        Assert.AreEqual(FrontEndMovieService.FactionScreenMovie, swap!.TargetName);
        Assert.AreEqual(CharacterCreation | FrontEndImageGroups.FactionArt, swap.Holds);
    }

    [TestMethod]
    public void BeginLoad_FactionScreen_HasNoVanillaMovieToFallBackTo()
    {
        var swap = _sut.BeginLoad(FrontEndMovieService.FactionScreenMovie, "FactionScreenVM");

        Assert.IsFalse(swap!.HasVanillaFallback,
            "the faction screen is TAOM's own movie: a failed build must reach its launcher, which shows the faction map, not load itself again");
    }

    [TestMethod]
    public void BeginLoad_AVanillaStage_FallsBackToItsVanillaMovie()
    {
        var swap = _sut.BeginLoad("CharacterCreationReviewStage", "CharacterCreationReviewStageVM");

        Assert.IsTrue(swap!.HasVanillaFallback);
    }

    [DataTestMethod]
    [DataRow("FaceGen", "FaceGenVM")]
    [DataRow("BannerEditor", "BannerEditorVM")]
    public void BeginLoad_TheBarberOrTheCampaignBannerEditor_StaysVanilla(string movie, string viewModel)
    {
        _state.IsInCharacterCreation().Returns(false);

        Assert.IsNull(_sut.BeginLoad(movie, viewModel));
        Assert.IsFalse(_adapter.IsSpriteRegistered("cc_panel_bg"));
    }

    [TestMethod]
    public void BeginLoad_EachCharacterCreationToggleOff_LeavesItsScreenVanilla()
    {
        _settings.Value = new FactionUISettings(true, true, true, true, true, true,
            customFaceGen: false, customNarrativeStage: false, customReviewStage: false,
            customBannerEditor: false, customClanNaming: false, customOptionsStage: false);

        Assert.IsNull(_sut.BeginLoad("FaceGen", "FaceGenVM"));
        Assert.IsNull(_sut.BeginLoad("CharacterCreationNarrativeStage", "CharacterCreationNarrativeStageVM"));
        Assert.IsNull(_sut.BeginLoad("CharacterCreationReviewStage", "CharacterCreationReviewStageVM"));
        Assert.IsNull(_sut.BeginLoad("BannerEditor", "BannerEditorVM"));
        Assert.IsNull(_sut.BeginLoad("CharacterCreationClanNamingStage", "CharacterCreationClanNamingStageVM"));
        Assert.IsNull(_sut.BeginLoad("CharacterCreationOptionsStage", "CharacterCreationOptionsStageVM"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("cc_panel_bg"));
    }

    [TestMethod]
    public void BeginLoad_AStageMovieDrivenByAnotherViewModel_IsLeftAlone()
    {
        Assert.IsNull(_sut.BeginLoad("FaceGen", "SomeOtherModsFaceGenVM"));
    }

    [TestMethod]
    public void BeginLoad_MainMenu_SwapsToTheThemedMenuAndHoldsOnlyTheMenuImages()
    {
        var swap = _sut.BeginLoad("InitialScreen", "InitialMenuVM");

        Assert.AreEqual(FrontEndMovieService.MainMenuMovie, swap!.TargetName);
        Assert.AreEqual(FrontEndImageGroups.MainMenu, swap.Holds);
        Assert.IsTrue(_adapter.IsSpriteRegistered("mm_logo"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("cc_panel_bg"));
    }

    [TestMethod]
    public void BeginLoad_MainMenu_AppliesTheSkillIconsWhenTheyAreOn()
    {
        _sut.BeginLoad("InitialScreen", "InitialMenuVM");

        Assert.IsTrue(_adapter.IsSpriteRegistered(@"SPGeneral\Skills\gui_skills_icon_bow"));
    }

    [TestMethod]
    public void BeginLoad_MainMenu_LeavesVanillaSkillIconsWhenTheyAreOff()
    {
        _settings.Value = new FactionUISettings(true, true, true, true, true, skillIcons: false);

        _sut.BeginLoad("InitialScreen", "InitialMenuVM");

        Assert.IsFalse(_adapter.IsSpriteRegistered(@"SPGeneral\Skills\gui_skills_icon_bow"));
    }

    [TestMethod]
    public void BeginLoad_MainMenuThemeOffSkillIconsOn_StillAppliesTheIcons()
    {
        _settings.Value = new FactionUISettings(customMainMenu: false, true, true, true, true, skillIcons: true);

        Assert.IsNull(_sut.BeginLoad("InitialScreen", "InitialMenuVM"));
        Assert.IsTrue(_adapter.IsSpriteRegistered(@"SPGeneral\Skills\gui_skills_icon_bow"));
    }

    [TestMethod]
    public void BeginLoad_MainMenuWithTheThemeOff_LeavesItAlone()
    {
        _settings.Value = new FactionUISettings(customMainMenu: false, true, true, true, true, true);

        Assert.IsNull(_sut.BeginLoad("InitialScreen", "InitialMenuVM"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("mm_logo"));
    }

    [TestMethod]
    public void BeginLoad_AMovieNamedLikeTheMenuButDrivenByAnotherViewModel_IsLeftAlone()
    {
        Assert.IsNull(_sut.BeginLoad("InitialScreen", "SomeOtherModsVM"));
    }

    [TestMethod]
    public void BeginLoad_GameVersion_SwapsToTheEmptyMovieWithoutImages()
    {
        var swap = _sut.BeginLoad("GameVersion", "GameVersionVM");

        Assert.AreEqual(FrontEndMovieService.EmptyMovie, swap!.TargetName);
        Assert.AreEqual(FrontEndImageGroups.None, swap.Holds);
    }

    [TestMethod]
    public void BeginLoad_GameVersionDrivenByAnotherViewModel_IsLeftAlone()
    {
        Assert.IsNull(_sut.BeginLoad("GameVersion", "SomeOtherModsVM"));
    }

    [TestMethod]
    public void BeginLoad_GameVersionWhenShown_IsLeftAlone()
    {
        _settings.Value = new FactionUISettings(true, hideGameVersion: false, true, true, true, true);

        Assert.IsNull(_sut.BeginLoad("GameVersion", "GameVersionVM"));
    }

    [TestMethod]
    public void BeginLoad_LoadingWindow_SwapsToTheThemedFrameWithItsResidentAssets()
    {
        var swap = _sut.BeginLoad("LoadingWindow", "LoadingWindowViewModel");

        Assert.AreEqual(FrontEndMovieService.LoadingWindowMovie, swap!.TargetName);
        Assert.AreEqual(FrontEndImageGroups.None, swap.Holds, "the loading window lives for the whole process");
        Assert.IsTrue(_adapter.IsSpriteRegistered("ld_bar_stone"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("PartyScreen")]
    public void BeginLoad_AnyOtherMovie_IsLeftAloneWithoutReadingTheSettings(string? movieName)
    {
        Assert.IsNull(_sut.BeginLoad(movieName, "AnyVM"));
        Assert.AreEqual(0, _settings.Reads, "BeginLoad runs for every movie the game loads");
    }

    [DataTestMethod]
    [DataRow("InitialScreen", "InitialMenuVM", FrontEndMovieService.MainMenuMovie)]
    [DataRow("FaceGen", "FaceGenVM", FrontEndMovieService.FaceGenMovie)]
    [DataRow("GameVersion", "GameVersionVM", FrontEndMovieService.EmptyMovie)]
    [DataRow("LoadingWindow", "LoadingWindowViewModel", FrontEndMovieService.LoadingWindowMovie)]
    public void BeginLoad_ThemedPrefabMissing_LeavesVanillaAndWarns(string movie, string viewModel, string target)
    {
        _adapter.MissingPrefabs.Add(target);

        Assert.IsNull(_sut.BeginLoad(movie, viewModel));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(target)));
    }

    // ── Holding the images for as long as the movie lives ─────────────────────────────────────

    [TestMethod]
    public void Tick_AThemedMovieStillLive_KeepsItsGroupsHeld()
    {
        LoadThemed("InitialScreen", "InitialMenuVM");

        Assert.AreEqual(FrontEndImageGroups.MainMenu, _sut.Tick());
    }

    [TestMethod]
    public void Tick_AThemedMovieTheEngineReleased_GivesUpItsGroups()
    {
        var movie = LoadThemed("InitialScreen", "InitialMenuVM");
        _state.IsMovieReleased(movie).Returns(true);

        Assert.AreEqual(FrontEndImageGroups.None, _sut.Tick());
    }

    [TestMethod]
    public void Tick_TwoLiveScreens_HoldTheUnionOfTheirGroups()
    {
        LoadThemed("InitialScreen", "InitialMenuVM");
        LoadThemed(FrontEndMovieService.FactionScreenMovie, "FactionScreenVM");

        Assert.AreEqual(FrontEndImageGroups.MainMenu | CharacterCreation | FrontEndImageGroups.FactionArt, _sut.Tick());
    }

    [TestMethod]
    public void Tick_OneOfTwoScreensSharingAGroupReleased_KeepsTheGroupHeld()
    {
        var narrative = LoadThemed("CharacterCreationNarrativeStage", "CharacterCreationNarrativeStageVM");
        LoadThemed("FaceGen", "FaceGenVM");
        _state.IsMovieReleased(narrative).Returns(true);

        Assert.AreEqual(CharacterCreation, _sut.Tick());
    }

    [TestMethod]
    public void Tick_AReleasedMovie_IsForgottenAndNeverAskedAboutAgain()
    {
        var movie = LoadThemed("InitialScreen", "InitialMenuVM");
        _state.IsMovieReleased(movie).Returns(true);
        _sut.Tick();
        _state.ClearReceivedCalls();

        _sut.Tick();

        _state.DidNotReceive().IsMovieReleased(movie);
    }

    [TestMethod]
    public void LoadSucceeded_AMovieThatHoldsNoImages_IsNotTracked()
    {
        var swap = _sut.BeginLoad("GameVersion", "GameVersionVM");
        _sut.LoadSucceeded(swap!, new object());

        Assert.AreEqual(FrontEndImageGroups.None, _sut.Tick());
        _state.DidNotReceive().IsMovieReleased(Arg.Any<object>());
    }

    [TestMethod]
    public void LoadFailed_HoldsNothingAndWarns()
    {
        var swap = _sut.BeginLoad("InitialScreen", "InitialMenuVM");

        _sut.LoadFailed(swap!);

        Assert.AreEqual(FrontEndImageGroups.None, _sut.Tick());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(FrontEndMovieService.MainMenuMovie)));
    }

    [TestMethod]
    public void LoadFailed_TheSameScreenLater_StaysVanillaUntilRestart()
    {
        // The engine keeps every failed GauntletMovie subscribed to its resource factories (v1.5.3
        // GauntletMovie.cs:52-53, unsubscribed only by Release), so a build that threw is not tried again.
        _sut.LoadFailed(_sut.BeginLoad("CharacterCreationReviewStage", "CharacterCreationReviewStageVM")!);

        Assert.IsNull(_sut.BeginLoad("CharacterCreationReviewStage", "CharacterCreationReviewStageVM"));
    }

    [TestMethod]
    public void LoadFailed_OneScreen_LeavesTheOthersThemed()
    {
        _sut.LoadFailed(_sut.BeginLoad("CharacterCreationReviewStage", "CharacterCreationReviewStageVM")!);

        Assert.IsNotNull(_sut.BeginLoad("CharacterCreationOptionsStage", "CharacterCreationOptionsStageVM"));
    }

    // ── The face generator decision, shared with its camera ───────────────────────────────────

    [TestMethod]
    public void WillThemeFaceGenerator_InCharacterCreationWithTheToggleOn_IsTrue()
    {
        Assert.IsTrue(_sut.WillThemeFaceGenerator());
    }

    [TestMethod]
    public void WillThemeFaceGenerator_OutsideCharacterCreation_IsFalse()
    {
        _state.IsInCharacterCreation().Returns(false);

        Assert.IsFalse(_sut.WillThemeFaceGenerator());
    }

    [TestMethod]
    public void WillThemeFaceGenerator_WithTheToggleOff_IsFalse()
    {
        _settings.Value = new FactionUISettings(true, true, true, true, true, true, customFaceGen: false);

        Assert.IsFalse(_sut.WillThemeFaceGenerator());
    }
}
