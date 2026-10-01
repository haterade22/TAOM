using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// The parchment map: the sheet is built once, on first need, from the module's picture, fades in over
/// the last stretch of the zoom, and shows only while MCM's switch is on and the borders are showing.
/// </summary>
[TestClass]
public class RealmAtlasServiceTests
{
    private const float Max = 500f;
    private const string ModuleData = @"C:\Modules\TAOM\ModuleData";

    private IBorderRenderAdapter _renderer = null!;
    private IRealmBordersSettings _settings = null!;
    private IModLogger _logger = null!;
    private RealmAtlasService _atlas = null!;
    private bool _built;
    private readonly List<IReadOnlyList<IReadOnlyList<BorderQuad>>> _builds = new List<IReadOnlyList<IReadOnlyList<BorderQuad>>>();

    [TestInitialize]
    public void SetUp()
    {
        _renderer = Substitute.For<IBorderRenderAdapter>();
        _renderer.IsAvailable.Returns(true);
        _renderer.HasSheet.Returns(_ => _built);
        _renderer.When(r => r.RemoveSheet()).Do(_ => _built = false);
        _renderer.SetSheet(Arg.Any<IReadOnlyList<IReadOnlyList<BorderQuad>>>(), Arg.Any<string>(), Arg.Any<uint>())
            .Returns(ci =>
            {
                _builds.Add(ci.ArgAt<IReadOnlyList<IReadOnlyList<BorderQuad>>>(0));
                _built = true;
                return true;
            });
        _settings = Substitute.For<IRealmBordersSettings>();
        _settings.ParchmentMap.Returns(true);
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        _logger = Substitute.For<IModLogger>();
        _atlas = new RealmAtlasService(_renderer, _settings, paths, _logger);
    }

    private void FailBuilds() =>
        _renderer.SetSheet(Arg.Any<IReadOnlyList<IReadOnlyList<BorderQuad>>>(), Arg.Any<string>(), Arg.Any<uint>()).Returns(false);

    [TestMethod]
    public void OnMapFrame_ZoomedIn_BuildsNothing()
    {
        _atlas.OnMapFrame(100f, Max, bordersShown: true);

        _renderer.DidNotReceiveWithAnyArgs().SetSheet(default!, default!, default);
    }

    [TestMethod]
    public void OnMapFrame_AllTheWayOut_BuildsTheSheetOnceFromTheModulePictureAndShowsIt()
    {
        _atlas.OnMapFrame(Max, Max, true);
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.Received(1).SetSheet(Arg.Any<IReadOnlyList<IReadOnlyList<BorderQuad>>>(),
            Path.Combine(ModuleData, "realm_borders", RealmAtlasService.TextureFileName), RealmAtlasService.DefaultPaper);
        Assert.AreEqual(64, _builds[0].Count, "an 8 x 8 grid of tiles");
        _renderer.Received(1).SetSheetAlpha(1f);
        Assert.AreEqual(1f, _atlas.Alpha);
    }

    [TestMethod]
    public void OnMapFrame_ParchmentMapOff_BuildsNothing()
    {
        _settings.ParchmentMap.Returns(false);

        _atlas.OnMapFrame(Max, Max, true);

        _renderer.DidNotReceiveWithAnyArgs().SetSheet(default!, default!, default);
    }

    [TestMethod]
    public void OnMapFrame_BordersHidden_BuildsNothing()
    {
        _atlas.OnMapFrame(Max, Max, bordersShown: false);

        _renderer.DidNotReceiveWithAnyArgs().SetSheet(default!, default!, default);
    }

    [TestMethod]
    public void OnMapFrame_BordersHidden_HidesTheSheet()
    {
        _atlas.OnMapFrame(Max, Max, true);

        _atlas.OnMapFrame(Max, Max, bordersShown: false);

        _renderer.Received(1).SetSheetAlpha(0f);
    }

