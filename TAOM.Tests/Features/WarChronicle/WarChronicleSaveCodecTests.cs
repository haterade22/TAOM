using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.WarChronicle;

[TestClass]
public class WarChronicleSaveCodecTests
{
    private static WarChroniclePayload Sample()
    {
        var payload = new WarChroniclePayload
        {
            ChronicleJson = "[{\"id\":\"hornburg\",\"state\":2}]",
        };
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
        Assert.AreEqual("[{\"id\":\"hornburg\",\"state\":2}]", back.ChronicleJson);
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
        Assert.AreEqual(JTokenType.Array, root["chronicle"]!.Type);
    }

    [TestMethod]
    public void Serialize_EmptyPayload_WritesEmptySectionsThatParseAsPresent()
    {
        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(new WarChroniclePayload()));

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(0, back.Baselines.Count);
        Assert.AreEqual("[]", back.ChronicleJson);
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
        Assert.AreEqual("[]", back.ChronicleJson);
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
        var back = ParseJson("{\"v\":1,\"effects\":\"oops\",\"baselines\":{\"a\":4},\"rally\":{\"tiers\":{\"a\":1}},\"chronicle\":[1]}");

        Assert.AreEqual(0, back.Effects.Count);
        Assert.AreEqual(4, back.Baselines["a"]);
        Assert.AreEqual(1, back.RallyTiers["a"]);
        Assert.AreEqual("[1]", back.ChronicleJson);
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
    public void Parse_ChronicleNotAnArray_ResetsAloneToAnEmptyArray()
    {
        var back = ParseJson("{\"v\":1,\"baselines\":{\"a\":1},\"chronicle\":{\"a\":1}}");

        Assert.AreEqual("[]", back.ChronicleJson);
        Assert.AreEqual(1, back.Baselines["a"]);
        Assert.AreEqual(1, back.Warnings.Count);
    }

    [TestMethod]
    public void Chronicle_AnOpaqueArray_RoundTripsUnchanged()
    {
        // Nested objects, a date-looking string and non-ASCII text: a save that does not understand
        // the section must not reformat any of it.
        const string chronicle = "[{\"id\":\"hornburg\",\"state\":2,\"window\":[12.5,40],\"when\":\"2026-10-08T00:00:00Z\","
            + "\"name\":\"Helm’s Deep\",\"ratio\":1.0,\"flags\":{\"a\":true,\"b\":null}},[],\"tail\"]";
        var payload = new WarChroniclePayload { ChronicleJson = chronicle };

        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(payload));

        Assert.AreEqual(chronicle, back.ChronicleJson);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [TestMethod]
    public void Chronicle_AnExoticNumberSpelling_KeepsItsValue()
    {
        // Newtonsoft does not keep a number's source text, so 1e5 may come back as 100000.0: the value
        // is what the later milestone reads.
        var payload = new WarChroniclePayload { ChronicleJson = "[{\"big\":1e5,\"small\":-2.5e-3}]" };

        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(payload));

        Assert.IsTrue(JToken.DeepEquals(JToken.Parse(payload.ChronicleJson), JToken.Parse(back.ChronicleJson)));
    }

    [TestMethod]
    public void Serialize_UnparseableChronicleText_WritesAnEmptyArray()
    {
        var payload = new WarChroniclePayload { ChronicleJson = "{not an array" };

        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(payload));

        Assert.AreEqual("[]", back.ChronicleJson);
    }

    [DataTestMethod]
    [DataRow("{\"a\":1}")]
    [DataRow("42")]
    [DataRow("\"text\"")]
    public void Serialize_ParseableNonArrayChronicleText_WritesAnEmptyArray(string chronicle)
    {
        var payload = new WarChroniclePayload { ChronicleJson = chronicle };

        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(payload));

        Assert.AreEqual("[]", back.ChronicleJson);
        Assert.AreEqual(0, back.Warnings.Count);
    }

    [TestMethod]
    public void Serialize_NullChronicleText_WritesAnEmptyArray()
    {
        var payload = new WarChroniclePayload { ChronicleJson = null! };

        var back = WarChronicleSaveCodec.Parse(WarChronicleSaveCodec.Serialize(payload));

        Assert.AreEqual("[]", back.ChronicleJson);
    }
}
