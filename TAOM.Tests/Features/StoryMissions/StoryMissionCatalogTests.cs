using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The validating loader of <c>story_missions.json</c> (config-providers.md): a bad value skips its row, or reverts to the default for a
/// number the Long Defeat readers own, and costs one warning. One test per invalid value.
/// </summary>
[TestClass]
public class StoryMissionCatalogTests
{
    private static readonly (string Key, string Json)[] Defaults =
    {
        ("id", "\"m1\""),
        ("name", "\"{=!}Mission one\""),
        ("scene", "\"scene_a\""),
        ("side", "\"Defend\""),
        ("defenderCulture", "\"vlandia\""),
        ("attackerCulture", "\"isengard\""),
        ("garrison", "[{\"troop\":\"g1\",\"class\":\"Infantry\",\"count\":10}]"),
        ("roster", "{\"waves\":[" + Wave(110) + "]}"),
    };

    private static string Wave(int count) =>
        "{\"count\":" + count + ",\"mix\":[{\"troop\":\"a1\",\"class\":\"Infantry\",\"weight\":1}]}";

    // A null value removes the key; a key the defaults lack is added.
    private static string Row(params (string Key, string? Json)[] set)
    {
        var d = new List<(string Key, string Json)>(Defaults);
        foreach (var (key, json) in set)
        {
            d.RemoveAll(x => x.Key == key);
            if (json != null)
                d.Add((key, json));
        }

        return "{" + string.Join(",", d.Select(x => "\"" + x.Key + "\":" + x.Json)) + "}";
    }

    private static string File(params string[] rows) => "{\"missions\":[" + string.Join(",", rows) + "]}";

    private static (StoryMissionCatalog Catalog, IModLogger Logger) Load(string? text)
    {
        var logger = Substitute.For<IModLogger>();
        return (StoryMissionCatalog.FromText(text, logger), logger);
    }

