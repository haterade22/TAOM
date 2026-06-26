using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.NavalTravel;

namespace TAOM.Tests.Features.NavalTravel;

[TestClass]
public class NavalTravelConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private NavalTravelConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_NavalTravel_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "naval_travel");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new NavalTravelConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "naval_travel_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidFile_ParsesAllFields()
    {
        WriteConfig(@"{ ""enabled"": false, ""applyToPlayer"": true, ""applyToAi"": false,
            ""embarkThresholdDistance"": 0.75, ""navalTerrainTypeIds"": [10, 11] }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.IsTrue(config.ApplyToPlayer);
        Assert.IsFalse(config.ApplyToAi);
        Assert.AreEqual(0.75f, config.EmbarkThresholdDistance);
        CollectionAssert.AreEqual(new[] { 10, 11 }, config.NavalTerrainTypeIds);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled); // default is false — feature parked pending navmesh (#296/#120)
        Assert.IsTrue(config.ApplyToPlayer);
        Assert.IsTrue(config.ApplyToAi);
        Assert.AreEqual(NavalTravelConfig.DefaultEmbarkThresholdDistance, config.EmbarkThresholdDistance);
        CollectionAssert.AreEqual(NavalTravelConfig.DefaultNavalTerrainTypeIds(), config.NavalTerrainTypeIds);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled); // default is false — feature parked pending navmesh (#296/#120)
        Assert.AreEqual(NavalTravelConfig.DefaultEmbarkThresholdDistance, config.EmbarkThresholdDistance);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsDefaults()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled); // default is false — feature parked pending navmesh (#296/#120)
        Assert.AreEqual(NavalTravelConfig.DefaultEmbarkThresholdDistance, config.EmbarkThresholdDistance);
        CollectionAssert.AreEqual(NavalTravelConfig.DefaultNavalTerrainTypeIds(), config.NavalTerrainTypeIds);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    // --- EmbarkThresholdDistance validation: one case per failure mode ---
    [TestMethod]
    public void GetConfig_NaNThreshold_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""embarkThresholdDistance"": ""NaN"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(NavalTravelConfig.DefaultEmbarkThresholdDistance, config.EmbarkThresholdDistance);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("embarkThresholdDistance")));
    }

    [TestMethod]
    public void GetConfig_NegativeThreshold_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""embarkThresholdDistance"": -2.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(NavalTravelConfig.DefaultEmbarkThresholdDistance, config.EmbarkThresholdDistance);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("embarkThresholdDistance")));
    }

    [TestMethod]
    public void GetConfig_TooLargeThreshold_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""embarkThresholdDistance"": 9999.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(NavalTravelConfig.DefaultEmbarkThresholdDistance, config.EmbarkThresholdDistance);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("embarkThresholdDistance")));
    }

    // --- NavalTerrainTypeIds validation ---
    [TestMethod]
    public void GetConfig_UnknownTerrainId_DroppedWithWarning()
    {
        WriteConfig(@"{ ""navalTerrainTypeIds"": [10, 999, 11] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { 10, 11 }, config.NavalTerrainTypeIds);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("999")));
    }

    [TestMethod]
    public void GetConfig_DuplicateTerrainIds_Deduplicated()
    {
        WriteConfig(@"{ ""navalTerrainTypeIds"": [10, 10, 11] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { 10, 11 }, config.NavalTerrainTypeIds);
    }

    [TestMethod]
    public void GetConfig_EmptyTerrainIds_RevertsToDefaultSetAndWarns()
    {
        WriteConfig(@"{ ""navalTerrainTypeIds"": [] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(NavalTravelConfig.DefaultNavalTerrainTypeIds(), config.NavalTerrainTypeIds);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("empty")));
    }

    [TestMethod]
    public void GetConfig_AllUnknownTerrainIds_RevertsToDefaultSetAndWarns()
    {
        WriteConfig(@"{ ""navalTerrainTypeIds"": [998, 999] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(NavalTravelConfig.DefaultNavalTerrainTypeIds(), config.NavalTerrainTypeIds);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no valid entries")));
    }

    // --- Boat-visual fields ---
    [TestMethod]
    public void GetConfig_ValidBoatFields_Parsed()
    {
        WriteConfig(@"{ ""renderBoatVisual"": false, ""boatMeshName"": ""custom_boat"", ""boatScale"": 0.7 }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.RenderBoatVisual);
        Assert.AreEqual("custom_boat", config.BoatMeshName);
        Assert.AreEqual(0.7f, config.BoatScale);
    }

    [TestMethod]
    public void GetConfig_DefaultBoatFields_WhenOmitted()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.RenderBoatVisual);
        Assert.AreEqual(NavalTravelConfig.DefaultBoatMeshName, config.BoatMeshName);
        Assert.AreEqual(NavalTravelConfig.DefaultBoatScale, config.BoatScale);
    }

    [DataTestMethod]
    [DataRow("\"NaN\"")]   // NaN
    [DataRow("0")]         // zero (invisible boat)
    [DataRow("-1.0")]      // negative
    [DataRow("9999.0")]    // above MaxBoatScale
    public void GetConfig_InvalidBoatScale_RevertsToDefaultAndWarns(string scaleJson)
    {
        WriteConfig($@"{{ ""boatScale"": {scaleJson} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(NavalTravelConfig.DefaultBoatScale, config.BoatScale);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("boatScale")));
    }

    [TestMethod]
    public void GetConfig_EmptyBoatMeshName_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""boatMeshName"": ""   "" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(NavalTravelConfig.DefaultBoatMeshName, config.BoatMeshName);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("boatMeshName")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig("{}");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }
}