    [TestMethod]
    public void OnMapFrame_ParchmentMapSwitchedOff_HidesTheSheetUntilOnAgain()
    {
        _atlas.OnMapFrame(Max, Max, true);

        _settings.ParchmentMap.Returns(false);
        _atlas.OnMapFrame(Max, Max, true);
        _settings.ParchmentMap.Returns(true);
        _atlas.OnMapFrame(Max, Max, true);

        Received.InOrder(() =>
        {
            _renderer.SetSheetAlpha(1f);
            _renderer.SetSheetAlpha(0f);
            _renderer.SetSheetAlpha(1f);
        });
        Assert.AreEqual(1, _builds.Count, "switching it off hides the sheet; it does not rebuild it");
    }

    [TestMethod]
    public void OnMapFrame_ZoomingBackIn_FadesTheSheetOutWithoutRebuildingIt()
    {
        _atlas.OnMapFrame(Max, Max, true);
        _atlas.OnMapFrame(437.5f, Max, true);
        _atlas.OnMapFrame(100f, Max, true);

        Received.InOrder(() =>
        {
            _renderer.SetSheetAlpha(1f);
            _renderer.SetSheetAlpha(0.5f);
            _renderer.SetSheetAlpha(0f);
        });
        Assert.AreEqual(1, _builds.Count);
    }

    [TestMethod]
    public void OnMapFrame_SheetGoneWithItsScene_BuildsItAgainAndShowsIt()
    {
        _atlas.OnMapFrame(Max, Max, true);
        _built = false; // a save load: the sheet went with its scene, outside RemoveSheet

        _atlas.OnMapFrame(Max, Max, true);

        Assert.AreEqual(2, _builds.Count);
        _renderer.Received(2).SetSheetAlpha(1f);
    }

    [TestMethod]
    public void OnMapFrame_BuildFails_DoesNotTryAgainEveryFrame()
    {
        FailBuilds();

        _atlas.OnMapFrame(Max, Max, true);
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.ReceivedWithAnyArgs(1).SetSheet(default!, default!, default);
        _renderer.DidNotReceive().SetSheetAlpha(Arg.Is<float>(a => a > 0f));
    }

    [TestMethod]
    public void OnMapFrame_NoMapScene_WaitsWithoutCountingItAFailure()
    {
        _renderer.IsAvailable.Returns(false);
        _atlas.OnMapFrame(Max, Max, true);
        _renderer.DidNotReceiveWithAnyArgs().SetSheet(default!, default!, default);

        _renderer.IsAvailable.Returns(true);
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.ReceivedWithAnyArgs(1).SetSheet(default!, default!, default);
        Assert.AreEqual(1f, _atlas.Alpha);
    }

