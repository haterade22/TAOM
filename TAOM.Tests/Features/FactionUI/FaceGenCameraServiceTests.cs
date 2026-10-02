using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.CharacterCreation;
using TAOM.Features.FactionUI.Menus;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Kysaro tuned the camera for each themed character-creation screen; the offsets must
/// reach the screen they were tuned for and no other, and a bad value in the file must not move the
/// camera (TAOM's config-provider rule: range-check, reject NaN, warn, fall back).
/// </summary>
[TestClass]
public class FaceGenCameraServiceTests
{
    private const string Root = @"C:\Game\Modules\TAOM";

    private FakeFrontEndResourceAdapter _adapter = null!;
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
        _logger = Substitute.For<IModLogger>();
        WriteConfig(@"{
            ""FaceGen"":   { ""x_offset"": -0.2 },
            ""Narrative"": { ""x_offset"": -0.5, ""distance_offset"": 0.5, ""fov_offset"": 0.075 },
            ""Review"":    { ""x_offset"": -0.6, ""distance_offset"": 0.5, ""fov_offset"": 0.075 }
        }");
    }

    private void WriteConfig(string json) =>
        _adapter.AddFile(Path.Combine(_paths.ConfigDirectory, FaceGenCameraConfigProvider.FileName), json);

    private FaceGenCameraService CreateSut() => new(new FaceGenCameraConfigProvider(_adapter, _paths, _logger));

    [TestMethod]
    public void OnFaceGeneratorOpening_Themed_AppliesTheFaceGenOffsets()
    {
        var sut = CreateSut();

        sut.OnFaceGeneratorOpening(themed: true);

        Assert.AreEqual(-0.2f, sut.CurrentOffsets.X, 0.0001f);
        Assert.AreEqual(0f, sut.CurrentOffsets.Distance);
    }

    [TestMethod]
    public void OnFaceGeneratorOpening_Vanilla_LeavesTheCameraAlone()
    {
        var sut = CreateSut();
        sut.OnFaceGeneratorOpening(themed: true);

        sut.OnFaceGeneratorOpening(themed: false);

        Assert.AreEqual(CameraOffsets.None.X, sut.CurrentOffsets.X);
        Assert.AreEqual("", sut.CurrentScreen);
    }

    [DataTestMethod]
    [DataRow(FrontEndMovieService.NarrativeMovie, -0.5f, 0.5f, 0.075f)]
    [DataRow(FrontEndMovieService.ReviewMovie, -0.6f, 0.5f, 0.075f)]
    [DataRow(FrontEndMovieService.OptionsMovie, 0f, 0f, 0f)]
    public void OnMovieLoaded_AThemedStage_SelectsItsOffsets(string movie, float x, float distance, float fov)
    {
        var sut = CreateSut();

        sut.OnMovieLoaded(movie);

        Assert.AreEqual(x, sut.CurrentOffsets.X, 0.0001f);
        Assert.AreEqual(distance, sut.CurrentOffsets.Distance, 0.0001f);
        Assert.AreEqual(fov, sut.CurrentOffsets.Fov, 0.0001f);
    }

    [DataTestMethod]
    [DataRow("CharacterCreationNarrativeStage")]
    [DataRow("CharacterCreationReviewStage")]
    [DataRow(FrontEndMovieService.ClanNamingMovie)]
    public void OnMovieLoaded_AVanillaOrUntunedStage_ClearsTheOffsets(string movie)
    {
        var sut = CreateSut();
        sut.OnMovieLoaded(FrontEndMovieService.NarrativeMovie);

        sut.OnMovieLoaded(movie);

        Assert.AreEqual("", sut.CurrentScreen);
        Assert.AreEqual(0f, sut.CurrentOffsets.X);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("EscapeMenu")]
    [DataRow("PartyScreen")]
    public void OnMovieLoaded_AMovieThatIsNotAStage_KeepsTheCurrentScreen(string? movie)
    {
        var sut = CreateSut();
        sut.OnMovieLoaded(FrontEndMovieService.NarrativeMovie);

        sut.OnMovieLoaded(movie);

        Assert.AreEqual(FaceGenCameraService.NarrativeScreen, sut.CurrentScreen);
    }

    [DataTestMethod]
    [DataRow("\"NaN\"")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("99")]
    [DataRow("-99")]
    [DataRow("\"left\"")]
    [DataRow("true")]
    public void OffsetsFor_AValueThatIsNotAFiniteNumberInRange_FallsBackToZeroAndWarns(string value)
    {
        WriteConfig("{ \"Narrative\": { \"x_offset\": " + value + ", \"distance_offset\": 0.5 } }");
        var sut = CreateSut();

        sut.OnMovieLoaded(FrontEndMovieService.NarrativeMovie);

        Assert.AreEqual(0f, sut.CurrentOffsets.X);
        Assert.AreEqual(0.5f, sut.CurrentOffsets.Distance, 0.0001f, "a bad value reverts alone, not its neighbours");
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("Narrative.x_offset")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("4")]
    [DataRow("-3.5")]
    [DataRow("\"far\"")]
    public void OffsetsFor_ADistanceThatIsNotAFiniteNumberInRange_FallsBackToZeroAndWarns(string value)
    {
        WriteConfig("{ \"Narrative\": { \"distance_offset\": " + value + ", \"x_offset\": -0.5 } }");
        var sut = CreateSut();

        sut.OnMovieLoaded(FrontEndMovieService.NarrativeMovie);

        Assert.AreEqual(0f, sut.CurrentOffsets.Distance);
        Assert.AreEqual(-0.5f, sut.CurrentOffsets.X, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("Narrative.distance_offset")));
    }

    [DataTestMethod]
    [DataRow("1.5")]
    [DataRow("-0.75")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    public void OffsetsFor_AFovBeyondItsRange_FallsBackToZeroAndWarns(string value)
    {
        WriteConfig("{ \"Review\": { \"fov_offset\": " + value + " } }");
        var sut = CreateSut();

        sut.OnMovieLoaded(FrontEndMovieService.ReviewMovie);

        Assert.AreEqual(0f, sut.CurrentOffsets.Fov);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("Review.fov_offset")));
    }

    [TestMethod]
    public void OffsetsFor_AnyRevertedValue_EndsWithOneSummaryWarning()
    {
        WriteConfig("{ \"Review\": { \"fov_offset\": 9, \"x_offset\": 9 } }");
        var sut = CreateSut();

        sut.OnMovieLoaded(FrontEndMovieService.ReviewMovie);
        _ = sut.CurrentOffsets;

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("2 entries reverted or ignored")));
    }

    [DataTestMethod]
    [DataRow("{ \"Facegen\": { \"x_offset\": 0.2 } }", "Facegen")]
    [DataRow("{ \"FaceGen\": 0.2 }", "FaceGen")]
    [DataRow("{ \"Narrative\": { \"x_ofset\": 0.2 } }", "x_ofset")]
    public void OffsetsFor_AMisspeltOrMisshapenEntry_IsIgnoredWithAWarning(string json, string named)
    {
        WriteConfig(json);
        var sut = CreateSut();

        sut.OnFaceGeneratorOpening(themed: true);

        Assert.AreEqual(0f, sut.CurrentOffsets.X);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(named)));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("1 entry reverted or ignored")));
    }

    [TestMethod]
    public void OffsetsFor_NotesAreIgnoredWithoutAWarning()
    {
        WriteConfig("{ \"_readme\": \"notes\", \"FaceGen\": { \"x_offset\": -0.2 } }");
        var sut = CreateSut();

        sut.OnFaceGeneratorOpening(themed: true);

        Assert.AreEqual(-0.2f, sut.CurrentOffsets.X, 0.0001f, "the file was read");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void OffsetsFor_MalformedJson_LeavesEveryCameraAtVanilla()
    {
        WriteConfig("{ not json");
        var sut = CreateSut();

        sut.OnMovieLoaded(FrontEndMovieService.NarrativeMovie);

        Assert.AreEqual(0f, sut.CurrentOffsets.X);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not valid JSON")));
    }

    [TestMethod]
    public void OffsetsFor_NoConfigFile_LeavesEveryCameraAtVanilla()
    {
        _adapter.Files.Clear();
        var sut = CreateSut();

        sut.OnFaceGeneratorOpening(themed: true);

        Assert.AreEqual(0f, sut.CurrentOffsets.X);
    }
}
