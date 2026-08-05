using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Semantic-validation tests per the config-provider rule: one test per validation rule,
/// covering parseable-but-invalid values — not just missing/malformed files.
/// </summary>
[TestClass]
public class EnlistmentContentConfigProviderTests
{
    private string _dir = null!;
    private IPathService _paths = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom_enlist_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "enlistment"));
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(_dir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private EnlistmentContentConfigProvider Provider() => new EnlistmentContentConfigProvider(_paths, _logger);

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_dir, "enlistment", "enlistment_config.json"), json);

    private void WriteDuties(string json) =>
        File.WriteAllText(Path.Combine(_dir, "enlistment", "enlistment_duties.json"), json);

    [TestMethod]
    public void GetConfig_MissingFile_DefaultsWithWarning()
    {
        var config = Provider().GetConfig();

        Assert.AreEqual(10, config.Progression.DailyLeadershipXp);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_DefaultsWithError()
    {
        WriteConfig("{ not json !!!");

        var config = Provider().GetConfig();

        Assert.AreEqual(4, config.Progression.DailyWageByRank.Count);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_NaNOfferChance_RevertsWithWarning()
    {
        WriteConfig("{\"scheduler\":{\"baseOfferChance\":\"NaN\"}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(0.06f, config.Scheduler.BaseOfferChance);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("baseOfferChance")));
    }

    [TestMethod]
    public void GetConfig_WageTableWrongLength_Reverts()
    {
        WriteConfig("{\"progression\":{\"dailyWageByRank\":[5,8]}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(4, config.Progression.DailyWageByRank.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("dailyWageByRank")));
    }

    [TestMethod]
    public void GetConfig_NegativeWage_Reverts()
    {
        WriteConfig("{\"progression\":{\"dailyWageByRank\":[5,8,-14,22]}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.Progression.DailyWageByRank[2]);
    }

    [TestMethod]
    public void GetConfig_NonMonotonicPromotionDays_RevertsToDefaultLadder()
    {
        WriteConfig("{\"promotions\":[" +
            "{\"toRank\":\"Soldier\",\"minDaysServed\":25,\"minServiceXp\":100}," +
            "{\"toRank\":\"Veteran\",\"minDaysServed\":7,\"minServiceXp\":350}," +
            "{\"toRank\":\"Sergeant\",\"minDaysServed\":60,\"minServiceXp\":800}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(7, config.Promotions[0].MinDaysServed, "default ladder restored");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("promotions")));
    }

    [TestMethod]
    public void GetConfig_WrongPromotionCount_Reverts()
    {
        WriteConfig("{\"promotions\":[{\"toRank\":\"Soldier\",\"minDaysServed\":7,\"minServiceXp\":100}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(3, config.Promotions.Count);
    }

    [TestMethod]
    public void GetConfig_MeritBandsNotDescending_Reverts()
    {
        WriteConfig("{\"meritBands\":[" +
            "{\"minScore\":40,\"serviceXp\":12},{\"minScore\":80,\"serviceXp\":30}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(4, config.MeritBands.Count, "default bands restored");
        Assert.IsTrue(config.MeritBands[0].MinScore > config.MeritBands[1].MinScore);
    }

    [TestMethod]
    public void GetConfig_ValidFile_LoadsAndKeepsValues()
    {
        WriteConfig("{\"progression\":{\"dailyLeadershipXp\":15}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(15, config.Progression.DailyLeadershipXp);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetDuties_MissingFile_EmptyTablesWithWarning()
    {
        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetDuties_UnknownSkillId_SkipsRowWithWarning()
    {
        // The M1 trap: a typo'd string the consumer branches on must SKIP+warn, never
        // silently take a default path.
        WriteDuties("{\"interactiveDuties\":[{\"id\":\"night_patrol\",\"optionA\":{\"key\":\"patrol\",\"skillId\":\"Scoutting\",\"difficulty\":60},\"optionB\":{\"key\":\"decline\",\"skillId\":\"Athletics\",\"difficulty\":50}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.InteractiveDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("Scoutting")));
    }

    [TestMethod]
    public void GetDuties_UnknownContext_SkipsRowWithWarning()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"bad_ctx\",\"mechanic\":\"VisitSettlement\",\"targetKind\":\"FriendlySettlement\",\"deadlineDays\":4,\"gates\":{\"requiredContexts\":[\"siegee\"]}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("siegee")));
    }

    [TestMethod]
    public void GetDuties_ZeroDeadline_SkipsRow()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"zero_deadline\",\"mechanic\":\"VisitSettlement\",\"targetKind\":\"FriendlySettlement\",\"deadlineDays\":0}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_DuplicateId_SkipsLaterRow()
    {
        WriteDuties("{\"fieldDuties\":[" +
            "{\"id\":\"road_patrol\",\"mechanic\":\"VisitSettlement\",\"targetKind\":\"FriendlySettlement\",\"deadlineDays\":4}," +
            "{\"id\":\"road_patrol\",\"mechanic\":\"CollectFood\",\"targetKind\":\"FriendlyVillage\",\"deadlineDays\":5}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count);
        Assert.AreEqual(DutyMechanic.VisitSettlement, duties.FieldDuties[0].Mechanic);
    }

    [TestMethod]
    public void GetDuties_NegativeReward_SkipsRow()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"neg\",\"mechanic\":\"VisitSettlement\",\"targetKind\":\"FriendlySettlement\",\"deadlineDays\":4,\"reportReward\":{\"gold\":-5}}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_ValidRows_Load()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"road_patrol\",\"mechanic\":\"VisitSettlement\",\"targetKind\":\"FriendlySettlement\",\"deadlineDays\":4,\"reportReward\":{\"serviceXp\":48,\"gold\":55}}]," +
            "\"incidents\":[{\"id\":\"pay_delay\",\"chance\":0.22,\"effect\":\"ReleaseDeferredPay\",\"optionA\":{\"key\":\"press\",\"skillId\":\"Charm\",\"difficulty\":65},\"optionB\":{\"key\":\"wait\",\"skillId\":\"Steward\",\"difficulty\":50}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count);
        Assert.AreEqual(1, duties.Incidents.Count);
        Assert.AreEqual("ReleaseDeferredPay", duties.Incidents[0].Effect);
    }

    [TestMethod]
    public void GetDuties_NaNIncidentChance_SkipsRow()
    {
        WriteDuties("{\"incidents\":[{\"id\":\"nan_chance\",\"chance\":\"NaN\",\"optionA\":{\"key\":\"a\",\"skillId\":\"Charm\",\"difficulty\":65},\"optionB\":{\"key\":\"b\",\"skillId\":\"Steward\",\"difficulty\":50}}]}");

        Assert.AreEqual(0, Provider().GetDuties().Incidents.Count);
    }
}
