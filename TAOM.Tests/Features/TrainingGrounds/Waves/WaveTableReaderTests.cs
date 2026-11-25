using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.TrainingGrounds.Waves.Config;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>training_waves.json is user-editable: one test per validation rule (config-providers.md).</summary>
[TestClass]
public class WaveTableReaderTests
{
    private const string GoodRow = "{\"count\":3,\"atOnce\":1,\"tiers\":{\"3\":1,\"4\":0,\"5\":0},\"weaponStage\":1,\"cap\":500,\"oddsTenths\":15}";

    private readonly List<string> _warnings = new();

    private WaveTable Read(string? json) => WaveTableReader.Read(json, _warnings.Add);

    private static string RowWith(string field, string valueJson)
    {
        var row = JObject.Parse(GoodRow);
        row[field] = JToken.Parse(valueJson);
        return row.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string RowWithout(string field)
    {
        var row = JObject.Parse(GoodRow);
        row.Remove(field);
        return row.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string File(string? rules, params string[] rows) =>
        "{" + (rules == null ? "" : "\"rules\":" + rules + ",") + "\"waves\":[" + string.Join(",", rows) + "]}";

    private static string RowOf(int count, int atOnce, int cap, int odds = 15) =>
        $"{{\"count\":{count},\"atOnce\":{atOnce},\"tiers\":{{\"3\":1}},\"weaponStage\":1,\"cap\":{cap},\"oddsTenths\":{odds}}}";

    private static void AssertSameTable(WaveTable expected, WaveTable actual)
    {
        Assert.AreEqual(expected.Rules, actual.Rules);
        Assert.AreEqual(expected.Waves.Count, actual.Waves.Count);
        for (var i = 0; i < expected.Waves.Count; i++)
        {
            var e = expected.Waves[i];
            var a = actual.Waves[i];
            Assert.AreEqual(e.Count, a.Count, "count of wave " + (i + 1));
            Assert.AreEqual(e.AtOnce, a.AtOnce, "atOnce of wave " + (i + 1));
            Assert.AreEqual(e.WeaponStage, a.WeaponStage, "stage of wave " + (i + 1));
            Assert.AreEqual(e.Cap, a.Cap, "cap of wave " + (i + 1));
            Assert.AreEqual(e.OddsTenths, a.OddsTenths, "odds of wave " + (i + 1));
            foreach (var tier in new[] { 3, 4, 5 })
                Assert.AreEqual(Weight(e, tier), Weight(a, tier), $"tier {tier} weight of wave {i + 1}");
        }
    }

    private static int Weight(WaveRow row, int tier) => row.TierWeights.TryGetValue(tier, out var w) ? w : 0;

    // ---- the default table

    [TestMethod]
    public void Default_IsTheEightWaveTable()
    {
        var waves = WaveTable.Default.Waves;

        Assert.AreEqual(8, waves.Count);
        CollectionAssert.AreEqual(new[] { 3, 5, 6, 8, 10, 12, 14, 16 }, waves.Select(w => w.Count).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 2, 3, 3, 4, 4, 5 }, waves.Select(w => w.AtOnce).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 2, 3, 3, 3 }, waves.Select(w => w.WeaponStage).ToArray());
        CollectionAssert.AreEqual(new[] { 500, 1000, 2000, 4000, 7500, 12000, 15000, 15000 }, waves.Select(w => w.Cap).ToArray());
        CollectionAssert.AreEqual(new[] { 15, 16, 17, 18, 19, 20, 20, 20 }, waves.Select(w => w.OddsTenths).ToArray());
        Assert.AreEqual(new WaveRules(10, 15_000, true, 5, 3), WaveTable.Default.Rules);
        CollectionAssert.AreEqual(new[] { 0, 2, 1 }, new[] { 3, 4, 5 }.Select(t => Weight(waves[4], t)).ToArray());
    }

    // ---- a clean file

    [TestMethod]
    public void Read_ValidFile_ReadsEveryValue_WithNoWarning()
    {
        var table = Read(File("{\"minStake\":20,\"hardCeiling\":5000,\"oneRunPerTownPerDay\":false,\"returnSeconds\":7,\"refillSeconds\":4}",
            "{\"count\":4,\"atOnce\":2,\"tiers\":{\"3\":2,\"4\":1},\"weaponStage\":2,\"cap\":300,\"oddsTenths\":12}"));

        Assert.AreEqual(0, _warnings.Count, string.Join(" | ", _warnings));
        Assert.AreEqual(new WaveRules(20, 5000, false, 7, 4), table.Rules);
        var row = table.Waves.Single();
        Assert.AreEqual(4, row.Count);
        Assert.AreEqual(2, row.AtOnce);
        Assert.AreEqual(2, row.WeaponStage);
        Assert.AreEqual(300, row.Cap);
        Assert.AreEqual(12, row.OddsTenths);
        Assert.AreEqual(2, Weight(row, 3));
        Assert.AreEqual(1, Weight(row, 4));
        Assert.AreEqual(0, Weight(row, 5));
    }

