using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Kysaro's loading screens replace the image vanilla picks, never the same one twice in a
/// row, and only one stays loaded (the sprite service releases the one before).
/// </summary>
[TestClass]
public class LoadingImageServiceTests
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
    private FrontEndSpriteService _sprites = null!;
    private LoadingImageService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        _paths = new FactionUIPaths(pathService);
        _adapter = new FakeFrontEndResourceAdapter();
        _adapter.AddFile(Path.Combine(_paths.LoadingScreens, "loading_erebor.png"));
        _adapter.AddFile(Path.Combine(_paths.LoadingScreens, "loading_urukhai.png"));
        _sprites = new FrontEndSpriteService(_adapter, _paths, Substitute.For<IModLogger>());
        _settings = new StubSettings();
        _sut = new LoadingImageService(_sprites, _adapter, _paths, _settings, new Random(7));
    }

    [TestMethod]
    public void ReplaceImageName_VanillaImage_BecomesOneOfOurs()
    {
        var name = _sut.ReplaceImageName("loading_3");

        StringAssert.StartsWith(name, FrontEndSpriteService.LoadingSpritePrefix);
        Assert.IsTrue(_adapter.IsSpriteRegistered(name!));
    }

    [TestMethod]
    public void ReplaceImageName_OptionTurnedOffWhileAPictureIsHeld_ReleasesThePictureAfterTheDelay()
    {
        var shown = _sut.ReplaceImageName("loading_3");
        _settings.Value = new FactionUISettings(true, true, true, true, customLoadingImages: false, true);

        Assert.IsNull(_sut.ReplaceImageName("loading_4"));
        for (var i = 0; i <= FrontEndSpriteService.ReleaseDelayTicks; i++)
            _sprites.Tick(FrontEndImageGroups.None);

        Assert.IsFalse(_adapter.IsSpriteRegistered(shown!));
        Assert.AreEqual(1, _adapter.ReleasedTextures.Count);
    }

    [TestMethod]
    public void ReplaceImageName_NeverPicksTheSameImageTwiceInARow()
    {
        var previous = _sut.ReplaceImageName("loading_1");
        for (var i = 0; i < 20; i++)
        {
            var next = _sut.ReplaceImageName("loading_1");
            Assert.AreNotEqual(previous, next);
            previous = next;
        }
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("fs_loading_loading_erebor")]
    public void ReplaceImageName_EmptyOrAlreadyOurs_IsLeftAlone(string? requested)
    {
        Assert.IsNull(_sut.ReplaceImageName(requested));
    }

    [TestMethod]
    public void ReplaceImageName_WithTheOptionOff_IsLeftAlone()
    {
        _settings.Value = new FactionUISettings(true, true, true, true, customLoadingImages: false, true);

        Assert.IsNull(_sut.ReplaceImageName("loading_3"));
    }

    [TestMethod]
    public void ReplaceImageName_WithNoImagesInstalled_IsLeftAlone()
    {
        _adapter.Files.Clear();

        Assert.IsNull(_sut.ReplaceImageName("loading_3"));
    }

    [TestMethod]
    public void ReplaceImageName_WithASingleImage_ShowsItEveryTime()
    {
        _adapter.Files.Remove(Path.Combine(_paths.LoadingScreens, "loading_urukhai.png"));

        Assert.AreEqual("fs_loading_loading_erebor", _sut.ReplaceImageName("loading_1"));
        Assert.AreEqual("fs_loading_loading_erebor", _sut.ReplaceImageName("loading_2"));
    }

    [TestMethod]
    public void ReplaceImageName_WhenTheImageFailsToLoad_LeavesVanillasImage()
    {
        _adapter.FailingTextures.Add(Path.Combine(_paths.LoadingScreens, "loading_erebor.png"));
        _adapter.FailingTextures.Add(Path.Combine(_paths.LoadingScreens, "loading_urukhai.png"));

        Assert.IsNull(_sut.ReplaceImageName("loading_3"));
    }
}
