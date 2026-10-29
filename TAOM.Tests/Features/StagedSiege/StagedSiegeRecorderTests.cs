using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The recorder's file half: lines appended, read back, folded and exported to a catalog-loadable file.</summary>
[TestClass]
public class StagedSiegeRecorderTests
{
    private const string Scene = "test_scene_001";

    private string _dir = null!;
    private StagedSiegeRecorder _recorder = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom-staged-siege-" + Guid.NewGuid().ToString("N"));
        _recorder = new StagedSiegeRecorder(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private void RecordAll()
    {
        foreach (var letter in new[] { 'A', 'B', 'C', 'D' })
        {
            var i = letter - 'A';
            _recorder.Append(Scene, letter, "point", 10f * i, 20f * i, 5f, 0f, 1f, 8f);
            if (letter != 'A')
                _recorder.Append(Scene, letter, "defender", 10f * i + 1, 20f * i + 1, 5f, 1f, 0f, null);
        }

        _recorder.Append(Scene, 'A', "detachment", 3f, 4f, 5f, 0f, -1f, null);
    }

    [TestMethod]
    public void Append_WritesOneParseableLinePerCall()
    {
        var line = _recorder.Append(Scene, 'B', "point", 1.5f, 2.5f, 3.5f, 0f, 1f, 8f);

        var records = _recorder.ReadAll(out var bad);
        Assert.AreEqual(0, bad);
        Assert.AreEqual(1, records.Count);
        Assert.AreEqual(Scene, records[0].Scene);
        Assert.AreEqual('B', records[0].Stage);
        Assert.AreEqual(1.5f, records[0].X);
        Assert.AreEqual(8f, records[0].Radius);
        StringAssert.Contains(line, "\"kind\":\"point\"");
    }

    [TestMethod]
    public void ReadAll_CountsGarbageLines()
    {
        _recorder.Append(Scene, 'A', "point", 1f, 2f, 3f, 0f, 1f, 8f);
        File.AppendAllText(_recorder.FilePath, "not json\n{\"scene\":\"x\"}\n");

        _recorder.ReadAll(out var bad);

        Assert.AreEqual(2, bad);
    }

    [TestMethod]
    public void LatestPoints_NewestPerStageWins_ForThatSceneOnly()
    {
        _recorder.Append(Scene, 'A', "point", 1f, 1f, 1f, 0f, 1f, 8f);
        _recorder.Append(Scene, 'A', "point", 2f, 2f, 2f, 0f, 1f, 8f);
        _recorder.Append("other_scene", 'A', "point", 9f, 9f, 9f, 0f, 1f, 8f);

        var latest = _recorder.LatestPoints(Scene, out _);

        Assert.AreEqual(1, latest.Count);
        Assert.AreEqual(2f, latest['A'].X);
    }

    [TestMethod]
    public void Export_CompleteScene_WritesFileTheCatalogLoadsWithNoWarnings()
    {
        RecordAll();

        var fragment = _recorder.Export(Scene, out var path);

        Assert.IsTrue(fragment.IsComplete);
        Assert.AreEqual(Path.Combine(_dir, "staged_siege_test_scene_001.json"), path);
        var logger = Substitute.For<IModLogger>();
        var catalog = StagedSiegeSceneCatalog.FromText(File.ReadAllText(path), logger);
        Assert.IsTrue(catalog.TryGetScene(Scene, out _));
        logger.DidNotReceiveWithAnyArgs().LogWarning(null!);
    }

    [TestMethod]
    public void Export_Incomplete_ListsWhatIsMissing()
    {
        _recorder.Append(Scene, 'A', "point", 1f, 1f, 1f, 0f, 1f, 8f);

        var fragment = _recorder.Export(Scene, out _);

        Assert.IsFalse(fragment.IsComplete);
        Assert.IsTrue(System.Linq.Enumerable.Contains(fragment.Missing, "point B"));
    }

    [TestMethod]
    public void ExportPath_ReplacesCharactersAFileNameCannotHold()
    {
        StringAssert.EndsWith(_recorder.ExportPath("a/b:c"), "staged_siege_a_b_c.json");
    }

    // --- what the record command accepts (the catalog refuses anything else) -----------------------------------------------

    [TestMethod]
    public void ParseRadius_NothingTyped_NoRadius()
    {
        Assert.IsNull(StagedSiegeRecorder.ParseRadius(null, out var radius));
        Assert.IsNull(radius);
    }

    [TestMethod]
    public void ParseRadius_InRange_Kept()
    {
        Assert.IsNull(StagedSiegeRecorder.ParseRadius("12.5", out var radius));
        Assert.AreEqual(12.5f, radius);
    }

    [DataTestMethod]
    [DataRow("abc")]
    [DataRow("1.9")]
    [DataRow("30.1")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    public void ParseRadius_BadOrOutsideTheCatalogRange_Refused(string text)
    {
        var refusal = StagedSiegeRecorder.ParseRadius(text, out var radius);

        Assert.IsNotNull(refusal);
        Assert.IsNull(radius);
    }

    [TestMethod]
    public void ParseRadius_RangeEdges_Accepted()
    {
        Assert.IsNull(StagedSiegeRecorder.ParseRadius("2", out _));
        Assert.IsNull(StagedSiegeRecorder.ParseRadius("30", out _));
    }

    [TestMethod]
    public void NormalizeLook_LevelGlance_IsNormalized()
    {
        Assert.IsNull(StagedSiegeRecorder.NormalizeLook(0f, 0.8f, out var fx, out var fy));
        Assert.AreEqual(0f, fx, 1e-6f);
        Assert.AreEqual(1f, fy, 1e-6f);
    }

    [DataTestMethod]
    [DataRow(0f, 0f)]
    [DataRow(0.01f, 0.01f)]
    [DataRow(0.3f, 0.2f)]
    [DataRow(float.NaN, 1f)]
    public void NormalizeLook_StraightUpOrDegenerate_Refused(float x, float y)
    {
        var refusal = StagedSiegeRecorder.NormalizeLook(x, y, out _, out _);

        Assert.IsNotNull(refusal);
        StringAssert.Contains(refusal, "level");
    }
}
