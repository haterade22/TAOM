using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.SaveLoadDiagnostics;

[TestClass]
public class BehaviorRecordStatsTests
{
    [TestMethod]
    public void Measure_NullRecords_IsEmpty()
    {
        var stats = BehaviorRecordStats.Measure(null);

        Assert.AreEqual(0, stats.Records);
        Assert.AreEqual(0L, stats.Elements);
        Assert.AreEqual(0, stats.MaxStringBytes);
        Assert.IsNull(stats.MaxStringKey);
    }

    [TestMethod]
    public void Measure_CountsRecordsAndCollectionElements()
    {
        var stats = BehaviorRecordStats.Measure(new Dictionary<string, object>
        {
            ["a"] = new List<int> { 1, 2, 3 },
            ["b"] = new Dictionary<string, int> { { "x", 1 } },
            ["c"] = 5,
        });

        Assert.AreEqual(3, stats.Records);
        Assert.AreEqual(4L, stats.Elements);
    }

    [TestMethod]
    public void Measure_FindsTheLongestUtf8StringInValuesKeysAndElements()
    {
        // U+20AC is three UTF-8 bytes, so the list element (9 bytes) beats the longer-looking ASCII strings.
        var stats = BehaviorRecordStats.Measure(new Dictionary<string, object>
        {
            ["plain"] = "abcdefgh",
            ["keys"] = new Dictionary<string, int> { { "abcdefg", 1 } },
            ["names"] = new List<string> { "a", "€€€" },
        });

        Assert.AreEqual(9, stats.MaxStringBytes);
        Assert.AreEqual("names", stats.MaxStringKey);
    }

    [TestMethod]
    public void FormatSummary_SummaryLineThenOnlyBehaviorsOverAThreshold()
    {
        var samples = new[]
        {
            new BehaviorSaveSample("small", "T.Small", new BehaviorRecordStats(1, 10, 5, "k"), 1.0),
            new BehaviorSaveSample("big", "T.Big", new BehaviorRecordStats(2, 12_000, 10, "k"), 2.0),
            new BehaviorSaveSample("wordy", "T.Wordy", new BehaviorRecordStats(3, 5, 31_000, "text"), 3.0),
            new BehaviorSaveSample("slow", "T.Slow", new BehaviorRecordStats(4, 6, 20, "k"), 300.0),
        };

        var lines = BehaviorSaveSummary.Format(samples);

        Assert.AreEqual(4, lines.Count);
        Assert.AreEqual(
            "behaviors=4 records=10 elements=12021 syncMs=306.0 maxStringBytes=31000 maxStringBehavior='wordy:text' slowest='slow' slowestMs=300.0",
            lines[0]);
        Assert.AreEqual("behavior='big' type='T.Big' records=2 elements=12000 maxStringBytes=10 syncMs=2.0", lines[1]);
        Assert.AreEqual("behavior='slow' type='T.Slow' records=4 elements=6 maxStringBytes=20 syncMs=300.0", lines[2]);
        Assert.AreEqual("behavior='wordy' type='T.Wordy' records=3 elements=5 maxStringBytes=31000 syncMs=3.0", lines[3]);
    }
}
