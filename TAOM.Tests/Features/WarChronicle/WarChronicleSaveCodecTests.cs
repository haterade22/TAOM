using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.WarChronicle;

[TestClass]
public class WarChronicleSaveCodecTests
{
    private static WarChroniclePayload Sample()
    {
        var payload = new WarChroniclePayload();
        payload.Chronicle.Add(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 49.5, CloseDay = 69.5, Assaulted = true });
        payload.Chronicle.Add(new ChronicleEventRecord("fellowship", ChronicleEventState.Occurred) { ResolvedDay = 20.25 });
        payload.Baselines["empire_w"] = 7;
        payload.Baselines["empire_s"] = 5;
        payload.Effects.Add(new WarEffect("rally:empire_w", "empire_w", WarEffectKind.VolunteerRate, 0.1f, 1234.5));
        payload.Effects.Add(new WarEffect("console", "empire_s", WarEffectKind.PrisonerEscape, -0.25f, 99d));
        payload.RallyTiers["empire_w"] = 1;
        return payload;
    }

    private static WarChroniclePayload ParseJson(string json) => WarChronicleSaveCodec.Parse(json);

    [TestMethod]
    public void RoundTrip_EverySection_ComesBackEqual()
    {
        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(Sample()));

        Assert.AreEqual(0, back.Warnings.Count, string.Join(" | ", back.Warnings));
        Assert.AreEqual(2, back.Effects.Count);
        var first = back.Effects[0];
        Assert.AreEqual("rally:empire_w", first.SourceId);
        Assert.AreEqual("empire_w", first.KingdomId);
        Assert.AreEqual(WarEffectKind.VolunteerRate, first.Kind);
        Assert.AreEqual(0.1f, first.Magnitude);
        Assert.AreEqual(1234.5, first.EndTimeHours);
        Assert.AreEqual(WarEffectKind.PrisonerEscape, back.Effects[1].Kind);
        Assert.AreEqual(-0.25f, back.Effects[1].Magnitude);
        Assert.AreEqual(7, back.Baselines["empire_w"]);
        Assert.AreEqual(5, back.Baselines["empire_s"]);
        Assert.AreEqual(1, back.RallyTiers["empire_w"]);
        var hornburg = back.Chronicle[0];
        Assert.AreEqual("hornburg", hornburg.Id);
        Assert.AreEqual(ChronicleEventState.Active, hornburg.State);
        Assert.AreEqual(49.5, hornburg.OpenDay);
        Assert.AreEqual(69.5, hornburg.CloseDay);
        Assert.IsTrue(hornburg.Assaulted);
        Assert.IsNull(hornburg.ResolvedDay);
        Assert.AreEqual(ChronicleEventState.Occurred, back.Chronicle[1].State);
        Assert.AreEqual(20.25, back.Chronicle[1].ResolvedDay);
    }

    [TestMethod]
    public void Serialize_WritesTheNamedShape()
    {
        var root = JObject.Parse(WarChronicleSaveCodec.Serialize(Sample()));

        Assert.AreEqual(1, (int)root["v"]!);
        var row = (JObject)((JArray)root["effects"]!)[0];
        Assert.AreEqual("rally:empire_w", (string)row["s"]!);
        Assert.AreEqual("empire_w", (string)row["k"]!);
        Assert.AreEqual("VolunteerRate", (string)row["kind"]!, "the kind is saved by name");
        Assert.AreEqual(JTokenType.Float, row["m"]!.Type);
        Assert.AreEqual(1234.5, (double)row["end"]!);
        Assert.AreEqual(7, (int)root["baselines"]!["empire_w"]!);
        Assert.IsNull(root["baselinesMidCampaign"], "lateness is derived from the data, not saved");
        Assert.AreEqual(1, (int)root["rally"]!["tiers"]!["empire_w"]!);
        var chronicle = (JObject)((JArray)root["chronicle"]!)[0];
        Assert.AreEqual(1, (int)chronicle["s"]!, "the state is saved as its int");
        Assert.IsNull(chronicle["res"], "an absent day is left out");
        Assert.IsTrue((bool)chronicle["a"]!);
    }

    [TestMethod]
    public void RoundTrip_ADeclinedStoryOffer_KeepsItsFlag()
    {
        var payload = new WarChroniclePayload();
        payload.Chronicle.Add(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 49, CloseDay = 69, StoryDeclined = true });

        var json = WarChronicleSaveCodec.Serialize(payload);
        var back = WarChronicleSaveCodec.Parse(json);

        Assert.IsTrue((bool)JObject.Parse(json)["chronicle"]![0]!["sd"]!);
        Assert.IsTrue(back.Chronicle.Single().StoryDeclined);
    }

    [TestMethod]
    public void Parse_ARowWithoutTheStoryFlag_IsNotDeclined()
    {
        var back = WarChronicleSaveCodec.Parse("{\"v\":1,\"chronicle\":[{\"id\":\"hornburg\",\"s\":1,\"open\":49,\"close\":69}]}");

        Assert.IsFalse(back.Chronicle.Single().StoryDeclined);
        Assert.IsNull(JObject.Parse(WarChronicleSaveCodec.Serialize(back))["chronicle"]![0]!["sd"], "a row not declined writes no flag");
    }

    [TestMethod]
    [DataRow("\"yes\"")]
    [DataRow("1")]
    [DataRow("null")]
    [DataRow("false")]
    public void Parse_AStoryFlagThatIsNotTrue_IsNotDeclined(string flag)
    {
        var back = WarChronicleSaveCodec.Parse("{\"v\":1,\"chronicle\":[{\"id\":\"hornburg\",\"s\":1,\"open\":49,\"close\":69,\"sd\":" + flag + "}]}");

        Assert.IsFalse(back.Chronicle.Single().StoryDeclined);
        Assert.AreEqual("hornburg", back.Chronicle.Single().Id, "a bad flag keeps the row");
    }

    [TestMethod]
    public void Serialize_EmptyPayload_WritesEmptySectionsThatParseAsPresent()
    {
        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(new WarChroniclePayload()));

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(0, back.Baselines.Count);
        Assert.AreEqual(0, back.Chronicle.Count);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Parse_NullOrEmpty_IsAnEmptyPayloadWithoutAWarning(string? json)
    {
        var back = WarChronicleSaveCodec.Parse(json);

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(0, back.Baselines.Count);
        Assert.AreEqual(0, back.Chronicle.Count);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("{not json")]
    [DataRow("[1,2,3]")]
    [DataRow("42")]
    public void Parse_MalformedPayload_ResetsEverythingWithOneWarning(string json)
    {
        var back = ParseJson(json);

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(0, back.Baselines.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("{\"effects\":[]}")]
    [DataRow("{\"v\":2,\"effects\":[]}")]
    [DataRow("{\"v\":\"1\",\"effects\":[]}")]
    [DataRow("{\"v\":1.0,\"effects\":[]}")]
    [DataRow("{\"v\":null,\"effects\":[]}")]
    [DataRow("{\"v\":99999999999999999999,\"effects\":[]}")]
    [DataRow("{\"v\":-99999999999999999999,\"effects\":[]}")]
    public void Parse_MissingOrUnknownVersion_ResetsEverythingWithOneWarning(string json)
    {
        var back = ParseJson(json);

        Assert.AreEqual(0, back.Baselines.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_UnknownKindName_SkipsOnlyThatRow()
    {
        var back = ParseJson("{\"v\":1,\"effects\":["
            + "{\"s\":\"a\",\"k\":\"x\",\"kind\":\"GarrisonRecruit\",\"m\":0.1,\"end\":10},"
            + "{\"s\":\"b\",\"k\":\"x\",\"kind\":\"PrisonerEscape\",\"m\":0.2,\"end\":10}]}");

        Assert.AreEqual(1, back.Effects.Count);
        Assert.AreEqual("b", back.Effects[0].SourceId);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("99")]
    [DataRow("volunteerrate")]
    [DataRow("")]
    public void Parse_KindThatIsNotAnExactDefinedName_IsSkipped(string kind)
    {
        var back = ParseJson("{\"v\":1,\"effects\":[{\"s\":\"a\",\"k\":\"x\",\"kind\":\"" + kind + "\",\"m\":0.1,\"end\":10}]}");

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("\"m\":NaN,\"end\":10")]
    [DataRow("\"m\":Infinity,\"end\":10")]
    [DataRow("\"m\":-Infinity,\"end\":10")]
    [DataRow("\"m\":0.1,\"end\":NaN")]
    [DataRow("\"m\":0.1,\"end\":Infinity")]
    [DataRow("\"m\":\"NaN\",\"end\":10")]
    [DataRow("\"m\":0.1,\"end\":\"10\"")]
    [DataRow("\"m\":null,\"end\":10")]
    [DataRow("\"m\":0.1")]
    [DataRow("\"end\":10")]
    [DataRow("\"m\":99999999999999999999,\"end\":10")]
    [DataRow("\"m\":0.1,\"end\":99999999999999999999")]
    [DataRow("\"m\":1e300,\"end\":10")]
    public void Parse_NonFiniteOrMissingNumber_SkipsTheRowWithAWarning(string numbers)
    {
        var back = ParseJson("{\"v\":1,\"effects\":[{\"s\":\"a\",\"k\":\"x\",\"kind\":\"VolunteerRate\"," + numbers + "}]}");

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("{\"k\":\"x\",\"kind\":\"VolunteerRate\",\"m\":0.1,\"end\":10}")]
    [DataRow("{\"s\":\"\",\"k\":\"x\",\"kind\":\"VolunteerRate\",\"m\":0.1,\"end\":10}")]
    [DataRow("{\"s\":\"a\",\"kind\":\"VolunteerRate\",\"m\":0.1,\"end\":10}")]
    [DataRow("{\"s\":\"a\",\"k\":\" \",\"kind\":\"VolunteerRate\",\"m\":0.1,\"end\":10}")]
    [DataRow("{\"s\":7,\"k\":\"x\",\"kind\":\"VolunteerRate\",\"m\":0.1,\"end\":10}")]
    [DataRow("{\"s\":\"a\",\"k\":\"x\",\"m\":0.1,\"end\":10}")]
    [DataRow("\"just a string\"")]
    [DataRow("null")]
    public void Parse_RowWithAMissingOrEmptyField_IsSkipped(string row)
    {
        var back = ParseJson("{\"v\":1,\"effects\":[" + row + "]}");

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_IntegerNumbers_AreAccepted()
    {
        var back = ParseJson("{\"v\":1,\"effects\":[{\"s\":\"a\",\"k\":\"x\",\"kind\":\"VolunteerRate\",\"m\":1,\"end\":10}]}");

        Assert.AreEqual(1f, back.Effects[0].Magnitude);
        Assert.AreEqual(10d, back.Effects[0].EndTimeHours);
    }

    [TestMethod]
    public void Parse_EffectsSectionNotAnArray_ResetsAloneAndTheOtherSectionsSurvive()
    {
        var back = ParseJson("{\"v\":1,\"effects\":\"oops\",\"baselines\":{\"a\":4},\"rally\":{\"tiers\":{\"a\":1}},\"chronicle\":[{\"id\":\"a\",\"s\":3,\"res\":4}]}");

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(4, back.Baselines["a"]);
        Assert.AreEqual(1, back.RallyTiers["a"]);
        Assert.AreEqual(1, back.Chronicle.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_BaselinesSectionCorrupt_ResetsAloneToNoSection()
    {
        var back = ParseJson("{\"v\":1,\"effects\":[{\"s\":\"a\",\"k\":\"x\",\"kind\":\"VolunteerRate\",\"m\":0.1,\"end\":10}],\"baselines\":[1,2]}");

        Assert.AreEqual(1, back.Effects.Count);
        Assert.AreEqual(0, back.Baselines.Count, "a corrupt section reads as an absent one, so the baselines are retaken");
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_BaselinesSectionAbsent_IsEmptyWithoutAWarning()
    {
        var back = ParseJson("{\"v\":1}");

        Assert.AreEqual(0, back.Baselines.Count);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_BaselineValuesThatAreNotNonNegativeIntegers_SkipOnlyThatKingdom()
    {
        var back = ParseJson("{\"v\":1,\"baselines\":{\"a\":7,\"b\":\"x\",\"c\":-3,\"d\":2.5,\"e\":null,\"f\":0}}");

        CollectionAssert.AreEquivalent(new[] { "a", "f" }, back.Baselines.Keys.ToList());
        Assert.AreEqual(4, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_BaselineWiderThan64Bits_SkipsOnlyThatKingdom()
    {
        var back = ParseJson("{\"v\":1,\"baselines\":{\"a\":7,\"b\":99999999999999999999,\"c\":2147483648}}");

        CollectionAssert.AreEquivalent(new[] { "a" }, back.Baselines.Keys.ToList());
        Assert.AreEqual(2, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_AnOldBaselinesMidCampaignKey_IsIgnoredWithoutAWarning()
    {
        // Development saves written before the flag was dropped still carry it.
        var back = ParseJson("{\"v\":1,\"baselines\":{\"a\":1},\"baselinesMidCampaign\":true}");

        Assert.AreEqual(1, back.Baselines["a"]);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_RallyTierOutOfRange_SkipsThatKingdom()
    {
        var back = ParseJson("{\"v\":1,\"rally\":{\"tiers\":{\"a\":2,\"b\":3,\"c\":-1,\"d\":\"x\"}}}");

        CollectionAssert.AreEquivalent(new[] { "a" }, back.RallyTiers.Keys.ToList());
        Assert.AreEqual(3, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("{\"v\":1,\"rally\":[1]}")]
    [DataRow("{\"v\":1,\"rally\":{\"tiers\":5}}")]
    public void Parse_RallySectionCorrupt_ResetsAlone(string json)
    {
        var back = ParseJson(json);

        Assert.AreEqual(0, back.RallyTiers.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_ChronicleNotAnArray_ResetsAlone()
    {
        var back = ParseJson("{\"v\":1,\"baselines\":{\"a\":1},\"chronicle\":{\"a\":1}}");

        Assert.AreEqual(0, back.Chronicle.Count);
        Assert.AreEqual(1, back.Baselines["a"]);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("{\"s\":2,\"res\":1}", "id")]
    [DataRow("{\"id\":\"\",\"s\":2,\"res\":1}", "id")]
    [DataRow("{\"id\":\"a\",\"s\":99}", "state")]
    [DataRow("{\"id\":\"a\",\"s\":-1}", "state")]
    [DataRow("{\"id\":\"a\",\"s\":\"Held\"}", "state")]
    [DataRow("{\"id\":\"a\",\"s\":1,\"open\":1}", "window")]
    [DataRow("{\"id\":\"a\",\"s\":1,\"open\":\"NaN\",\"close\":2}", "window")]
    [DataRow("{\"id\":\"a\",\"s\":2,\"res\":\"x\"}", "res")]
    [DataRow("{\"id\":\"a\",\"s\":3}", "resolution day")]
    [DataRow("7", "object")]
    public void Parse_ABadChronicleRow_IsSkippedAloneWithAWarning(string badRow, string fragment)
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[" + badRow + ",{\"id\":\"good\",\"s\":3,\"res\":40}]}");

        Assert.AreEqual(1, back.Chronicle.Count);
        Assert.AreEqual("good", back.Chronicle[0].Id);
        Assert.IsTrue(back.Warnings.Any(w => w.Contains(fragment)), string.Join("\n", back.Warnings));
    }

    [TestMethod]
    public void Parse_ADuplicateChronicleId_KeepsTheFirst()
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[{\"id\":\"a\",\"s\":3,\"res\":1},{\"id\":\"a\",\"s\":4,\"res\":1}]}");

        Assert.AreEqual(ChronicleEventState.Held, back.Chronicle.Single().State);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_AChronicleRowForAnUnknownEvent_IsKeptSoANewerSaveSurvivesAnOlderBuild()
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[{\"id\":\"from_a_later_release\",\"s\":5,\"res\":12}]}");

        Assert.AreEqual(ChronicleEventState.Lapsed, back.Chronicle.Single().State);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_APendingRowWithNoDays_IsKept()
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[{\"id\":\"a\",\"s\":0}]}");

        Assert.AreEqual(ChronicleEventState.Pending, back.Chronicle.Single().State);
    }

    [TestMethod]
    public void RoundTrip_PhaseSeenDays_ComeBackEqual()
    {
        var payload = new WarChroniclePayload();
        payload.PhaseSeenDays[WarPhase.IsengardWar] = 101;
        payload.PhaseSeenDays[WarPhase.FullWar] = 120.5;

        var json = WarChronicleSaveCodec.Serialize(payload);
        var back = WarChronicleSaveCodec.Parse(json);

        Assert.AreEqual(0, back.Warnings.Count, string.Join(" | ", back.Warnings));
        Assert.AreEqual(101d, back.PhaseSeenDays[WarPhase.IsengardWar]);
        Assert.AreEqual(120.5, back.PhaseSeenDays[WarPhase.FullWar]);
        Assert.AreEqual(101d, (double)JObject.Parse(json)["phases"]!["IsengardWar"]!, "saved by phase name");
    }

    [TestMethod]
    public void Parse_PhasesSectionAbsent_IsEmptyWithoutAWarning()
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[{\"id\":\"a\",\"s\":0}]}");

        Assert.AreEqual(0, back.PhaseSeenDays.Count);
        Assert.AreEqual(1, back.Chronicle.Count);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_PhasesSectionNotAnObject_ResetsAloneAndTheOtherSectionsSurvive()
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[{\"id\":\"a\",\"s\":0}],\"phases\":[1]}");

        Assert.AreEqual(0, back.PhaseSeenDays.Count);
        Assert.AreEqual(1, back.Chronicle.Count);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("\"Peace\":3", "Peace")]
    [DataRow("\"WarEnded\":3", "WarEnded")]
    [DataRow("\"Phase9\":3", "Phase9")]
    [DataRow("\"1\":3", "'1'")]
    [DataRow("\"FullWar\":\"NaN\"", "FullWar")]
    [DataRow("\"FullWar\":null", "FullWar")]
    public void Parse_ABadPhaseRow_IsSkippedAloneWithAWarning(string badRow, string fragment)
    {
        var back = ParseJson("{\"v\":1,\"phases\":{" + badRow + ",\"IsengardWar\":40}}");

        Assert.AreEqual(40d, back.PhaseSeenDays[WarPhase.IsengardWar]);
        Assert.IsFalse(back.PhaseSeenDays.ContainsKey(WarPhase.FullWar));
        Assert.AreEqual(1, back.Warnings.Count, string.Join(" | ", back.Warnings));
        Assert.IsTrue(back.Warnings.Any(w => w.Contains(fragment)), string.Join(" | ", back.Warnings));
    }

    [TestMethod]
    public void RoundTrip_DailyRunDays_ComeBackEqual()
    {
        var payload = new WarChroniclePayload();
        payload.DailyRunDays[WarChronicleDailyGate.TickLane] = 12;
        payload.DailyRunDays[WarChronicleDailyGate.ChronicleLane] = 11;

        var json = WarChronicleSaveCodec.Serialize(payload);
        var back = WarChronicleSaveCodec.Parse(json);

        Assert.AreEqual(0, back.Warnings.Count, string.Join(" | ", back.Warnings));
        Assert.AreEqual(12, back.DailyRunDays[WarChronicleDailyGate.TickLane]);
        Assert.AreEqual(11, back.DailyRunDays[WarChronicleDailyGate.ChronicleLane]);
        Assert.AreEqual(12, (int)JObject.Parse(json)["daily"]!["tick"]!);
    }

    [TestMethod]
    public void Parse_DailySectionAbsent_IsEmptyWithoutAWarning()
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[{\"id\":\"a\",\"s\":0}]}");

        Assert.AreEqual(0, back.DailyRunDays.Count);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [TestMethod]
    public void Parse_DailySectionNotAnObject_ResetsAloneAndTheOtherSectionsSurvive()
    {
        var back = ParseJson("{\"v\":1,\"chronicle\":[{\"id\":\"a\",\"s\":0}],\"daily\":[12]}");

        Assert.AreEqual(0, back.DailyRunDays.Count);
        Assert.AreEqual(1, back.Chronicle.Count);
        Assert.AreEqual(1, back.Warnings.Count, string.Join(" | ", back.Warnings));
    }

    [DataTestMethod]
    [DataRow("\"tick\":-1")]
    [DataRow("\"tick\":1.5")]
    [DataRow("\"tick\":\"12\"")]
    [DataRow("\"tick\":99999999999999999999")]
    public void Parse_ADailyDayThatIsNotANonNegativeInteger_IsSkippedAlone(string badRow)
    {
        var back = ParseJson("{\"v\":1,\"daily\":{" + badRow + ",\"chronicle\":40}}");

        Assert.IsFalse(back.DailyRunDays.ContainsKey(WarChronicleDailyGate.TickLane));
        Assert.AreEqual(40, back.DailyRunDays[WarChronicleDailyGate.ChronicleLane]);
        Assert.AreEqual(1, back.Warnings.Count, string.Join(" | ", back.Warnings));
    }
}