    private static void AssertWarned(IModLogger logger, string fragment) =>
        logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(fragment)));

    private static void AssertSkipped(string text, string warning)
    {
        var (catalog, logger) = Load(text);

        Assert.AreEqual(0, catalog.Missions.Count);
        AssertWarned(logger, warning);
    }

    [TestMethod]
    public void Load_AValidRow_ReadsEveryFieldWithNoWarning()
    {
        var (catalog, logger) = Load(File(Row()));

        var m = catalog.Missions.Single();
        Assert.AreEqual("m1", m.Id);
        Assert.AreEqual("{=!}Mission one", m.Name);
        Assert.AreEqual("scene_a", m.Scene);
        Assert.AreEqual(StorySide.Defending, m.Side);
        Assert.AreEqual("vlandia", m.DefenderCulture);
        Assert.AreEqual("isengard", m.AttackerCulture);
        Assert.AreEqual(1, m.Garrison.Count);
        Assert.AreEqual(1, m.Waves.Authored.Count);
        Assert.AreEqual(RunRules.Default.PreparationSeconds, m.Rules.PreparationSeconds);
        Assert.IsNull(m.Keep);
        Assert.IsNull(m.Muster);
        Assert.IsNull(m.Music, "a mission with no music keeps the campaign's music");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Load_TheAttackSide_IsAccepted()
    {
        var (catalog, _) = Load(File(Row(("side", "\"Attack\""))));

        Assert.AreEqual(StorySide.Attacking, catalog.Missions.Single().Side);
    }

    [TestMethod]
    public void Load_ANameWithNoText_FallsBackToTheId()
    {
        var (catalog, _) = Load(File(Row(("name", null))));

        Assert.AreEqual("m1", catalog.Missions.Single().Name);
    }

    [TestMethod]
    public void Load_AMissingFile_LoadsNothingWithOneWarning()
    {
        var (catalog, logger) = Load(null);

        Assert.AreEqual(0, catalog.Missions.Count);
        AssertWarned(logger, "was not found");
    }

    [TestMethod]
    public void Load_MalformedJson_LoadsNothing()
    {
        AssertSkipped("{ \"missions\": [ ", "could not be read");
    }

    [TestMethod]
    public void Load_ARootThatIsNotAnObject_LoadsNothing()
    {
        AssertSkipped("[]", "not a JSON object");
    }

    [TestMethod]
    public void Load_MissionsThatIsNotAnArray_LoadsNothing()
    {
        AssertSkipped("{\"missions\": 7}", "'missions' is missing or not an array");
    }

    [TestMethod]
    public void Load_ARowThatIsNotAnObject_IsSkipped_AndTheNextRowLoads()
    {
        var (catalog, logger) = Load("{\"missions\":[42," + Row(("id", "\"m2\"")) + "]}");

        Assert.AreEqual("m2", catalog.Missions.Single().Id);
        AssertWarned(logger, "mission #0 skipped");
    }

    [TestMethod]
    public void Load_AMissingId_SkipsTheRow()
    {
        AssertSkipped(File(Row(("id", null))), "no 'id'");
    }

    [TestMethod]
    public void Load_ABlankId_SkipsTheRow()
    {
        AssertSkipped(File(Row(("id", "\"  \""))), "no 'id'");
    }

    [TestMethod]
    public void Load_ADuplicateId_KeepsTheFirst()
    {
        var (catalog, logger) = Load(File(Row(("scene", "\"first\"")), Row(("scene", "\"second\""))));

        Assert.AreEqual("first", catalog.Missions.Single().Scene);
        AssertWarned(logger, "duplicate id");
    }

    [TestMethod]
    public void Load_AMissingScene_SkipsTheRow()
    {
        AssertSkipped(File(Row(("scene", null))), "'scene'");
    }

    [TestMethod]
    public void Load_ASceneWithAPathOrSpace_SkipsTheRow()
    {
        AssertSkipped(File(Row(("scene", "\"../scene a\""))), "scene must be");
    }

    [TestMethod]
    public void Load_ASceneNameThatIsTooLong_SkipsTheRow()
    {
        AssertSkipped(File(Row(("scene", "\"" + new string('s', 129) + "\""))), "scene must be");
    }

    [TestMethod]
    public void Load_AMissingSide_SkipsTheRow()
    {
        AssertSkipped(File(Row(("side", null))), "'side'");
    }

    [TestMethod]
    public void Load_AnUnknownSide_SkipsTheRow_NotADefault()
    {
        AssertSkipped(File(Row(("side", "\"Siege\""))), "'side'");
    }

    [TestMethod]
    public void Load_ASideInTheWrongCase_SkipsTheRow()
    {
        AssertSkipped(File(Row(("side", "\"defend\""))), "'side'");
    }

    [TestMethod]
    public void Load_ASideThatIsNotText_SkipsTheRow()
    {
        AssertSkipped(File(Row(("side", "1"))), "'side'");
    }

    [TestMethod]
    public void Load_AMissingDefenderCulture_SkipsTheRow()
    {
        AssertSkipped(File(Row(("defenderCulture", null))), "'defenderCulture'");
    }

    [TestMethod]
    public void Load_AMissingAttackerCulture_SkipsTheRow()
    {
        AssertSkipped(File(Row(("attackerCulture", null))), "'attackerCulture'");
    }

    [TestMethod]
    public void Load_NoGarrison_SkipsTheRow()
    {
        AssertSkipped(File(Row(("garrison", null))), "no valid garrison row");
    }

    [TestMethod]
    public void Load_AGarrisonRowWithAnUnknownClass_IsSkipped_AndTheOthersStay()
    {
        var garrison = "[{\"troop\":\"g1\",\"class\":\"Cavalry\",\"count\":5},{\"troop\":\"g2\",\"class\":\"Ranged\",\"count\":7}]";

        var (catalog, logger) = Load(File(Row(("garrison", garrison))));

        Assert.AreEqual("g2", catalog.Missions.Single().Garrison.Single().TroopId);
        AssertWarned(logger, "garrison[0].class");
    }

    [TestMethod]
    public void Load_EveryGarrisonRowInvalid_SkipsTheRow()
    {
        AssertSkipped(File(Row(("garrison", "[{\"troop\":\"g1\",\"class\":\"Infantry\",\"count\":0}]"))), "no valid garrison row");
    }

    [TestMethod]
    public void Load_NoRoster_SkipsTheRow()
    {
        AssertSkipped(File(Row(("roster", null))), "'roster'");
    }

    [TestMethod]
    public void Load_ARosterThatIsNotAnObject_SkipsTheRow()
    {
        AssertSkipped(File(Row(("roster", "[]"))), "'roster'");
    }

    [TestMethod]
    public void Load_ZeroWaves_SkipsTheRow()
    {
        AssertSkipped(File(Row(("roster", "{\"waves\":[]}"))), "no valid wave");
    }

    [TestMethod]
    public void Load_ARosterWithNoWavesKey_SkipsTheRow()
    {
        AssertSkipped(File(Row(("roster", "{}"))), ".waves' is missing");
    }

    [TestMethod]
    public void Load_AWaveBelowTheLiveFloor_IsDropped_AndTheRowWithNoWaveLeftIsSkipped()
    {
        AssertSkipped(File(Row(("roster", "{\"waves\":[" + Wave(100) + "]}"))), "no valid wave");
    }

    [TestMethod]
    public void Load_OneBadWaveAmongGoodOnes_IsDropped_AndTheRowKeepsTheRest()
    {
        var (catalog, logger) = Load(File(Row(("roster", "{\"waves\":[" + Wave(100) + "," + Wave(120) + "]}"))));

        Assert.AreEqual(120, catalog.Missions.Single().Waves.Authored.Single().Count);
        AssertWarned(logger, "waves[0].count");
    }

    [TestMethod]
    public void Load_ANaNRule_RevertsToTheDefault_AndTheMissionStays()
    {
        var (catalog, logger) = Load(File(Row(("rules", "{\"preparationSeconds\": NaN}"))));

        Assert.AreEqual(RunRules.Default.PreparationSeconds, catalog.Missions.Single().Rules.PreparationSeconds);
        AssertWarned(logger, "rules.preparationSeconds");
    }

    [TestMethod]
    public void Load_AnInfiniteRule_RevertsToTheDefault()
    {
        var (catalog, logger) = Load(File(Row(("rules", "{\"intermissionSeconds\": Infinity}"))));

        Assert.AreEqual(RunRules.Default.IntermissionSeconds, catalog.Missions.Single().Rules.IntermissionSeconds);
        AssertWarned(logger, "rules.intermissionSeconds");
    }

    [TestMethod]
    public void Load_ARuleOutOfRange_RevertsToTheDefault()
    {
        var (catalog, logger) = Load(File(Row(("rules", "{\"preparationSeconds\": 0}"))));

        Assert.AreEqual(RunRules.Default.PreparationSeconds, catalog.Missions.Single().Rules.PreparationSeconds);
        AssertWarned(logger, "rules.preparationSeconds");
    }

    [TestMethod]
    public void Load_AValidRule_IsKept()
    {
        var (catalog, logger) = Load(File(Row(("rules", "{\"preparationSeconds\": 20}"))));

        Assert.AreEqual(20f, catalog.Missions.Single().Rules.PreparationSeconds);
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Load_AKeepMinAttackersBelowTheStragglerFloor_IsRaised()
    {
        // 10% of the smallest wave (110) plus one is 12: fewer attackers than that at the keep could be stragglers.
        var (catalog, logger) = Load(File(Row(("rules", "{\"keepMinAttackers\": 1}"))));

        Assert.AreEqual(12, catalog.Missions.Single().Rules.KeepMinAttackers);
        AssertWarned(logger, "keepMinAttackers");
    }

    [TestMethod]
    public void Load_ARecordedKeep_IsRead()
    {
        var (catalog, _) = Load(File(Row(("keep", "{\"x\":1,\"y\":2,\"z\":3,\"radius\":10}"))));

        Assert.AreEqual(new KeepPoint(1f, 2f, 3f, 10f), catalog.Missions.Single().Keep);
    }

    [TestMethod]
    public void Load_ANaNKeep_IsDropped_AndTheMissionStays()
    {
        var (catalog, logger) = Load(File(Row(("keep", "{\"x\":NaN,\"y\":2,\"z\":3}"))));

        Assert.IsNull(catalog.Missions.Single().Keep);
        AssertWarned(logger, "keep.x");
    }

    [TestMethod]
    public void Load_ARecordedMuster_IsRead()
    {
        var (catalog, _) = Load(File(Row(("muster", "{\"foot\":{\"x\":1,\"y\":2,\"z\":3,\"fx\":0.6,\"fy\":0.8}}"))));

        Assert.IsNotNull(catalog.Missions.Single().Muster?.Foot);
        Assert.IsNull(catalog.Missions.Single().Muster?.Archers);
    }

    [TestMethod]
    public void Load_ANaNMusterPoint_DropsTheWholeBlock_AndTheMissionStays()
    {
        var (catalog, logger) = Load(File(Row(("muster", "{\"foot\":{\"x\":NaN,\"y\":2,\"z\":3,\"fx\":0.6,\"fy\":0.8}}"))));

        Assert.IsNull(catalog.Missions.Single().Muster);
        AssertWarned(logger, "muster.foot.x");
    }

    [TestMethod]
    public void Load_ABadRowDoesNotStopTheRowsAfterIt()
    {
        var (catalog, _) = Load(File(Row(("side", "\"Siege\"")), Row(("id", "\"m2\""))));

        Assert.AreEqual("m2", catalog.Missions.Single().Id);
    }

    [TestMethod]
    public void Load_AnyRevertedValue_EndsWithOneSummaryWarning()
    {
        var (catalog, logger) = Load(File(Row(("rules", "{\"preparationSeconds\": NaN}"))));

        Assert.AreEqual(1, catalog.Missions.Count);
        AssertWarned(logger, "reverted or were skipped");
    }

    [TestMethod]
    public void Load_AMusicPath_IsRead()
    {
        var (catalog, logger) = Load(File(Row(("music", "\"StoryMusic/isengard_vs_rohan.ogg\""))));

        Assert.AreEqual("StoryMusic/isengard_vs_rohan.ogg", catalog.Missions.Single().Music);
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    // The path goes to the engine as a file under ModuleSounds, so only a plain relative .ogg path passes: a float WAV, the source format, may not decode.
    [DataTestMethod]
    [DataRow("\"\"", DisplayName = "blank")]
    [DataRow("7", DisplayName = "not text")]
    [DataRow("\"StoryMusic/../x.ogg\"", DisplayName = "parent folder")]
    [DataRow("\"StoryMusic\\\\x.ogg\"", DisplayName = "backslash")]
    [DataRow("\"/StoryMusic/x.ogg\"", DisplayName = "leading slash")]
    [DataRow("\"C:/x.ogg\"", DisplayName = "drive")]
    [DataRow("\"Story Music/x.ogg\"", DisplayName = "space")]
    [DataRow("\"StoryMusic/x.wav\"", DisplayName = "wav")]
    [DataRow("\"StoryMusic/x.OGG\"", DisplayName = "upper-case extension")]
    [DataRow("\"StoryMusic/.ogg\"", DisplayName = "no file name")]
    public void Load_ABadMusicPath_IsDropped_WithAWarning_AndTheMissionStays(string json)
    {
        var (catalog, logger) = Load(File(Row(("music", json))));

        Assert.IsNull(catalog.Missions.Single().Music);
        AssertWarned(logger, "'music'");
    }

    [TestMethod]
    public void Load_AMusicPathOneCharacterTooLong_IsDropped()
    {
        var (catalog, logger) = Load(File(Row(("music", "\"" + new string('a', StoryMissionCatalog.MaxMusicLength - 3) + ".ogg\""))));

        Assert.IsNull(catalog.Missions.Single().Music);
        AssertWarned(logger, "'music'");
    }

    [TestMethod]
    public void Load_AMusicPathOfExactlyTheLimit_IsRead()
    {
        var path = new string('a', StoryMissionCatalog.MaxMusicLength - 4) + ".ogg";

        var (catalog, logger) = Load(File(Row(("music", "\"" + path + "\""))));

        Assert.AreEqual(path, catalog.Missions.Single().Music);
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Missions_ReadOnce_ReturnTheSameListEachTime()
    {
        var (catalog, _) = Load(File(Row()));

        Assert.AreSame(catalog.Missions, catalog.Missions);
    }

    [TestMethod]
    public void TryGet_FindsAMissionByIdIgnoringCase_AndRefusesTheRest()
    {
        var (catalog, _) = Load(File(Row()));

        Assert.IsTrue(catalog.TryGet("M1", out var found));
        Assert.AreEqual("m1", found.Id);
        Assert.IsFalse(catalog.TryGet("nope", out _));
        Assert.IsFalse(catalog.TryGet("", out _));
        Assert.IsFalse(catalog.TryGet(null!, out _));
    }
}