    [TestMethod]
    public void OnMapFrame_BuildThrows_LatchesAndLogsTheException()
    {
        _renderer.SetSheet(Arg.Any<IReadOnlyList<IReadOnlyList<BorderQuad>>>(), Arg.Any<string>(), Arg.Any<uint>())
            .Returns(_ => throw new InvalidOperationException("native refusal"));

        _atlas.OnMapFrame(Max, Max, true);
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.ReceivedWithAnyArgs(1).SetSheet(default!, default!, default);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("native refusal")));
        Assert.AreEqual(0f, _atlas.Alpha);
    }

    [TestMethod]
    public void Rebuild_AfterAFailure_TriesAgain()
    {
        FailBuilds();
        _atlas.OnMapFrame(Max, Max, true);

        _atlas.Rebuild();
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.ReceivedWithAnyArgs(2).SetSheet(default!, default!, default);
        _renderer.Received().RemoveSheet();
    }

    [TestMethod]
    public void OnMapScreenClosed_AfterAFailedBuild_TriesOnceMoreOnTheNextMap()
    {
        FailBuilds();
        _atlas.OnMapFrame(Max, Max, true);
        _atlas.OnMapFrame(Max, Max, true);

        _atlas.OnMapScreenClosed();
        _atlas.OnMapFrame(Max, Max, true);
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.ReceivedWithAnyArgs(2).SetSheet(default!, default!, default);
        StringAssert.Contains(_atlas.Status(), "the build failed");
    }

    [TestMethod]
    public void Rebuild_ForgetsTheLastBuildsResult()
    {
        FailBuilds();
        _atlas.OnMapFrame(Max, Max, true);

        _atlas.Rebuild();

        StringAssert.Contains(_atlas.Status(), "not built yet");
    }

    [DataTestMethod]
    [DataRow(0.5f, 0.6f)]
    [DataRow(0f, 1f)]
    public void SetFade_ValidBand_MovesTheFade(float start, float full)
    {
        Assert.IsTrue(_atlas.SetFade(start, full));

        _atlas.OnMapFrame(full * Max, Max, true);

        _renderer.Received(1).SetSheetAlpha(1f);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0.95f)]
    [DataRow(0.8f, float.NaN)]
    [DataRow(0.8f, float.PositiveInfinity)]
    [DataRow(0.9f, 0.8f)]
    [DataRow(0.9f, 0.9f)]
    [DataRow(-0.1f, 0.9f)]
    [DataRow(0.8f, 1.5f)]
    public void SetFade_NotANumberOrOutOfOrder_IsRefused(float start, float full)
    {
        Assert.IsFalse(_atlas.SetFade(start, full));

        Assert.AreEqual(RealmAtlasService.DefaultFadeStart, _atlas.FadeStart);
        Assert.AreEqual(RealmAtlasService.DefaultFadeFull, _atlas.FadeFull);
    }

    [TestMethod]
    public void OnMapFrame_FirstBuild_InksTheUnderlaySepiaAndTheCornersCarryIt()
    {
        _atlas.OnMapFrame(Max, Max, true);

        Assert.IsTrue(_builds[0].SelectMany(t => t).All(q => q.NearStart.Colour == RealmAtlasService.DefaultInk),
            "the corners' colour is the ink underlay's; the banner material ignores it for the paper");
    }

    [TestMethod]
    public void UseTint_RebuildsInThosePaperAndInkColoursAndKeepsThemOpaque()
    {
        _atlas.OnMapFrame(Max, Max, true);

        _atlas.UseTint(0x00654321u, 0x00123456u);
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.Received(1).RemoveSheet();
        Assert.AreEqual(0xFF654321u, _atlas.Paper);
        Assert.AreEqual(0xFF123456u, _atlas.Ink);
        _renderer.Received(1).SetSheet(Arg.Any<IReadOnlyList<IReadOnlyList<BorderQuad>>>(), Arg.Any<string>(), 0xFF654321u);
        Assert.IsTrue(_builds[1].SelectMany(t => t).All(q => q.FarEnd.Colour == 0xFF123456u));
    }

    [TestMethod]
    public void UseTint_PaperOnly_KeepsTheInk()
    {
        _atlas.UseTint(0x00654321u, null);

        Assert.AreEqual(0xFF654321u, _atlas.Paper);
        Assert.AreEqual(RealmAtlasService.DefaultInk, _atlas.Ink);
    }

    [TestMethod]
    public void Status_NamesTheZoomAndTheFade()
    {
        _atlas.OnMapFrame(450f, Max, true);

        string status = _atlas.Status();

        StringAssert.Contains(status, "camera 450 of 500");
        StringAssert.Contains(status, "0.8");
    }

    [TestMethod]
    public void Status_BeforeAnyMapFrame_SaysWhatIsNotKnownYet()
    {
        string status = _atlas.Status();

        StringAssert.Contains(status, "camera unknown of unknown");
        StringAssert.Contains(status, "not built yet");
    }

    [TestMethod]
    public void Status_ParchmentMapOff_SaysMcmHasItOff()
    {
        _settings.ParchmentMap.Returns(false);

        StringAssert.Contains(_atlas.Status(), "off in MCM");
    }

    [TestMethod]
    public void OnMapFrame_BuildThrowsPartway_RemovesWhatItBuiltAndShowsNothing()
    {
        _renderer.SetSheet(Arg.Any<IReadOnlyList<IReadOnlyList<BorderQuad>>>(), Arg.Any<string>(), Arg.Any<uint>())
            .Returns(_ =>
            {
                _built = true; // some tiles were added before the throw
                throw new InvalidOperationException("mid-build");
            });

        _atlas.OnMapFrame(Max, Max, true);
        _atlas.OnMapFrame(Max, Max, true);

        _renderer.Received().RemoveSheet();
        _renderer.DidNotReceive().SetSheetAlpha(Arg.Is<float>(a => a > 0f));
    }
}
