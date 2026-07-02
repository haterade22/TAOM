using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SettlementEconomy;

namespace TAOM.Tests.Features.SettlementEconomy;

[TestClass]
public class SettlementEconomyConfigProviderTests
{
    private string _tempDir = null!;
    private string _configDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private SettlementEconomyConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_SettlementEconomy_" + Path.GetRandomFileName());
        _configDir = Path.Combine(_tempDir, "settlement_economy");
        Directory.CreateDirectory(_configDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new SettlementEconomyConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_configDir, "settlement_economy_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""townGoldBase"": 30000,
  ""townGoldPerProsperity"": 15,
  ""townGoldRegenRate"": 0.4
}");

        var c = _sut.GetConfig();

        Assert.AreEqual(30000f, c.TownGoldBase, 0.001f);
        Assert.AreEqual(15f, c.TownGoldPerProsperity, 0.001f);
        Assert.AreEqual(0.4f, c.TownGoldRegenRate, 0.001f);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var c = _sut.GetConfig();

        Assert.AreEqual(25000f, c.TownGoldBase, 0.001f);
        Assert.AreEqual(12f, c.TownGoldPerProsperity, 0.001f);
        Assert.AreEqual(0.25f, c.TownGoldRegenRate, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var c = _sut.GetConfig();

        Assert.AreEqual(25000f, c.TownGoldBase, 0.001f);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_PartialJson_MergesWithDefaults()
    {
        WriteConfig(@"{ ""townGoldRegenRate"": 0.35 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.35f, c.TownGoldRegenRate, 0.001f);
        Assert.AreEqual(25000f, c.TownGoldBase, 0.001f);
        Assert.AreEqual(12f, c.TownGoldPerProsperity, 0.001f);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""townGoldBase"": 20000 }");

        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
    }

    [TestMethod]
    public void GetConfig_NegativeTownGoldBase_RevertsToDefaultAndWarns()
    {
        // A negative base could make regen permanently negative at low prosperity —
        // reintroducing the exact bug this feature exists to fix.
        WriteConfig(@"{ ""townGoldBase"": -5000 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(25000f, c.TownGoldBase, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldBase")));
    }

    [TestMethod]
    public void GetConfig_OversizedTownGoldBase_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""townGoldBase"": 5000000 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(25000f, c.TownGoldBase, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldBase")));
    }

    [TestMethod]
    public void GetConfig_NaNTownGoldBase_RevertsToFiniteDefault()
    {
        WriteConfig(@"{ ""townGoldBase"": NaN }");

        var c = _sut.GetConfig();

        Assert.AreEqual(25000f, c.TownGoldBase, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldBase")));
    }

    [TestMethod]
    public void GetConfig_NegativeTownGoldPerProsperity_RevertsToDefaultAndWarns()
    {
        // Negative slope inverts prosperity's meaning (sign-flip rule, csharp-architecture.md).
        WriteConfig(@"{ ""townGoldPerProsperity"": -12 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(12f, c.TownGoldPerProsperity, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldPerProsperity")));
    }

    [TestMethod]
    public void GetConfig_OversizedTownGoldPerProsperity_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""townGoldPerProsperity"": 500 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(12f, c.TownGoldPerProsperity, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldPerProsperity")));
    }

    [TestMethod]
    public void GetConfig_InfinityTownGoldPerProsperity_RevertsToFiniteDefault()
    {
        WriteConfig(@"{ ""townGoldPerProsperity"": Infinity }");

        var c = _sut.GetConfig();

        Assert.AreEqual(12f, c.TownGoldPerProsperity, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldPerProsperity")));
    }

    [TestMethod]
    public void GetConfig_NegativeRegenRate_RevertsToDefaultAndWarns()
    {
        // A negative rate inverts mean-reversion: towns would diverge from the target instead
        // of converging — the model would itself drain towns.
        WriteConfig(@"{ ""townGoldRegenRate"": -0.25 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.25f, c.TownGoldRegenRate, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldRegenRate")));
    }

    [TestMethod]
    public void GetConfig_RegenRateAboveOne_RevertsToDefaultAndWarns()
    {
        // rate > 1 overshoots the equilibrium target and oscillates.
        WriteConfig(@"{ ""townGoldRegenRate"": 1.5 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.25f, c.TownGoldRegenRate, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldRegenRate")));
    }

    [TestMethod]
    public void GetConfig_NaNRegenRate_RevertsToFiniteDefault()
    {
        WriteConfig(@"{ ""townGoldRegenRate"": NaN }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.25f, c.TownGoldRegenRate, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townGoldRegenRate")));
    }

    [TestMethod]
    public void GetConfig_ZeroRegenRate_AcceptedAsExtremeTuning()
    {
        // 0 is the deliberate inclusive lower bound: "freeze regen entirely" is arithmetically
        // sane and an explicit user choice, unlike a sign flip.
        WriteConfig(@"{ ""townGoldRegenRate"": 0 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0f, c.TownGoldRegenRate, 0.001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("townGoldRegenRate")));
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""townGoldBase"": 25000, ""townGoldPerProsperity"": 12, ""townGoldRegenRate"": 0.25 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
