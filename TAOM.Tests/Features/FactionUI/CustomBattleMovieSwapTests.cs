using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Resources;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// TAOM's own Custom Battle screen (TaomCustomBattle, bound to TcbVM). It draws the main menu's and the
/// encyclopedia's shared images by their original names, so its load takes the same image hold as every
/// themed front-end screen: the images are registered before the movie binds them, held while the movie
/// lives, released after it closes, and registered again when the screen reopens.
/// </summary>
[TestClass]
public class CustomBattleMovieSwapTests
{
    private const string Root = @"C:\Game\Modules\TAOM";
    private const string Movie = "TaomCustomBattle";
    private const string ViewModel = "TcbVM";

    private sealed class StubSettings : FactionUISettingsProvider
    {
        protected override FactionUISettings ReadSettings() => FactionUISettings.Default;
    }

    private const string Brushes = "TaomCustomBattle";

    private FakeFrontEndResourceAdapter _adapter = null!;
    private IFrontEndStateAdapter _state = null!;
    private FrontEndSpriteService _sprites = null!;
    private FrontEndMovieService _sut = null!;
    private FactionUIPaths _paths = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        _paths = new FactionUIPaths(pathService);
        _adapter = new FakeFrontEndResourceAdapter();
        _adapter.AddFile(Path.Combine(_paths.RuntimeSprites, "ec_panel.png"));
        _adapter.AddFile(Path.Combine(_paths.RuntimeSprites, "mm_glow.png"));
        _adapter.AddFile(Path.Combine(_paths.RuntimeSprites, "fs_portrait_gondor.png"));
        _adapter.AddFile(Path.Combine(_paths.RuntimeFonts, "FS_Garamond", "FS_Garamond.png"));
        _logger = Substitute.For<IModLogger>();
        _state = Substitute.For<IFrontEndStateAdapter>();
        RebuildWith(_adapter);
    }

    private void RebuildWith(IFrontEndResourceAdapter adapter)
    {
        _sprites = new FrontEndSpriteService(adapter, _paths, _logger);
        _sut = new FrontEndMovieService(_sprites, new StubSettings(), _state, adapter, _logger);
    }

    /// <summary>Passes every call to the fake and, each time the named brush file is read, records which
    /// fonts and images the engine already knew at that moment.</summary>
    private sealed class BrushLoadRecorder : IFrontEndResourceAdapter
    {
        private readonly FakeFrontEndResourceAdapter _inner;
        private readonly string _brushFile;

        public BrushLoadRecorder(FakeFrontEndResourceAdapter inner, string brushFile)
        {
            _inner = inner;
            _brushFile = brushFile;
        }

        public List<(string[] Fonts, string[] Sprites)> Loads { get; } = new();

        public bool IsUiReady => _inner.IsUiReady;
        public bool IsSpriteRegistered(string spriteName) => _inner.IsSpriteRegistered(spriteName);
        public IReadOnlyList<string> ListFiles(string directory, string suffix) => _inner.ListFiles(directory, suffix);
        public IReadOnlyList<string> ListDirectories(string directory) => _inner.ListDirectories(directory);
        public bool FileExists(string path) => _inner.FileExists(path);
        public string? ReadAllText(string path) => _inner.ReadAllText(path);
        public FrontEndTexture? LoadTexture(string filePath) => _inner.LoadTexture(filePath);
        public void RegisterSprite(string spriteName, FrontEndTexture texture, NinePatch? ninePatch) => _inner.RegisterSprite(spriteName, texture, ninePatch);
        public void UnregisterSprite(string spriteName) => _inner.UnregisterSprite(spriteName);
        public void ReleaseTexture(FrontEndTexture texture) => _inner.ReleaseTexture(texture);
        public bool RegisterFont(string fontDirectory, string fontName) => _inner.RegisterFont(fontDirectory, fontName);
        public bool HasPrefab(string movieName) => _inner.HasPrefab(movieName);

        public void LoadBrushFile(string brushFileName)
        {
            if (brushFileName == _brushFile)
                Loads.Add((_inner.FontsRegistered.ToArray(), _inner.Registered.Keys.ToArray()));
            _inner.LoadBrushFile(brushFileName);
        }
    }

    [TestMethod]
    public void BeginLoad_CustomBattleScreen_KeepsItsMovieAndHoldsMenuAndChrome()
    {
        var swap = _sut.BeginLoad(Movie, ViewModel);

        Assert.IsNotNull(swap);
        Assert.AreEqual(Movie, swap!.TargetName, "the screen's own prefab loads; only the hold is added");
        Assert.AreEqual(FrontEndImageGroups.MainMenu | FrontEndImageGroups.CharacterCreation, swap.Holds);
        Assert.IsTrue(_adapter.IsSpriteRegistered("ec_panel"));
        Assert.IsTrue(_adapter.IsSpriteRegistered("mm_glow"));
        Assert.IsFalse(_adapter.IsSpriteRegistered("fs_portrait_gondor"), "the screen draws no faction art");
    }

    /// <summary>The screen's own brushes name shared images (ec_panel_9, cc_card_9, fs_confirm) and the
    /// resident fonts, and the engine binds both once, when a brush file is parsed. So the screen's brush
    /// file is read by the acquire, after the fonts and every image it holds are registered, never before.</summary>
    [TestMethod]
    public void BeginLoad_CustomBattleScreen_ReadsItsBrushFileAfterTheFontsAndImages()
    {
        var recorder = new BrushLoadRecorder(_adapter, Brushes);
        RebuildWith(recorder);

        _sut.BeginLoad(Movie, ViewModel);

        Assert.AreEqual(1, recorder.Loads.Count, "the screen's brush file is read once by the acquire");
        var seen = recorder.Loads[0];
        CollectionAssert.Contains(seen.Fonts, "FS_Garamond", "fonts registered before the brush file is parsed");
        CollectionAssert.Contains(seen.Sprites, "ec_panel", "the encyclopedia's images registered first");
        CollectionAssert.Contains(seen.Sprites, "mm_glow", "the main menu's images registered first");
        CollectionAssert.Contains(_adapter.BrushFileLoads, "TAOMEncyclopedia");
        CollectionAssert.Contains(_adapter.BrushFileLoads, "TAOMCharCreation");
    }

    [TestMethod]
    public void BeginLoad_CustomBattleReopenedAfterRelease_ReadsItsBrushFileAgainAfterTheImages()
    {
        var recorder = new BrushLoadRecorder(_adapter, Brushes);
        RebuildWith(recorder);
        var handle = new object();
        _sut.LoadSucceeded(_sut.BeginLoad(Movie, ViewModel)!, handle);
        _state.IsMovieReleased(handle).Returns(true);
        for (var i = 0; i <= FrontEndSpriteService.ReleaseDelayTicks; i++)
            _sprites.Tick(_sut.Tick());
        Assert.IsFalse(_adapter.IsSpriteRegistered("ec_panel"), "released before the reopen");

        _sut.BeginLoad(Movie, ViewModel);

        Assert.AreEqual(2, recorder.Loads.Count, "the reopen reads the brush file again");
        CollectionAssert.Contains(recorder.Loads[1].Sprites, "ec_panel", "after the images are back");
    }

    [TestMethod]
    public void OnEngineResourcesRefreshed_CustomBattleOpen_ReadsItsBrushFileAfterTheFontsAndImages()
    {
        var recorder = new BrushLoadRecorder(_adapter, Brushes);
        RebuildWith(recorder);
        _sut.BeginLoad(Movie, ViewModel);
        _adapter.SimulateEngineRefresh();

        _sprites.OnEngineResourcesRefreshed();

        Assert.AreEqual(2, recorder.Loads.Count, "the rebuilt brush table gets the screen's brushes back");
        CollectionAssert.Contains(recorder.Loads[1].Fonts, "FS_Garamond");
        CollectionAssert.Contains(recorder.Loads[1].Sprites, "ec_panel");
    }

    [DataTestMethod]
    [DataRow("CustomBattleVM")]
    [DataRow("SomeOtherModVM")]
    [DataRow(null)]
    public void BeginLoad_CustomBattleMovieWithAnotherViewModel_TakesNoHold(string? viewModel)
    {
        Assert.IsNull(_sut.BeginLoad(Movie, viewModel));
        Assert.IsFalse(_adapter.IsSpriteRegistered("ec_panel"));
    }

    [TestMethod]
    public void BeginLoad_CustomBattlePrefabNotInstalled_TakesNoHold()
    {
        _adapter.MissingPrefabs.Add(Movie);

        Assert.IsNull(_sut.BeginLoad(Movie, ViewModel));
        Assert.IsFalse(_adapter.IsSpriteRegistered("ec_panel"));
    }

    [TestMethod]
    public void BeginLoad_CustomBattleFailedToBuild_TakesNoHoldUntilRestart()
    {
        _sut.LoadFailed(_sut.BeginLoad(Movie, ViewModel)!);

        Assert.IsNull(_sut.BeginLoad(Movie, ViewModel));
    }

    [TestMethod]
    public void Tick_CustomBattleClosedThenReopened_ReleasesAfterTheDelayAndRegistersAgain()
    {
        var handle = new object();
        _sut.LoadSucceeded(_sut.BeginLoad(Movie, ViewModel)!, handle);
        for (var i = 0; i < FrontEndSpriteService.ReleaseDelayTicks * 2; i++)
            _sprites.Tick(_sut.Tick());
        Assert.IsTrue(_adapter.IsSpriteRegistered("ec_panel"), "held while the movie lives");

        _state.IsMovieReleased(handle).Returns(true);
        for (var i = 0; i < FrontEndSpriteService.ReleaseDelayTicks; i++)
            _sprites.Tick(_sut.Tick());
        Assert.IsFalse(_adapter.IsSpriteRegistered("ec_panel"), "released once the closed screen's delay ran out");

        Assert.IsNotNull(_sut.BeginLoad(Movie, ViewModel));
        Assert.IsTrue(_adapter.IsSpriteRegistered("ec_panel"), "a reopened screen registers its images again");
    }

    /// <summary>The placeholder chrome the screen shipped with is gone: no literal tcb_ image in its
    /// prefabs or brush file. Bound sprites (map previews from TcbSprites) are not literals.</summary>
    [TestMethod]
    public void CustomBattlePrefabsAndBrushes_NameNoPlaceholderSprite()
    {
        var files = Directory.EnumerateFiles(RepoPath("Main", "_Module", "GUI", "PreFabs", "CustomBattle", "Muster"), "*.xml")
            .Append(RepoPath("Main", "_Module", "GUI", "Brushes", "TaomCustomBattle.xml"))
            .ToList();
        Assert.IsTrue(files.Count > 1, "the Custom Battle prefabs were not found");

        var offenders = files
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "Sprite=\"(tcb_[^\"]*)\"").Cast<Match>()
                .Select(m => Path.GetFileName(f) + ": " + m.Groups[1].Value))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
    }
}
