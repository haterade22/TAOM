using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Messengers;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerConfigProviderTests
{
    private string _tempDir = null!;
    private string _messengersDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private MessengerConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Messengers_" + Path.GetRandomFileName());
        _messengersDir = Path.Combine(_tempDir, "messengers");
        Directory.CreateDirectory(_messengersDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new MessengerConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_messengersDir, "messenger_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""accidentChancePerHour"": 0.005,
  ""travelSpeedMultiplier"": 1.5
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.005f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.5f, config.TravelSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsInfo()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_NegativeAccidentChance_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": -0.1 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour=-0.1")));
    }

    [TestMethod]
    public void GetConfig_AccidentChanceAboveOne_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 1.5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour=1.5")));
    }

    [TestMethod]
    public void GetConfig_ZeroSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": 0.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=0")));
    }

    [TestMethod]
    public void GetConfig_NegativeSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": -1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=-1")));
    }

    [TestMethod]
    public void GetConfig_AbsurdSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": 100.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=100")));
    }

    [TestMethod]
    public void GetConfig_BoundaryAccidentChanceZero_Accepted()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.0f, config.AccidentChancePerHour, 0.00001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_BoundaryAccidentChanceOne_Accepted()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.AccidentChancePerHour, 0.00001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_PartialJson_MergesWithDefaults()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.01 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.01f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.005 }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.003, ""travelSpeedMultiplier"": 1.2 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside")));
    }

    [TestMethod]
    public void GetConfig_NaNAccidentChance_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": ""NaN"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_InfinitySpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": ""Infinity"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier")));
    }

    [TestMethod]
    public void GetConfig_NegativeInfinitySpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": ""-Infinity"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier")));
    }

    [TestMethod]
    public void DefaultConfig_MatchesLOTRAOMSpec()
    {
        var config = new MessengerConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f,
            "Default accident chance should be 0.2%/hr to match LOTRAOM");
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f,
            "Default speed multiplier should be 1.0 (no scaling)");
    }
}