    [TestMethod]
    public void Read_NullText_GivesTheDefaultTable_WithoutAWarning()
    {
        AssertSameTable(WaveTable.Default, Read(null));
        AssertSameTable(WaveTable.Default, Read("  "));
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void Read_NotJson_GivesTheDefaultTable_AndWarns()
    {
        AssertSameTable(WaveTable.Default, Read("{ this is not json"));
        Assert.IsTrue(_warnings.Count >= 1);
    }

    [TestMethod]
    public void Read_RootThatIsNotAnObject_GivesTheDefaultTable_AndWarns()
    {
        AssertSameTable(WaveTable.Default, Read("[1,2,3]"));
        Assert.IsTrue(_warnings.Count >= 1);
    }

    [TestMethod]
    public void Read_EmptyObject_GivesTheDefaultTable_AndWarnsThatWavesIsMissing()
    {
        AssertSameTable(WaveTable.Default, Read("{}"));
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("'waves'")), string.Join(" | ", _warnings));
    }

    [TestMethod]
    public void Read_WavesKeyAbsent_WarnsOnce_AndSummarises()
    {
        var table = Read("{\"rules\":{\"minStake\":50}}");

        Assert.AreEqual(WaveTable.Default.Waves.Count, table.Waves.Count);
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("no 'waves'")), string.Join(" | ", _warnings));
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("reverted, skipped or adjusted")), "a fallback to the default waves is a reversion");
    }

    [TestMethod]
    public void Read_MisspelledWavesKey_WarnsAboutTheUnknownKey_AndThatWavesIsMissing()
    {
        var table = Read("{\"wavs\":[" + GoodRow + "]}");

        Assert.AreEqual(WaveTable.Default.Waves.Count, table.Waves.Count);
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("'wavs'")), string.Join(" | ", _warnings));
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("no 'waves'")), string.Join(" | ", _warnings));
    }

    [TestMethod]
    public void Read_UnknownTopLevelKeys_WarnOncePerKey_AndChangeNothing()
    {
        var table = Read("{\"rulez\":{\"minStake\":50},\"colour\":3,\"waves\":[" + GoodRow + "]}");

        Assert.AreEqual(WaveRules.Default, table.Rules, "a misspelled rules key leaves the default rules");
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("'rulez'")), string.Join(" | ", _warnings));
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("'colour'")), string.Join(" | ", _warnings));
        Assert.AreEqual(2, _warnings.Count, "an unknown key is no reversion, so no summary follows");
    }

    [TestMethod]
    public void Read_RulesOnly_KeepsTheDefaultWaves_AndWarnsThatWavesIsMissing()
    {
        var table = Read("{\"rules\":{\"minStake\":50}}");

        Assert.AreEqual(50, table.Rules.MinStake);
        Assert.AreEqual(WaveTable.Default.Waves.Count, table.Waves.Count);
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("no 'waves'")), string.Join(" | ", _warnings));
    }

    // ---- a row that breaks a rule is skipped

    [DataTestMethod]
    [DataRow("count", "0")]
    [DataRow("count", "61")]
    [DataRow("count", "-3")]
    [DataRow("count", "2.5")]
    [DataRow("count", "\"3\"")]
    [DataRow("count", "null")]
    [DataRow("atOnce", "0")]
    [DataRow("atOnce", "-1")]
    [DataRow("atOnce", "1.5")]
    [DataRow("tiers", "{\"2\":1}")]
    [DataRow("tiers", "{\"6\":1}")]
    [DataRow("tiers", "{\"x\":1}")]
    [DataRow("tiers", "{\"3\":-1}")]
    [DataRow("tiers", "{\"3\":0,\"4\":0,\"5\":0}")]
    [DataRow("tiers", "{}")]
    [DataRow("tiers", "{\"3\":\"a\"}")]
    [DataRow("tiers", "{\"3\":1.5}")]
    [DataRow("tiers", "[1]")]
    [DataRow("weaponStage", "0")]
    [DataRow("weaponStage", "4")]
    [DataRow("cap", "9")]
    [DataRow("cap", "1000001")]
    [DataRow("cap", "5000000000")]
    [DataRow("oddsTenths", "10")]
    [DataRow("oddsTenths", "41")]
    public void Read_RowBreakingARule_IsSkipped_AndTheNextRowIsKept(string field, string valueJson)
    {
        var table = Read(File(null, RowWith(field, valueJson), RowOf(5, 2, 1000)));

        Assert.AreEqual(1, table.Waves.Count, field + "=" + valueJson);
        Assert.AreEqual(5, table.Waves[0].Count);
        Assert.IsTrue(_warnings.Any(w => w.Contains("waves[0]")), "no warning names the row: " + string.Join(" | ", _warnings));
    }

    [DataTestMethod]
    [DataRow("count")]
    [DataRow("atOnce")]
    [DataRow("tiers")]
    [DataRow("weaponStage")]
    [DataRow("cap")]
    [DataRow("oddsTenths")]
    public void Read_RowMissingAField_IsSkipped(string field)
    {
        var table = Read(File(null, RowWithout(field), RowOf(5, 2, 1000)));

        Assert.AreEqual(1, table.Waves.Count);
        Assert.AreEqual(5, table.Waves[0].Count);
    }

    [TestMethod]
    public void Read_RowThatIsNotAnObject_IsSkipped()
    {
        var table = Read(File(null, "7", RowOf(5, 2, 1000)));

        Assert.AreEqual(1, table.Waves.Count);
        Assert.IsTrue(_warnings.Count >= 1);
    }

    [TestMethod]
    public void Read_ValuesAtEachBound_AreAccepted()
    {
        var table = Read(File(null,
            "{\"count\":1,\"atOnce\":1,\"tiers\":{\"5\":1},\"weaponStage\":3,\"cap\":10,\"oddsTenths\":11}",
            "{\"count\":60,\"atOnce\":60,\"tiers\":{\"3\":1},\"weaponStage\":1,\"cap\":1000000,\"oddsTenths\":40}"));

        Assert.AreEqual(2, table.Waves.Count);
        Assert.AreEqual(0, _warnings.Count, string.Join(" | ", _warnings));
    }

    // ---- a row that can be adjusted is adjusted, with a warning

    [TestMethod]
    public void Read_AtOnceAboveCount_IsClampedToCount_WithAWarning()
    {
        var table = Read(File(null, RowOf(4, 9, 500)));

        Assert.AreEqual(4, table.Waves[0].AtOnce);
        Assert.IsTrue(_warnings.Any(w => w.Contains("atOnce")));
    }

    [TestMethod]
    public void Read_CapBelowThePreviousRow_IsRaisedToIt_WithAWarning()
    {
        var table = Read(File(null, RowOf(3, 1, 800), RowOf(4, 2, 300), RowOf(5, 2, 900)));

        CollectionAssert.AreEqual(new[] { 800, 800, 900 }, table.Waves.Select(w => w.Cap).ToArray());
        Assert.IsTrue(_warnings.Any(w => w.Contains("cap")));
    }

    [TestMethod]
    public void Read_CapAfterASkippedRow_IsComparedWithTheLastKeptRow()
    {
        var table = Read(File(null, RowOf(3, 1, 800), RowOf(0, 1, 5000), RowOf(4, 2, 300)));

        CollectionAssert.AreEqual(new[] { 800, 800 }, table.Waves.Select(w => w.Cap).ToArray());
    }

    [TestMethod]
    public void Read_MoreThanTwentyRows_KeepsTheFirstTwenty_WithAWarning()
    {
        var rows = Enumerable.Range(0, 25).Select(i => RowOf(3, 1, 500)).ToArray();

        var table = Read(File(null, rows));

        Assert.AreEqual(20, table.Waves.Count);
        Assert.IsTrue(_warnings.Count >= 1);
    }

    [TestMethod]
    public void Read_NoValidRow_GivesTheDefaultWaves_AndWarns()
    {
        var table = Read(File("{\"minStake\":50}", RowOf(0, 1, 500)));

        AssertSameTable(new WaveTable(WaveTable.Default.Waves, new WaveRules(50, 15_000, true, 5, 3)), table);
        Assert.IsTrue(_warnings.Count >= 2, "a row warning and a summary");
    }

    [TestMethod]
    public void Read_WavesThatAreNotAnArray_GiveTheDefaultWaves_AndWarn()
    {
        var table = Read("{\"waves\":{\"count\":3}}");

        AssertSameTable(WaveTable.Default, table);
        Assert.IsTrue(_warnings.Count >= 1);
    }

    // ---- the rules

    [DataTestMethod]
    [DataRow("minStake", "0")]
    [DataRow("minStake", "1001")]
    [DataRow("minStake", "2.5")]
    [DataRow("minStake", "\"10\"")]
    [DataRow("hardCeiling", "99")]
    [DataRow("hardCeiling", "1000001")]
    [DataRow("hardCeiling", "null")]
    [DataRow("returnSeconds", "0")]
    [DataRow("returnSeconds", "31")]
    [DataRow("refillSeconds", "0")]
    [DataRow("refillSeconds", "31")]
    [DataRow("oneRunPerTownPerDay", "1")]
    [DataRow("oneRunPerTownPerDay", "\"true\"")]
    public void Read_RuleBreakingItsRange_RevertsToItsDefault_WithAWarning(string key, string valueJson)
    {
        var table = Read(File("{\"" + key + "\":" + valueJson + "}", GoodRow));

        Assert.AreEqual(WaveRules.Default, table.Rules, key + "=" + valueJson);
        Assert.AreEqual(1, table.Waves.Count);
        Assert.IsTrue(_warnings.Any(w => w.Contains(key)), string.Join(" | ", _warnings));
    }

    [TestMethod]
    public void Read_RuleValuesAtTheirBounds_AreAccepted()
    {
        var low = Read(File("{\"minStake\":1,\"hardCeiling\":100,\"returnSeconds\":1,\"refillSeconds\":1}", GoodRow));
        var high = Read(File("{\"minStake\":1000,\"hardCeiling\":1000000,\"returnSeconds\":30,\"refillSeconds\":30}", GoodRow));

        Assert.AreEqual(new WaveRules(1, 100, true, 1, 1), low.Rules);
        Assert.AreEqual(new WaveRules(1000, 1_000_000, true, 30, 30), high.Rules);
        Assert.AreEqual(0, _warnings.Count, string.Join(" | ", _warnings));
    }

    [TestMethod]
    public void Read_HardCeilingBelowMinStake_RevertsBothToTheirDefaults()
    {
        var table = Read(File("{\"minStake\":500,\"hardCeiling\":300}", GoodRow));

        Assert.AreEqual(10, table.Rules.MinStake);
        Assert.AreEqual(15_000, table.Rules.HardCeiling);
        Assert.IsTrue(_warnings.Any(w => w.Contains("hardCeiling")));
    }

    [TestMethod]
    public void Read_UnknownRulesKey_WarnsOnce_AndChangesNothing()
    {
        var table = Read(File("{\"colour\":3}", GoodRow));

        Assert.AreEqual(WaveRules.Default, table.Rules);
        Assert.AreEqual(1, _warnings.Count(w => w.Contains("colour")));
        Assert.AreEqual(1, _warnings.Count, "an unknown key is no reversion, so no summary follows");
    }

    [TestMethod]
    public void Read_RulesThatAreNotAnObject_KeepTheDefaultRules_AndWarn()
    {
        var table = Read("{\"rules\":5,\"waves\":[" + GoodRow + "]}");

        Assert.AreEqual(WaveRules.Default, table.Rules);
        Assert.IsTrue(_warnings.Count >= 1);
    }

    // ---- the summary

    [TestMethod]
    public void Read_AnyReversion_AddsOneSummaryWarning()
    {
        Read(File("{\"returnSeconds\":99}", GoodRow));

        Assert.AreEqual(2, _warnings.Count, string.Join(" | ", _warnings));
        StringAssert.Contains(_warnings[1], "reverted");
    }

    [TestMethod]
    public void Read_ASkippedRow_AddsOneSummaryWarning()
    {
        Read(File(null, RowOf(0, 1, 500), RowOf(3, 1, 500)));

        Assert.AreEqual(2, _warnings.Count, string.Join(" | ", _warnings));
        StringAssert.Contains(_warnings[1], "reverted");
    }

    [TestMethod]
    public void Read_NeverThrows_OnHostileText()
    {
        foreach (var text in new[] { "null", "\"x\"", "{\"waves\":null}", "{\"rules\":null,\"waves\":[null,[],{}]}", "{\"waves\":[{\"tiers\":null}]}", "\u0000" })
        {
            var table = Read(text);
            Assert.IsTrue(table.Waves.Count > 0, text);
        }
    }
}
