using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.WarPledge;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>#1113: war_pledge.json is validated at load, one test per rule (config-providers.md).</summary>
[TestClass]
public class WarPledgeConfigProviderTests
{
    private string _moduleDataPath = null!;
    private IModLogger _logger = null!;
    private WarPledgeConfigProvider _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _moduleDataPath = Path.Combine(Path.GetTempPath(), "taom_war_pledge_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_moduleDataPath, "diplomacy"));
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(_moduleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new WarPledgeConfigProvider(paths, _logger);
    }

    [TestCleanup]
    public void CleanUp()
    {
        if (Directory.Exists(_moduleDataPath))
            Directory.Delete(_moduleDataPath, recursive: true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_moduleDataPath, "diplomacy", "war_pledge.json"), json);

    private const string TwoLeans =
        @"""leans"": [ { ""kingdom"": ""rhudaur"", ""lean"": ""Evil"" }, { ""kingdom"": ""cardolan"", ""lean"": ""Free"" } ]";

    [TestMethod]
    public void LoadConfig_ValidFile_ReadsEveryValueAndKeepsTheRowOrder()
    {
        WriteConfig(@"{ ""losingBalance"": 40, ""daysBetweenAiJoins"": 12, " + TwoLeans + " }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(40, config.LosingBalance);
        Assert.AreEqual(12, config.DaysBetweenAiJoins);
        CollectionAssert.AreEqual(new[] { "rhudaur", "cardolan" }, config.Leans.Select(l => l.KingdomId).ToList());
        Assert.AreEqual(FactionSide.Evil, config.Leans[0].Lean);
        Assert.AreEqual(FactionSide.Free, config.Leans[1].Lean);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void LoadConfig_MissingFile_GivesTheDefaultsAnEmptyTableAndAWarning()
    {
        var config = _sut.LoadConfig();

        Assert.AreEqual(WarPledgeConfig.DefaultLosingBalance, config.LosingBalance);
        Assert.AreEqual(WarPledgeConfig.DefaultDaysBetweenAiJoins, config.DaysBetweenAiJoins);
        Assert.AreEqual(0, config.Leans.Count);
        _logger.ReceivedWithAnyArgs(1).LogWarning(default);
    }

    [DataTestMethod]
    [DataRow("{ not json", DisplayName = "malformed")]
    [DataRow(@"{ ""losingBalance"": ""many"" }", DisplayName = "a word for a number")]
    [DataRow(@"{ ""losingBalance"": 25.5 }", DisplayName = "a fraction for a whole number")]
    [DataRow(@"{ ""leans"": 7 }", DisplayName = "a number for the table")]
    public void LoadConfig_UnreadableFile_GivesTheDefaultsAndAnError(string json)
    {
        WriteConfig(json);

        var config = _sut.LoadConfig();

        Assert.AreEqual(WarPledgeConfig.DefaultLosingBalance, config.LosingBalance);
        Assert.AreEqual(0, config.Leans.Count);
        _logger.ReceivedWithAnyArgs(1).LogError(default);
    }

    [TestMethod]
    public void LoadConfig_TheLiteralNull_GivesTheDefaults()
    {
        WriteConfig("null");

        var config = _sut.LoadConfig();

        Assert.AreEqual(WarPledgeConfig.DefaultLosingBalance, config.LosingBalance);
        Assert.AreEqual(WarPledgeConfig.DefaultDaysBetweenAiJoins, config.DaysBetweenAiJoins);
        Assert.AreEqual(0, config.Leans.Count);
    }

    [TestMethod]
    public void LoadConfig_NumbersAbsent_TakeTheDefaultsWithoutAWarning()
    {
        WriteConfig("{ " + TwoLeans + " }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(WarPledgeConfig.DefaultLosingBalance, config.LosingBalance);
        Assert.AreEqual(WarPledgeConfig.DefaultDaysBetweenAiJoins, config.DaysBetweenAiJoins);
        Assert.AreEqual(2, config.Leans.Count);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-25)]
    [DataRow(101)]
    public void LoadConfig_LosingBalanceOutsideItsRange_RevertsToTheDefault(int value)
    {
        WriteConfig(@"{ ""losingBalance"": " + value + ", " + TwoLeans + " }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(WarPledgeConfig.DefaultLosingBalance, config.LosingBalance);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("losingBalance")));
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(100)]
    public void LoadConfig_LosingBalanceAtAnEdgeOfItsRange_IsKept(int value)
    {
        WriteConfig(@"{ ""losingBalance"": " + value + ", " + TwoLeans + " }");

        Assert.AreEqual(value, _sut.LoadConfig().LosingBalance);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3650)]
    public void LoadConfig_DaysBetweenAiJoinsAtAnEdgeOfItsRange_IsKept(int value)
    {
        WriteConfig(@"{ ""daysBetweenAiJoins"": " + value + ", " + TwoLeans + " }");

        Assert.AreEqual(value, _sut.LoadConfig().DaysBetweenAiJoins);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(3651)]
    public void LoadConfig_DaysBetweenAiJoinsOutsideItsRange_RevertsToTheDefault(int value)
    {
        WriteConfig(@"{ ""daysBetweenAiJoins"": " + value + ", " + TwoLeans + " }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(WarPledgeConfig.DefaultDaysBetweenAiJoins, config.DaysBetweenAiJoins);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("daysBetweenAiJoins")));
    }

    [TestMethod]
    public void LoadConfig_LeansMissing_GivesAnEmptyTableAndAWarning()
    {
        WriteConfig(@"{ ""losingBalance"": 30 }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(30, config.LosingBalance);
        Assert.AreEqual(0, config.Leans.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'leans' is missing")));
    }

    // Each bad row is dropped alone; the two good rows beside it load.
    [DataTestMethod]
    [DataRow(@"null", DisplayName = "null row")]
    [DataRow(@"{ ""lean"": ""Evil"" }", DisplayName = "no kingdom")]
    [DataRow(@"{ ""kingdom"": ""  "", ""lean"": ""Evil"" }", DisplayName = "blank kingdom")]
    [DataRow(@"{ ""kingdom"": ""umbar"" }", DisplayName = "no lean")]
    [DataRow(@"{ ""kingdom"": ""umbar"", ""lean"": ""Evli"" }", DisplayName = "mistyped lean")]
    [DataRow(@"{ ""kingdom"": ""umbar"", ""lean"": ""1"" }", DisplayName = "a number for a lean")]
    [DataRow(@"{ ""kingdom"": ""rhudaur"", ""lean"": ""Free"" }", DisplayName = "second row of a kingdom")]
    [DataRow(@"{ ""kingdom"": ""RHUDAUR"", ""lean"": ""Free"" }", DisplayName = "second row in another case")]
    public void LoadConfig_ABadLeanRow_IsDroppedAloneWithAWarningAndASummary(string badRow)
    {
        WriteConfig(@"{ ""leans"": [ { ""kingdom"": ""rhudaur"", ""lean"": ""Evil"" }, " + badRow
                    + @", { ""kingdom"": ""cardolan"", ""lean"": ""Free"" } ] }");

        var config = _sut.LoadConfig();

        CollectionAssert.AreEqual(new[] { "rhudaur", "cardolan" }, config.Leans.Select(l => l.KingdomId).ToList());
        Assert.AreEqual(FactionSide.Evil, config.Leans[0].Lean, "the first row of a kingdom stands");
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("reverted or dropped")));
    }

    [DataTestMethod]
    [DataRow("free", FactionSide.Free)]
    [DataRow("EVIL", FactionSide.Evil)]
    [DataRow(" Neutral ", FactionSide.Neutral)]
    public void LoadConfig_LeanText_IsReadWithoutCaseAndTrimmed(string text, FactionSide expected)
    {
        WriteConfig(@"{ ""leans"": [ { ""kingdom"": "" dorwinion "", ""lean"": """ + text + @""" } ] }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1, config.Leans.Count);
        Assert.AreEqual("dorwinion", config.Leans[0].KingdomId);
        Assert.AreEqual(expected, config.Leans[0].Lean);
    }
}
