using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BattleBalance;

namespace TAOM.Tests.Features.BattleBalance;

/// <summary>
/// One test per rejection branch of <c>BattleBalanceConfigProvider.Validate</c> (#1291). The shipped file is
/// covered by <c>ShippedBattleBalanceConfigTests</c>; these prove that a bad value is reverted, and to what.
/// </summary>
[TestClass]
public class BattleBalanceConfigProviderValidationTests
{
    private IModLogger _logger;
    private BattleBalanceConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new BattleBalanceConfigProvider(Substitute.For<IPathService>(), _logger);
    }

    private static float CompiledSurvival(string culture) =>
        new BattleBalanceConfig().CasualtyRatios.CulturalSurvivalBonuses[culture];

    [TestMethod]
    public void Validate_CompiledDefaults_ChangeNothingAndWarnNothing()
    {
        var result = _sut.Validate(new BattleBalanceConfig());

        Assert.AreEqual(CompiledSurvival("gondor"), result.CasualtyRatios.CulturalSurvivalBonuses["gondor"], 0.0001f);
        Assert.AreEqual(2.91f, result.TroopPower.TierPower["T7"], 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(2f)]
    [DataRow(-1.5f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Validate_SurvivalBonusOutsideMinusOneToOne_RevertsToTheCompiledDefault(float bad)
    {
        var config = new BattleBalanceConfig();
        config.CasualtyRatios.CulturalSurvivalBonuses["mirkwood"] = bad;

        var result = _sut.Validate(config);

        Assert.AreEqual(CompiledSurvival("mirkwood"), result.CasualtyRatios.CulturalSurvivalBonuses["mirkwood"], 0.0001f);
    }

    [TestMethod]
    public void Validate_NegativeCultureOutOfRange_KeepsItsCompiledPenalty()
    {
        var config = new BattleBalanceConfig();
        config.CasualtyRatios.CulturalSurvivalBonuses["gundabad"] = float.NaN;

        var result = _sut.Validate(config);

        Assert.AreEqual(-0.1f, result.CasualtyRatios.CulturalSurvivalBonuses["gundabad"], 0.0001f);
    }

    [TestMethod]
    public void Validate_SurvivalBonusOfACultureWithNoCompiledRow_RemovesTheRowSoTheBonusIsZero()
    {
        var config = new BattleBalanceConfig();
        config.CasualtyRatios.CulturalSurvivalBonuses["mordor"] = 7f;

        var result = _sut.Validate(config);

        Assert.IsFalse(result.CasualtyRatios.CulturalSurvivalBonuses.ContainsKey("mordor"));
        Assert.AreEqual(0f, result.CasualtyRatios.GetCulturalSurvivalBonus("mordor"), 0.0001f);
    }

    // T11 has no compiled row, so a bad T11 must fall to GetTierPower's formula, (2 + 11) * (10 + 11) * 0.02.
    [TestMethod]
    public void Validate_TierPowerWithNoCompiledRow_RemovesTheRowSoTheFormulaDecides()
    {
        var config = new BattleBalanceConfig();
        config.TroopPower.TierPower["T11"] = float.NaN;

        var result = _sut.Validate(config);

        Assert.IsFalse(result.TroopPower.TierPower.ContainsKey("T11"));
        Assert.AreEqual(13f * 21f * 0.02f, result.TroopPower.GetTierPower(11), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(-1f)]
    [DataRow(1f)]
    [DataRow(0.35f)]
    public void Validate_SurvivalBonusInsideTheRange_IsKept(float good)
    {
        var config = new BattleBalanceConfig();
        config.CasualtyRatios.CulturalSurvivalBonuses["gondor"] = good;

        var result = _sut.Validate(config);

        Assert.AreEqual(good, result.CasualtyRatios.CulturalSurvivalBonuses["gondor"], 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Validate_TierPowerNotFinite_RevertsToTheCompiledDefault()
    {
        var config = new BattleBalanceConfig();
        config.TroopPower.TierPower["T7"] = float.NaN;

        var result = _sut.Validate(config);

        Assert.AreEqual(2.91f, result.TroopPower.TierPower["T7"], 0.0001f);
    }

    [TestMethod]
    public void Validate_TwoRejectedValues_WarnOncePerValueAndOnceInSummary()
    {
        var config = new BattleBalanceConfig();
        config.TroopPower.TierPower["T7"] = 0f;
        config.CasualtyRatios.CulturalSurvivalBonuses["gondor"] = 3f;

        _sut.Validate(config);

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("TierPower[T7]")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("CulturalSurvivalBonuses[gondor]")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("2 value(s)")));
    }

    [TestMethod]
    public void Validate_NullCasualtyRatiosSection_RevertsToTheCompiledSection()
    {
        var config = new BattleBalanceConfig { CasualtyRatios = null };

        var result = _sut.Validate(config);

        Assert.AreEqual(CompiledSurvival("gondor"), result.CasualtyRatios.GetCulturalSurvivalBonus("gondor"), 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("CasualtyRatios is null")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("1 value(s)")));
    }

    [TestMethod]
    public void Validate_NullSurvivalTable_RevertsToTheCompiledTable()
    {
        var config = new BattleBalanceConfig();
        config.CasualtyRatios.CulturalSurvivalBonuses = null;

        var result = _sut.Validate(config);

        Assert.AreEqual(CompiledSurvival("gondor"), result.CasualtyRatios.GetCulturalSurvivalBonus("gondor"), 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("CulturalSurvivalBonuses is null")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("1 value(s)")));
    }

    [TestMethod]
    public void Validate_NullTroopPowerSection_RevertsToTheCompiledSection()
    {
        var config = new BattleBalanceConfig { TroopPower = null };

        var result = _sut.Validate(config);

        Assert.AreEqual(2.91f, result.TroopPower.GetTierPower(7), 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("TroopPower is null")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("1 value(s)")));
    }

    [TestMethod]
    public void Validate_NullTierPowerTable_RevertsToTheCompiledTable()
    {
        var config = new BattleBalanceConfig();
        config.TroopPower.TierPower = null;

        var result = _sut.Validate(config);

        Assert.AreEqual(2.91f, result.TroopPower.GetTierPower(7), 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("TierPower is null")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("1 value(s)")));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-0.1f)]
    [DataRow(1.5f)]
    [DataRow(float.PositiveInfinity)]
    public void Validate_PowerRatioExponentOutsideZeroToOne_RevertsToTheCompiledDefault(float bad)
    {
        var config = new BattleBalanceConfig();
        config.AutoResolve.PowerRatioExponent = bad;

        var result = _sut.Validate(config);

        Assert.AreEqual(0.5f, result.AutoResolve.PowerRatioExponent, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("PowerRatioExponent")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("1 value(s)")));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.7f)]
    [DataRow(1f)]
    public void Validate_PowerRatioExponentInZeroToOne_IsKept(float good)
    {
        var config = new BattleBalanceConfig();
        config.AutoResolve.PowerRatioExponent = good;

        var result = _sut.Validate(config);

        Assert.AreEqual(good, result.AutoResolve.PowerRatioExponent, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Validate_NullAutoResolveSection_RevertsToTheCompiledSection()
    {
        var result = _sut.Validate(new BattleBalanceConfig { AutoResolve = null });

        Assert.AreEqual(0.5f, result.AutoResolve.PowerRatioExponent, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("AutoResolve is null")));
    }

    [TestMethod]
    public void Validate_NullSiegeDefenceSection_RevertsToTheCompiledSection()
    {
        var result = _sut.Validate(new BattleBalanceConfig { SiegeDefence = null });

        Assert.AreEqual(0.1f, result.SiegeDefence.DefenderAdvantageBonuses["bluecraig"], 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("SiegeDefence is null")));
    }

    [TestMethod]
    public void Validate_NullDefenderAdvantageTable_RevertsToTheCompiledTable()
    {
        var config = new BattleBalanceConfig();
        config.SiegeDefence.DefenderAdvantageBonuses = null;

        var result = _sut.Validate(config);

        Assert.AreEqual(0.1f, result.SiegeDefence.DefenderAdvantageBonuses["bluecraig"], 0.0001f);
    }

    [DataTestMethod]
    [DataRow(-0.1f)]
    [DataRow(1.5f)]
    [DataRow(float.NaN)]
    public void Validate_DefenderAdvantageBonusOutsideZeroToOne_RevertsOrRemovesTheRow(float bad)
    {
        var config = new BattleBalanceConfig();
        config.SiegeDefence.DefenderAdvantageBonuses["bluecraig"] = bad;
        config.SiegeDefence.DefenderAdvantageBonuses["gondor"] = bad;

        var result = _sut.Validate(config);

        Assert.AreEqual(0.1f, result.SiegeDefence.DefenderAdvantageBonuses["bluecraig"], 0.0001f);
        Assert.IsFalse(result.SiegeDefence.DefenderAdvantageBonuses.ContainsKey("gondor"));
    }

    [TestMethod]
    public void Validate_NullUpkeepFreeList_RevertsToTheCompiledList()
    {
        var config = new BattleBalanceConfig();
        config.SiegeDefence.UpkeepFreeGarrisons = null;

        var result = _sut.Validate(config);

        Assert.IsTrue(result.SiegeDefence.UpkeepFreeGarrisons["bluecraig"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("SiegeDefence.UpkeepFreeGarrisons is null")));
    }

    [TestMethod]
    public void Validate_NullDailyGarrisonGrowthTable_RevertsToTheCompiledTable()
    {
        var config = new BattleBalanceConfig();
        config.SiegeDefence.DailyGarrisonGrowth = null;

        var result = _sut.Validate(config);

        Assert.AreEqual(2f, result.SiegeDefence.DailyGarrisonGrowth["bluecraig"], 0.0001f);
    }

    [DataTestMethod]
    [DataRow(-1f)]
    [DataRow(21f)]
    [DataRow(float.NaN)]
    public void Validate_DailyGarrisonGrowthOutsideZeroToTwenty_RevertsOrRemovesTheRow(float bad)
    {
        var config = new BattleBalanceConfig();
        config.SiegeDefence.DailyGarrisonGrowth["bluecraig"] = bad;
        config.SiegeDefence.DailyGarrisonGrowth["gondor"] = bad;

        var result = _sut.Validate(config);

        Assert.AreEqual(2f, result.SiegeDefence.DailyGarrisonGrowth["bluecraig"], 0.0001f);
        Assert.IsFalse(result.SiegeDefence.DailyGarrisonGrowth.ContainsKey("gondor"));
    }

    // The tests above build the config in C#. These two go through the file and Newtonsoft, which merges JSON
    // keys into the compiled dictionaries and sets a section to null on a JSON null.
    [TestMethod]
    public void GetConfig_FileWithAnOutOfRangePenalty_KeepsTheCompiledPenalty()
    {
        var result = LoadFromFile("{\"CasualtyRatios\":{\"CulturalSurvivalBonuses\":{\"gundabad\":5}}}");

        Assert.AreEqual(-0.1f, result.CasualtyRatios.GetCulturalSurvivalBonus("gundabad"), 0.0001f);
        Assert.AreEqual(CompiledSurvival("gondor"), result.CasualtyRatios.GetCulturalSurvivalBonus("gondor"), 0.0001f);
    }

    [TestMethod]
    public void GetConfig_FileWithANullSection_UsesTheCompiledSection()
    {
        var result = LoadFromFile("{\"CasualtyRatios\":null}");

        Assert.IsNotNull(result.CasualtyRatios);
        Assert.AreEqual(CompiledSurvival("gondor"), result.CasualtyRatios.GetCulturalSurvivalBonus("gondor"), 0.0001f);
    }

    // A null cannot become a float, so the parse throws and the whole file falls back, behind an error.
    [TestMethod]
    public void GetConfig_FileWithANullValue_FallsBackToTheCompiledConfigWithAnError()
    {
        var result = LoadFromFile("{\"CasualtyRatios\":{\"CulturalSurvivalBonuses\":{\"gondor\":null,\"rivendell\":0.9}}}");

        Assert.AreEqual(CompiledSurvival("gondor"), result.CasualtyRatios.GetCulturalSurvivalBonus("gondor"), 0.0001f);
        Assert.AreEqual(CompiledSurvival("rivendell"), result.CasualtyRatios.GetCulturalSurvivalBonus("rivendell"), 0.0001f);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    private BattleBalanceConfig LoadFromFile(string json)
    {
        var root = Path.Combine(Path.GetTempPath(), "taom-bbc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "configs"));
        try
        {
            File.WriteAllText(Path.Combine(root, "configs", "battle_balance_config.json"), json);
            var paths = Substitute.For<IPathService>();
            paths.ModuleDataPath.Returns(root);
            return new BattleBalanceConfigProvider(paths, _logger).GetConfig();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
