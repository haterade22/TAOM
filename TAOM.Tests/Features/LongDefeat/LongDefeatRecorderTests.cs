using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>The recorder's file half: lines appended, read back, folded into the block a person pastes into fortresses.json.</summary>
[TestClass]
public class LongDefeatRecorderTests
{
    private const string Scene = "test_fortress_001";

    private string _dir = null!;
    private LongDefeatRecorder _recorder = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom-ld-recorder-" + Guid.NewGuid().ToString("N"));
        _recorder = new LongDefeatRecorder(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [TestMethod]
    public void Append_WritesOneParseableLinePerCall()
    {
        var line = _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1.5f, 2.5f, 3.5f, 0f, 1f, 12f);

        var records = _recorder.ReadAll(out var bad);
        Assert.AreEqual(0, bad);
        Assert.AreEqual(1, records.Count);
        Assert.AreEqual(Scene, records[0].Scene);
        Assert.AreEqual(LongDefeatRecorder.KeepKind, records[0].Kind);
        Assert.AreEqual(2.5f, records[0].Y);
        Assert.AreEqual(12f, records[0].Radius);
        StringAssert.Contains(line, "\"kind\":\"keep\"");
    }

    [TestMethod]
    public void ReadAll_CountsGarbageAndIncompleteLines_AndKeepsTheRest()
    {
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 2f, 3f, 0f, 1f, null);
        File.AppendAllText(_recorder.FilePath, "not json\n{\"scene\":\"x\"}\n\n");

        var records = _recorder.ReadAll(out var bad);

