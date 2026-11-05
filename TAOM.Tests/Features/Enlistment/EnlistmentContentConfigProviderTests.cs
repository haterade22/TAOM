using System.Linq;
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Newtonsoft.Json.Linq;
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
    public void GetConfig_RetiredKeys_LoadWithoutWarning()
    {
        WriteConfig("{ \"progression\": { \"dailyLeadershipXp\": 11, \"trainingSessionXp\": 20 }, "
            + "\"scheduler\": { \"reputationMax\": 50 }, "
            + "\"meritBands\": [ { \"minScore\": 60, \"serviceXp\": 20, \"repDomain\": \"Field\", \"repAmount\": 1, \"gradeKey\": \"strong\" }, "
            + "{ \"minScore\": 0, \"serviceXp\": 4, \"repDomain\": \"None\", \"repAmount\": 0, \"gradeKey\": \"rough\" } ] }");

        var config = Provider().GetConfig();

        Assert.AreEqual(11, config.Progression.DailyLeadershipXp);
        Assert.AreEqual("strong", config.MeritBands[0].GradeKey);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s =>
            s.Contains("trainingSessionXp") || s.Contains("reputationMax") || s.Contains("repDomain") || s.Contains("repAmount")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetDuties_RetiredRewardKeys_LoadWithoutWarning()
    {
        WriteDuties("{\"incidents\":[{\"id\":\"pay_delay\",\"title\":\"{=taom_enlist_duty_pay_delay_title}x\",\"body\":\"{=taom_enlist_duty_pay_delay_body}x\",\"chance\":0.22,\"effect\":\"ReleaseDeferredPay\","
            + "\"gates\":{\"minRank\":\"Recruit\"},"
            + "\"optionA\":{\"key\":\"press_claim\",\"label\":\"{=taom_enlist_duty_pay_delay_opta}x\",\"skillId\":\"Charm\",\"difficulty\":65,"
            + "\"successReward\":{\"serviceXp\":20,\"trust\":-1,\"repDomain\":\"Command\",\"repAmount\":2}},"
            + "\"optionB\":{\"key\":\"wait_it_out\",\"label\":\"{=taom_enlist_duty_pay_delay_optb}x\",\"skillId\":\"Steward\",\"difficulty\":50,"
            + "\"successReward\":{\"serviceXp\":16,\"trust\":1}}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.Incidents.Count);
        Assert.AreEqual(20, duties.Incidents[0].OptionA.SuccessReward.ServiceXp);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
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
    public void GetConfig_LegacyMaxDeferredWagesKey_WarnsAboutTheRename()
    {
        // Migration hazard: the key was renamed AND changed unit (flat gold -> days of the current
        // rank wage). Newtonsoft binds the old name to nothing, so without this warning a player's
        // retuned install silently takes the new default and never learns why.
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWages\":60}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.WagePolicy.MaxDeferredWageDays, "the old key is ignored, not converted");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxDeferredWages") && s.Contains("renamed")));
    }

    [TestMethod]
    public void GetConfig_NewMaxDeferredWageDaysKey_NoRenameWarning()
    {
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWageDays\":30}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(30, config.WagePolicy.MaxDeferredWageDays);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("renamed")));
    }

    [TestMethod]
    public void GetConfig_NegativeMaxDeferredWageDays_Reverts()
    {
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWageDays\":-1}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.WagePolicy.MaxDeferredWageDays);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxDeferredWageDays")));
    }

    [TestMethod]
    public void GetConfig_MaxDeferredWageDaysAboveContractLength_Reverts()
    {
        // Unbounded day counts turn arrears into a discharge-time gold bomb and push the
        // days x wage multiply toward overflow. One contract (365 days) is the ceiling.
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWageDays\":4000}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.WagePolicy.MaxDeferredWageDays);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxDeferredWageDays")));
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
    public void GetConfig_MeritBandsNegativeRenown_Reverts()
    {
        // Renown joined the non-negative set when it became live config (2026-08-11). It is
        // directional: BattleRenownPolicy adds the band figure to the flat win/loss base, so a
        // negative band does not merely shrink the reward — it eats the base, and a distinguished
        // fight ends up paying LESS than a rough one. Clamped at the policy, reverted here.
        WriteConfig("{\"meritBands\":[" +
            "{\"minScore\":80,\"serviceXp\":30,\"renown\":-5},{\"minScore\":0,\"serviceXp\":4,\"renown\":0}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(4, config.MeritBands.Count, "default bands restored");
        Assert.IsTrue(config.MeritBands.All(b => b.Renown >= 0));
    }

    /// <summary>
    /// The regression for the defect BattleRenownPolicy's own doc comment described but the config
    /// never delivered: <c>MeritBand.Renown</c> existed, the policy added it, and NO default band
    /// and NO shipped config key ever set it. <c>bandRenown</c> was 0 on every path, so every
    /// battle paid the identical flat base while the comment claimed the band did the
    /// differentiating.
    ///
    /// BattleRenownPolicyTests could not catch it — every one of those cases passes
    /// <c>bandRenown</c> in as a literal, so the pure function was fully covered and completely
    /// disconnected from the numbers that actually reach it. This asserts the SUPPLY side.
    /// </summary>
    [TestMethod]
    public void DefaultMeritBands_ActuallyDifferentiateOnRenown()
    {
        var bands = EnlistmentContentConfigProvider.BuildDefaults().MeritBands;

        Assert.IsTrue(bands.Any(b => b.Renown > 0),
            "no default band awards renown — bandRenown is always 0 and every battle pays the same flat base");

        // Descending minScore must mean non-increasing renown, or a worse fight outearns a better
        // one. Strictly greater at the ends, so the ladder is not merely flat-but-nonzero.
        for (var i = 1; i < bands.Count; i++)
            Assert.IsTrue(bands[i].Renown <= bands[i - 1].Renown,
                $"band '{bands[i].GradeKey}' pays more renown than the better band above it");

        Assert.IsTrue(bands[0].Renown > bands[bands.Count - 1].Renown,
            "the best and worst bands pay the same renown — the grade buys nothing");
    }

    /// <summary>
    /// The defaults are only half the fix: an install reads the SHIPPED json, and a key absent
    /// there silently takes the compiled default rather than the tuner's intent. This pins that the
    /// renown chain is actually exposed in the file players edit — both the per-band figure and the
    /// flat win/loss base it is added to.
    /// </summary>
    [TestMethod]
    public void ShippedConfig_ExposesTheWholeRenownChain()
    {
        var provider = new EnlistmentContentConfigProvider(ShippedPaths(), _logger);
        var config = provider.GetConfig();

        Assert.IsTrue(config.MeritBands.Any(b => b.Renown > 0),
            "the shipped enlistment_config.json has no band awarding renown");
        Assert.IsTrue(config.Progression.BattleWinRenown > 0,
            "the shipped enlistment_config.json does not set battleWinRenown");

        var raw = File.ReadAllText(Path.Combine(
            ShippedModuleDataPath(), "enlistment", "enlistment_config.json"));
        StringAssert.Contains(raw, "\"renown\"",
            "the band renown key is missing from the shipped file, so a retune has nothing to edit");
        StringAssert.Contains(raw, "\"battleWinRenown\"",
            "the flat renown base is missing from the shipped file");

        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("meritBands")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    /// <summary>
    /// Standing has exactly two earners: a duty success, and a merit band that pays trust. The band
    /// half has to survive both surfaces or it does not exist for half the installs — an
    /// <c>enlistment_config.json</c> that omits the key silently takes the compiled default, and a
    /// compiled default that disagrees with the shipped file means two players on the same build
    /// progress at different rates with nothing in the log.
    ///
    /// This is the same class of gap as the renown chain above, on the field that gates promotion:
    /// Veteran requires <c>minTrust: 0</c>, so a soldier who fights steadily and takes no camp work
    /// is stuck at Soldier forever if no band he can reach pays trust. That shipped, and a live
    /// 73-day service reached "badly thought of" on 2903 XP because of it.
    /// </summary>
    [TestMethod]
    public void MeritBandTrust_MatchesBetweenTheShippedFileAndTheCompiledDefaults()
    {
        // Read the FILE, not the provider's output. Going through GetConfig() looks stronger and is
        // strictly weaker: four paths inside LoadConfig/ValidateConfig replace MeritBands wholesale
        // with DefaultMeritBands() — file missing, unparseable, a null-or-empty meritBands array
        // (which logs NOTHING at all), and IsValidBandLadder rejecting a reordered or partially
        // edited ladder. On any of them this test would compare the defaults against themselves and
        // report green, which is precisely the divergence it exists to catch.
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(
            ShippedModuleDataPath(), "enlistment", "enlistment_config.json")));
        var shipped = ((JArray)raw["meritBands"])
            .ToDictionary(b => (string)b["gradeKey"], b => (int?)b["trust"] ?? 0);
        var compiled = EnlistmentContentConfigProvider.BuildDefaults().MeritBands;

        // Keyed by gradeKey, not by index: a reordered file must report the band that actually
        // diverged rather than whichever one happened to land in that slot.
        foreach (var band in compiled)
        {
            Assert.IsTrue(shipped.ContainsKey(band.GradeKey),
                $"band '{band.GradeKey}' exists in DefaultMeritBands() but not in enlistment_config.json");
            Assert.AreEqual(band.Trust, shipped[band.GradeKey],
                $"band '{band.GradeKey}' pays {shipped[band.GradeKey]} trust in enlistment_config.json "
                + $"but {band.Trust} in DefaultMeritBands() — an install without the file would earn "
                + "standing at a different rate");
        }

        Assert.AreEqual(compiled.Count, shipped.Count, "band count diverged between file and defaults");

        // The earner has to exist at all; WHICH band carries it is MeritTrustFloorTests' call, and
        // it is not a free choice — the bands below `strong` are reachable without fighting.
        Assert.IsTrue(compiled.Any(b => b.Trust > 0),
            "no merit band pays trust, so fighting well cannot raise standing at all");

        for (var i = 1; i < compiled.Count; i++)
            Assert.IsTrue(compiled[i].Trust <= compiled[i - 1].Trust,
                $"band '{compiled[i].GradeKey}' pays more trust than the better band above it");

        // The round trip must also be clean, or a future edit could satisfy every assertion above
        // while the provider silently discarded the file at load.
        new EnlistmentContentConfigProvider(ShippedPaths(), _logger).GetConfig();
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    /// <summary>
    /// The duty half of the same gap. `ShippedPaths()` was reached only by `GetConfig()` callers, so
    /// no test loaded the real `enlistment_duties.json` through the provider's own validation —
    /// `FieldDutyReachabilityTests` parses the file directly and every other duty test stubs the
    /// provider. `LoadDuties` SKIPS a bad row with nothing but a log warning, so a typo in one of
    /// the 11 rows that gained a second support skill (or in any retuned difficulty) would drop that
    /// duty at runtime while every other test kept counting thirteen.
    /// </summary>
    [TestMethod]
    public void ShippedDuties_LoadThroughTheProviderWithoutBeingSkipped()
    {
        var duties = new EnlistmentContentConfigProvider(ShippedPaths(), _logger).GetDuties();

        Assert.AreEqual(13, duties.FieldDuties.Count, "a field duty row was skipped at load");
        Assert.AreEqual(11, duties.InteractiveDuties.Count, "an interactive duty row was skipped at load");
        Assert.AreEqual(3, duties.Incidents.Count, "an incident row was skipped at load");

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    private const string FieldRow = "{\"id\":\"road_patrol\",\"title\":\"{=taom_enlist_duty_road_patrol_title}a road patrol\","
        + "\"success\":\"{=taom_enlist_duty_road_patrol_success}The road is quiet.\",\"failure\":\"{=taom_enlist_duty_road_patrol_failure}You miss it.\","
        + "\"difficulty\":50,\"durationHours\":4,\"supportSkills\":[\"Scouting\"],\"gates\":{\"minRank\":\"Recruit\"}}";

    private static string PopupRow(string section, string title, string body, string labelA, string labelB) =>
        "{\"id\":\"night_patrol\"," + (title == null ? "" : "\"title\":\"" + title + "\",")
        + (body == null ? "" : "\"body\":\"" + body + "\",")
        + (section == "incidents" ? "\"chance\":0.2,\"effect\":\"\"," : "")
        + "\"optionA\":{\"key\":\"walk\"," + (labelA == null ? "" : "\"label\":\"" + labelA + "\",") + "\"skillId\":\"Scouting\",\"difficulty\":60},"
        + "\"optionB\":{\"key\":\"watch\"," + (labelB == null ? "" : "\"label\":\"" + labelB + "\",") + "\"skillId\":\"Athletics\",\"difficulty\":55}}";

    private const string Title = "{=taom_enlist_duty_night_patrol_title}Night Watch";
    private const string Body = "{=taom_enlist_duty_night_patrol_body}Walk the rounds.";
    private const string LabelA = "{=taom_enlist_duty_night_patrol_opta}Walk Rounds";
    private const string LabelB = "{=taom_enlist_duty_night_patrol_optb}Double Watch";

    [TestMethod]
    public void GetDuties_RowsWithKeyedText_Load()
    {
        WriteDuties("{\"fieldDuties\":[" + FieldRow + "],\"interactiveDuties\":[" + PopupRow("interactiveDuties", Title, Body, LabelA, LabelB) + "]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count);
        Assert.AreEqual(1, duties.InteractiveDuties.Count);
        Assert.AreEqual("{=taom_enlist_duty_road_patrol_title}a road patrol", duties.FieldDuties[0].Title);
        Assert.AreEqual(LabelB, duties.InteractiveDuties[0].OptionB.Label);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetDuties_FieldDutyWithoutTitle_SkippedWithWarning()
    {
        WriteDuties("{\"fieldDuties\":[" + FieldRow.Replace("\"title\":\"{=taom_enlist_duty_road_patrol_title}a road patrol\",", "") + "]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("road_patrol") && s.Contains("title text is missing")));
    }

    [TestMethod]
    public void GetDuties_FieldDutyWithBadResultText_SkippedWithEveryProblemNamed()
    {
        var bad = FieldRow
            .Replace("\"success\":\"{=taom_enlist_duty_road_patrol_success}The road is quiet.\",", "")
            .Replace("{=taom_enlist_duty_road_patrol_failure}You miss it.", "{=taom_enlist_duty_road_patrol_failure}  ")
            .Replace("{=taom_enlist_duty_road_patrol_title}a road patrol", "{=taom_enlist_duty_other_title}a road patrol");
        WriteDuties("{\"fieldDuties\":[" + bad + "]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("road_patrol")
            && s.Contains("title text must start with")
            && s.Contains("success text is missing")
            && s.Contains("failure text has no English")));
    }

    [TestMethod]
    public void GetDuties_InteractiveDutyWithSeveralBadTexts_ReportsAllOfThem()
    {
        WriteDuties("{\"interactiveDuties\":[" + PopupRow("interactiveDuties", Title, null, null, LabelB) + "]}");

        Assert.AreEqual(0, Provider().GetDuties().InteractiveDuties.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("body text is missing") && s.Contains("optionA.label text is missing")));
    }

    [DataTestMethod]
    [DataRow(null, Body, LabelA, LabelB, "title text is missing")]
    [DataRow(Title, null, LabelA, LabelB, "body text is missing")]
    [DataRow(Title, Body, null, LabelB, "optionA.label text is missing")]
    [DataRow(Title, Body, LabelA, "Double Watch", "optionB.label text must start with")]
    [DataRow(Title, Body, "{=taom_enlist_duty_other_opta}Walk Rounds", LabelB, "optionA.label text must start with")]
    [DataRow("{=taom_enlist_duty_night_patrol_title}", Body, LabelA, LabelB, "title text has no English")]
    public void GetDuties_InteractiveDutyWithBadText_SkippedWithWarning(string title, string body, string labelA, string labelB, string expected)
    {
        WriteDuties("{\"interactiveDuties\":[" + PopupRow("interactiveDuties", title, body, labelA, labelB) + "]}");

        Assert.AreEqual(0, Provider().GetDuties().InteractiveDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("night_patrol") && s.Contains(expected)));
    }

    [TestMethod]
    public void GetDuties_IncidentWithBadText_SkippedWithWarning()
    {
        WriteDuties("{\"incidents\":[" + PopupRow("incidents", Title, Body, LabelA, "Double Watch") + "]}");

        Assert.AreEqual(0, Provider().GetDuties().Incidents.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("night_patrol") && s.Contains("optionB.label text must start with")));
    }

    private IPathService ShippedPaths()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ShippedModuleDataPath());
        return paths;
    }

    private static string ShippedModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail("Could not locate Main/_Module/ModuleData from the test working directory.");
        return null;
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
    public void GetConfig_ValidRankTables_AppliedNotAppendedToDefaults()
    {
        // Regression (in-game 2026-08-07): the model initializes these lists, and Newtonsoft's
        // default ObjectCreationHandling.Auto APPENDS file entries onto that existing collection.
        // A valid 4-entry table deserialized to 8, failed the "exactly 4" check, and silently
        // reverted to the compiled defaults — so every retune was discarded with only a warning.
        // Values here deliberately differ from the defaults so a revert cannot pass this test.
        WriteConfig("{\"progression\":{\"dailyWageByRank\":[9,11,13,17],\"dailyServiceXpByRank\":[1,2,3,4]}}");

        var config = Provider().GetConfig();

        CollectionAssert.AreEqual(new[] { 9, 11, 13, 17 }, config.Progression.DailyWageByRank.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, config.Progression.DailyServiceXpByRank.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("dailyWageByRank")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("dailyServiceXpByRank")));
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
    public void GetDuties_MoreThanTwoSupportSkills_SkipsRowWithWarning()
    {
        // Only [0] and [1] are ever read, and EffectiveSkill returns the better of the two. A third
        // entry looks like a third chance to the author and is dead data at runtime — the exact
        // shape of silence this provider exists to refuse.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"too_many\",\"title\":\"{=taom_enlist_duty_too_many_title}x\",\"success\":\"{=taom_enlist_duty_too_many_success}x\",\"failure\":\"{=taom_enlist_duty_too_many_failure}x\",\"difficulty\":50,\"durationHours\":6,"
            + "\"supportSkills\":[\"Scouting\",\"Riding\",\"Athletics\"]}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("only the first two")));
    }

    [TestMethod]
    public void GetDuties_TwoSupportSkills_IsAccepted()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"pairing\",\"title\":\"{=taom_enlist_duty_pairing_title}x\",\"success\":\"{=taom_enlist_duty_pairing_success}x\",\"failure\":\"{=taom_enlist_duty_pairing_failure}x\",\"difficulty\":50,\"durationHours\":6,"
            + "\"supportSkills\":[\"Scouting\",\"Riding\"]}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count, "the two-skill pairing is the shipped shape");
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("supportSkills")));
    }

    [TestMethod]
    public void GetDuties_UnknownContext_SkipsRowWithWarning()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"bad_ctx\",\"title\":\"{=taom_enlist_duty_bad_ctx_title}x\",\"success\":\"{=taom_enlist_duty_bad_ctx_success}x\",\"failure\":\"{=taom_enlist_duty_bad_ctx_failure}x\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"],\"gates\":{\"requiredContexts\":[\"siegee\"]}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("siegee")));
    }

    [TestMethod]
    public void GetDuties_ZeroDurationHours_SkipsRow()
    {
        // Every OTHER field is deliberately valid, so this pins the durationHours rule specifically.
        // The version this replaced wrote the retired keys (mechanic / targetKind / deadlineDays:0)
        // and omitted supportSkills — so the row was skipped by the support-skill rule and the test
        // passed while testing nothing about duration. A zero-hour duty resolves the instant it is
        // assigned, which is exactly what this must keep out of the data.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"zero_hours\",\"title\":\"{=taom_enlist_duty_zero_hours_title}x\",\"success\":\"{=taom_enlist_duty_zero_hours_success}x\",\"failure\":\"{=taom_enlist_duty_zero_hours_failure}x\",\"difficulty\":55,\"durationHours\":0,"
            + "\"supportSkills\":[\"Scouting\"]}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_NoSupportSkills_SkipsRow()
    {
        // A row with no skills rolls against skill 0 forever — silently unwinnable at any rank.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"no_skills\",\"title\":\"{=taom_enlist_duty_no_skills_title}x\",\"success\":\"{=taom_enlist_duty_no_skills_success}x\",\"failure\":\"{=taom_enlist_duty_no_skills_failure}x\",\"difficulty\":55,\"durationHours\":6}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_ValidRow_IsKept()
    {
        // The positive control the three skip-tests need: proves they fail for their stated reason
        // and not because the fixture shape is wrong.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"ok\",\"title\":\"{=taom_enlist_duty_ok_title}x\",\"success\":\"{=taom_enlist_duty_ok_success}x\",\"failure\":\"{=taom_enlist_duty_ok_failure}x\",\"difficulty\":55,\"durationHours\":6,"
            + "\"supportSkills\":[\"Scouting\"]}]}");

        var duties = Provider().GetDuties().FieldDuties;
        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual(6, duties[0].DurationHours);
        Assert.AreEqual(55, duties[0].Difficulty);
    }

    [TestMethod]
    public void GetDuties_DuplicateId_SkipsLaterRow()
    {
        WriteDuties("{\"fieldDuties\":[" +
            "{\"id\":\"road_patrol\",\"title\":\"{=taom_enlist_duty_road_patrol_title}x\",\"success\":\"{=taom_enlist_duty_road_patrol_success}x\",\"failure\":\"{=taom_enlist_duty_road_patrol_failure}x\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"]}," +
            "{\"id\":\"road_patrol\",\"title\":\"{=taom_enlist_duty_road_patrol_title}x\",\"success\":\"{=taom_enlist_duty_road_patrol_success}x\",\"failure\":\"{=taom_enlist_duty_road_patrol_failure}x\",\"difficulty\":48,\"durationHours\":4,\"supportSkills\":[\"Steward\"]}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count);
        Assert.AreEqual(55, duties.FieldDuties[0].Difficulty, "the FIRST row wins a duplicate id");
    }

    [TestMethod]
    public void GetDuties_NegativeReward_SkipsRow()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"neg\",\"title\":\"{=taom_enlist_duty_neg_title}x\",\"success\":\"{=taom_enlist_duty_neg_success}x\",\"failure\":\"{=taom_enlist_duty_neg_failure}x\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"],\"reportReward\":{\"gold\":-5}}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_ValidRows_Load()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"road_patrol\",\"title\":\"{=taom_enlist_duty_road_patrol_title}x\",\"success\":\"{=taom_enlist_duty_road_patrol_success}x\",\"failure\":\"{=taom_enlist_duty_road_patrol_failure}x\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"],\"reportReward\":{\"serviceXp\":48,\"gold\":55}}]," +
            "\"incidents\":[{\"id\":\"pay_delay\",\"title\":\"{=taom_enlist_duty_pay_delay_title}x\",\"body\":\"{=taom_enlist_duty_pay_delay_body}x\",\"chance\":0.22,\"effect\":\"ReleaseDeferredPay\",\"optionA\":{\"key\":\"press\",\"label\":\"{=taom_enlist_duty_pay_delay_opta}x\",\"skillId\":\"Charm\",\"difficulty\":65},\"optionB\":{\"key\":\"wait\",\"label\":\"{=taom_enlist_duty_pay_delay_optb}x\",\"skillId\":\"Steward\",\"difficulty\":50}}]}");

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