        Assert.AreEqual(2, bad);
        Assert.AreEqual(1, records.Count);
        Assert.IsNull(records[0].Radius);
    }

    [TestMethod]
    public void ReadAll_WithNoFile_IsEmpty()
    {
        Assert.AreEqual(0, _recorder.ReadAll(out var bad).Count);
        Assert.AreEqual(0, bad);
    }

    [TestMethod]
    public void Export_FoldsTheNewestRecordPerKind_ForThatSceneOnly()
    {
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 1f, 1f, 0f, 1f, 8f);
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 2f, 3f, 4f, 0f, 1f, 9f);
        _recorder.Append("other_scene", LongDefeatRecorder.KeepKind, 9f, 9f, 9f, 0f, 1f, 30f);
        _recorder.Append(Scene, LongDefeatRecorder.MusterFootKind, 5f, 6f, 7f, 1f, 0f, null);

        var export = _recorder.Export(Scene, out var path);

        var json = JObject.Parse(File.ReadAllText(path));
        Assert.AreEqual(Scene, (string?)json["scene"]);
        Assert.AreEqual(3f, (float)json["keep"]!["y"]!);
        Assert.AreEqual(9f, (float)json["keep"]!["radius"]!);
        Assert.AreEqual(5f, (float)json["muster"]!["foot"]!["x"]!);
        Assert.AreEqual(1f, (float)json["muster"]!["foot"]!["fx"]!);
        Assert.IsNull(json["muster"]!["archers"]);
        CollectionAssert.AreEqual(new[] { LongDefeatRecorder.MusterArchersKind }, export.Missing.ToArray());
        Assert.IsFalse(export.IsComplete);
    }

    [TestMethod]
    public void Export_WithEveryKind_IsComplete()
    {
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.MusterFootKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.MusterArchersKind, 1f, 1f, 1f, 0f, 1f, null);

        Assert.IsTrue(_recorder.Export(Scene, out _).IsComplete);
    }

    [TestMethod]
    public void Export_WithNothingRecorded_WritesAFileAndNamesEveryMissingPiece()
    {
        var export = _recorder.Export(Scene, out var path);

        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(3, export.Missing.Count);
        Assert.IsNull(JObject.Parse(File.ReadAllText(path))["keep"]);
    }

    [TestMethod]
    public void TheKeepBlockOfAnExport_IsReadBackByTheFortressCatalog()
    {
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 12.5f, -3f, 40f, 0f, 1f, 9f);
        var keep = JObject.Parse(File.ReadAllText(ExportedPath()))["keep"]!;
        var fortresses = JObject.Parse(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "fortresses.json")));
        fortresses["fortresses"]![0]!["keep"] = keep;

        var catalog = LongDefeatConfigCatalog.FromTexts(fortresses.ToString(),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "waves.json")),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "shop.json")), Substitute.For<IModLogger>());

        var point = catalog.Fortresses[0].Keep!;
        Assert.AreEqual(12.5f, point.X);
        Assert.AreEqual(-3f, point.Y);
        Assert.AreEqual(40f, point.Z);
        Assert.AreEqual(9f, point.Radius);
    }

    [TestMethod]
    public void ExportPath_MakesTheSceneNameASafeFileName()
    {
        StringAssert.EndsWith(_recorder.ExportPath("a/b:c"), "long_defeat_a_b_c.json");
    }

    [TestMethod]
    public void ParseRadius_AcceptsNoneAndTheCatalogsRange_RefusesTheRest()
    {
        Assert.IsNull(LongDefeatRecorder.ParseRadius(null, out var none));
        Assert.IsNull(none);
        Assert.IsNull(LongDefeatRecorder.ParseRadius("2", out var low));
        Assert.AreEqual(2f, low);
        Assert.IsNull(LongDefeatRecorder.ParseRadius("50", out var high));
        Assert.AreEqual(50f, high);
        foreach (var bad in new[] { "abc", "NaN", "Infinity", "1.9", "50.1", "-5" })
            Assert.IsNotNull(LongDefeatRecorder.ParseRadius(bad, out var r), bad + " should be refused");
    }

    [TestMethod]
    public void NormalizeLook_GivesAUnitGroundDirection_AndRefusesAVerticalOrNaNLook()
    {
        Assert.IsNull(LongDefeatRecorder.NormalizeLook(3f, 4f, out var fx, out var fy));
        Assert.AreEqual(0.6f, fx, 1e-5f);
        Assert.AreEqual(0.8f, fy, 1e-5f);
        Assert.IsNotNull(LongDefeatRecorder.NormalizeLook(0f, 0f, out _, out _));
        Assert.IsNotNull(LongDefeatRecorder.NormalizeLook(float.NaN, 1f, out _, out _));
    }

    [TestMethod]
    public void Sally_IsAKind_ThatAppendsAndReadsBackLikeTheMusterPoints()
    {
        Assert.IsTrue(LongDefeatRecorder.IsKind(LongDefeatRecorder.SallyKind));
        Assert.AreEqual("sally", LongDefeatRecorder.SallyKind);

        _recorder.Append(Scene, LongDefeatRecorder.SallyKind, -20.5f, 70f, 12f, 0f, -1f, null);

        var record = _recorder.ReadAll(out var bad).Single();
        Assert.AreEqual(0, bad);
        Assert.AreEqual(LongDefeatRecorder.SallyKind, record.Kind);
        Assert.AreEqual(-1f, record.Fy);
    }

    [TestMethod]
    public void Export_ARecordedSally_BecomesTheSallyBlock_InTheMusterShape_AndTheNewestOneWins()
    {
        _recorder.Append(Scene, LongDefeatRecorder.SallyKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.SallyKind, -20.5f, 70f, 12f, 0f, -1f, null);

        var export = _recorder.Export(Scene, out var path);

        var sally = JObject.Parse(File.ReadAllText(path))["sally"]!;
        Assert.AreEqual(-20.5f, (float)sally["x"]!);
        Assert.AreEqual(70f, (float)sally["y"]!);
        Assert.AreEqual(12f, (float)sally["z"]!);
        Assert.AreEqual(0f, (float)sally["fx"]!);
        Assert.AreEqual(-1f, (float)sally["fy"]!);
        Assert.IsTrue(export.HasSally);
    }

    [TestMethod]
    public void Export_WithNoSally_HasNoSallyBlock_AndASallyIsNeverAMissingPiece()
    {
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.MusterFootKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.MusterArchersKind, 1f, 1f, 1f, 0f, 1f, null);

        var export = _recorder.Export(Scene, out var path);

        Assert.IsNull(JObject.Parse(File.ReadAllText(path))["sally"]);
        Assert.IsFalse(export.HasSally);
        Assert.IsTrue(export.IsComplete, "the sally is optional: a scene without one is complete");
        CollectionAssert.DoesNotContain(export.Missing.ToArray(), LongDefeatRecorder.SallyKind);
    }

    [TestMethod]
    public void TheSallyBlockOfAnExport_IsReadBackByTheFortressCatalog()
    {
        _recorder.Append(Scene, LongDefeatRecorder.SallyKind, -20.5f, 70f, 12f, 0f, -1f, null);
        var sally = JObject.Parse(File.ReadAllText(ExportedPath()))["sally"]!;
        var fortresses = JObject.Parse(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "fortresses.json")));
        fortresses["fortresses"]![0]!["sally"] = sally;

        var catalog = LongDefeatConfigCatalog.FromTexts(fortresses.ToString(),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "waves.json")),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "shop.json")), Substitute.For<IModLogger>());

        var point = catalog.Fortresses[0].Sally!;
        Assert.AreEqual(-20.5f, point.X);
        Assert.AreEqual(70f, point.Y);
        Assert.AreEqual(12f, point.Z);
        Assert.AreEqual(-1f, point.Fy);
    }

    private string ExportedPath()
    {
        _recorder.Export(Scene, out var path);
        return path;
    }
}
