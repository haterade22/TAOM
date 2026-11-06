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
    public void ArmouryAndYard_AreKinds_WithTheirFieldNames()
    {
        Assert.AreEqual("armoury", LongDefeatRecorder.ArmouryKind);
        Assert.AreEqual("yard", LongDefeatRecorder.YardKind);
        Assert.IsTrue(LongDefeatRecorder.IsKind(LongDefeatRecorder.ArmouryKind));
        Assert.IsTrue(LongDefeatRecorder.IsKind(LongDefeatRecorder.YardKind));
        Assert.IsFalse(LongDefeatRecorder.IsTowerKind(LongDefeatRecorder.ArmouryKind));
        Assert.IsFalse(LongDefeatRecorder.IsTowerKind(LongDefeatRecorder.YardKind));
    }

    [TestMethod]
    public void Export_ARecordedArmouryAndYard_BecomeTheirBlocks_InTheMusterShape_AndTheNewestOneWins()
    {
        _recorder.Append(Scene, LongDefeatRecorder.ArmouryKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.ArmouryKind, 30f, 40f, 5f, 1f, 0f, null);
        _recorder.Append(Scene, LongDefeatRecorder.YardKind, 60f, 70f, 2f, 0f, -1f, null);

        _recorder.Export(Scene, out var path);

        var root = JObject.Parse(File.ReadAllText(path));
        var armoury = root["armoury"]!;
        Assert.AreEqual(30f, (float)armoury["x"]!);
        Assert.AreEqual(40f, (float)armoury["y"]!);
        Assert.AreEqual(5f, (float)armoury["z"]!);
        Assert.AreEqual(1f, (float)armoury["fx"]!);
        Assert.AreEqual(0f, (float)armoury["fy"]!);
        var yard = root["yard"]!;
        Assert.AreEqual(60f, (float)yard["x"]!);
        Assert.AreEqual(-1f, (float)yard["fy"]!);
    }

    [TestMethod]
    public void Export_WithNoArmouryOrYard_HasNeitherBlock_AndNeitherIsAMissingPiece()
    {
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.MusterFootKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.MusterArchersKind, 1f, 1f, 1f, 0f, 1f, null);

        var export = _recorder.Export(Scene, out var path);

        var root = JObject.Parse(File.ReadAllText(path));
        Assert.IsNull(root["armoury"]);
        Assert.IsNull(root["yard"]);
        Assert.IsTrue(export.IsComplete, "the armoury and the yard are optional until Phase 4 reads them");
        CollectionAssert.DoesNotContain(export.Missing.ToArray(), LongDefeatRecorder.ArmouryKind);
        CollectionAssert.DoesNotContain(export.Missing.ToArray(), LongDefeatRecorder.YardKind);
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

    [TestMethod]
    public void Post_AppendsWithItsTower_AndReadsBack()
    {
        var line = _recorder.Append(Scene, LongDefeatRecorder.PostKind, 4f, 5f, 6f, 0f, 1f, null, "hornburg");

        var record = _recorder.ReadAll(out var bad).Single();
        Assert.AreEqual(0, bad);
        Assert.AreEqual("post", record.Kind);
        Assert.AreEqual("hornburg", record.Tower);
        StringAssert.Contains(line, "\"tower\":\"hornburg\"");
    }

    [TestMethod]
    public void AKindWithoutATower_WritesNoTowerField_AndReadsBackWithNone()
    {
        var line = _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 4f, 5f, 6f, 0f, 1f, null);

        Assert.IsFalse(line.Contains("\"tower\""), line);
        Assert.IsNull(_recorder.ReadAll(out _).Single().Tower);
    }

    [DataTestMethod]
    [DataRow("post", null)]
    [DataRow("tower_volley", null)]
    [DataRow("post", "")]
    [DataRow("post", "Hornburg")]
    [DataRow("tower_volley", "tower 1")]
    [DataRow("post", "tower2")]
    public void ReadAll_ATowerKindWithAMissingOrBadTower_IsABadLine(string kind, string? tower)
    {
        var o = new JObject { ["scene"] = Scene, ["kind"] = kind, ["x"] = 1, ["y"] = 2, ["z"] = 3, ["fx"] = 0, ["fy"] = 1 };
        if (tower != null)
            o["tower"] = tower;
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_recorder.FilePath, o.ToString(Newtonsoft.Json.Formatting.None) + "\n");

        var records = _recorder.ReadAll(out var bad);

        Assert.AreEqual(1, bad);
        Assert.AreEqual(0, records.Count);
    }

    [TestMethod]
    public void IsKind_CoversEveryKindTheRecorderWrites_AndTowerKindsAreTheTwoThatNameATower()
    {
        foreach (var kind in new[] { "keep", "muster_foot", "muster_archers", "sally", "post", "tower_volley", "volley" })
            Assert.IsTrue(LongDefeatRecorder.IsKind(kind), kind);
        Assert.IsFalse(LongDefeatRecorder.IsKind("towers"));
        CollectionAssert.AreEqual(new[] { "post", "tower_volley" }, LongDefeatRecorder.AllKinds.Where(LongDefeatRecorder.IsTowerKind).ToArray());
    }

    [TestMethod]
    public void IsTowerId_AcceptsLowerCaseLettersAndUnderscores_Only()
    {
        foreach (var good in new[] { "hornburg", "deeping_wall", "a", "_" })
            Assert.IsTrue(FortressesReader.IsTowerId(good), good);
        foreach (var bad in new[] { "", "Hornburg", "tower1", "a b", "a-b", "a/b", "caf" + (char)233 })
            Assert.IsFalse(FortressesReader.IsTowerId(bad), bad);
    }

    [TestMethod]
    public void Export_AppendsPostsAndSpotsPerTower_InTheOrderRecorded()
    {
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 1f, 1f, 1f, 0f, 1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 9f, 9f, 9f, 1f, 0f, null, "wall");
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 2f, 2f, 2f, 0f, -1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.TowerVolleyKind, 3f, 3f, 3f, 0f, 1f, null, "wall");
        _recorder.Append(Scene, LongDefeatRecorder.TowerVolleyKind, 4f, 4f, 4f, 0f, 1f, null, "hornburg");
        _recorder.Append("other_scene", LongDefeatRecorder.PostKind, 7f, 7f, 7f, 0f, 1f, null, "elsewhere");

        var towers = (JArray)JObject.Parse(File.ReadAllText(ExportedPath()))["towers"]!;

        CollectionAssert.AreEqual(new[] { "hornburg", "wall" }, towers.Select(t => (string)t["id"]!).ToArray());
        var hornburgPosts = (JArray)towers[0]["posts"]!;
        Assert.AreEqual(2, hornburgPosts.Count);
        Assert.AreEqual(1f, (float)hornburgPosts[0]["x"]!);
        Assert.AreEqual(2f, (float)hornburgPosts[1]["x"]!);
        Assert.AreEqual(-1f, (float)hornburgPosts[1]["fy"]!);
        Assert.AreEqual(4f, (float)towers[0]["volley"]![0]!["x"]!);
        Assert.AreEqual(9f, (float)towers[1]["posts"]![0]!["x"]!);
        Assert.AreEqual(3f, (float)towers[1]["volley"]![0]!["z"]!);
    }

    [TestMethod]
    public void Export_ATowerVolleySpotIsJustAPoint_AndATowerWithNoPostsHasNoPostsField()
    {
        _recorder.Append(Scene, LongDefeatRecorder.TowerVolleyKind, 3f, 4f, 5f, 0f, 1f, null, "wall");

        var tower = JObject.Parse(File.ReadAllText(ExportedPath()))["towers"]![0]!;

        Assert.IsNull(tower["posts"]);
        CollectionAssert.AreEquivalent(new[] { "x", "y", "z" }, ((JObject)tower["volley"]![0]!).Properties().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    public void Export_VolleyOriginsKeepEveryRecord_InOrder_AsPlainPoints()
    {
        _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 1f, 2f, 3f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 4f, 5f, 6f, 0f, 1f, null);
        _recorder.Append("other_scene", LongDefeatRecorder.VolleyKind, 7f, 8f, 9f, 0f, 1f, null);

        var origins = (JArray)JObject.Parse(File.ReadAllText(ExportedPath()))["volley"]!["origins"]!;

        Assert.AreEqual(2, origins.Count);
        Assert.AreEqual(1f, (float)origins[0]["x"]!);
        Assert.AreEqual(6f, (float)origins[1]["z"]!);
        CollectionAssert.AreEquivalent(new[] { "x", "y", "z" }, ((JObject)origins[0]).Properties().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    public void Export_WithNoTowerOrVolleyRecords_HasNeitherBlock_AndTheOtherKindsStayNewestOnly()
    {
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 1f, 1f, 0f, 1f, 8f);
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 2f, 3f, 4f, 0f, 1f, 9f);
        _recorder.Append(Scene, LongDefeatRecorder.SallyKind, 5f, 5f, 5f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.SallyKind, 6f, 6f, 6f, 0f, 1f, null);

        var json = JObject.Parse(File.ReadAllText(ExportedPath()));

        Assert.IsNull(json["towers"]);
        Assert.IsNull(json["volley"]);
        Assert.AreEqual(3f, (float)json["keep"]!["y"]!);
        Assert.AreEqual(6f, (float)json["sally"]!["x"]!);
    }

    [TestMethod]
    public void Export_TowerAndVolleyRecordsAreNeverAMissingPiece()
    {
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 1f, 1f, 1f, 0f, 1f, null, "wall");
        _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 1f, 1f, 1f, 0f, 1f, null);

        var export = _recorder.Export(Scene, out _);

        CollectionAssert.AreEquivalent(new[] { "keep", "muster_foot", "muster_archers" }, export.Missing.ToArray());
    }

    [TestMethod]
    public void Export_CountsRecordsPerKind_AndPerTower_ForThatSceneOnly()
    {
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 1f, 1f, 1f, 0f, 1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 2f, 1f, 1f, 0f, 1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 3f, 1f, 1f, 0f, 1f, null, "wall");
        _recorder.Append(Scene, LongDefeatRecorder.TowerVolleyKind, 3f, 3f, 3f, 0f, 1f, null, "wall");
        _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 1f, 2f, 3f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 2f, 1f, 1f, 0f, 1f, null);
        _recorder.Append("other_scene", LongDefeatRecorder.VolleyKind, 7f, 8f, 9f, 0f, 1f, null);

        var export = _recorder.Export(Scene, out _);

        Assert.AreEqual(2, export.CountOf(LongDefeatRecorder.KeepKind));
        Assert.AreEqual(0, export.CountOf(LongDefeatRecorder.SallyKind));
        Assert.AreEqual(3, export.CountOf(LongDefeatRecorder.PostKind));
        Assert.AreEqual(1, export.CountOf(LongDefeatRecorder.TowerVolleyKind));
        Assert.AreEqual(1, export.CountOf(LongDefeatRecorder.VolleyKind));
        CollectionAssert.AreEqual(new[] { "hornburg", "wall" }, export.Towers.Select(t => t.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 1 }, export.Towers.Select(t => t.Posts).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, export.Towers.Select(t => t.TowerVolley).ToArray());
    }

    [TestMethod]
    public void Clear_RemovesOneKindForThisSceneOnly_AndKeepsEverythingElseIncludingGarbageLines()
    {
        _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 1f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 2f, 1f, 1f, 0f, 1f, null);
        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 3f, 1f, 1f, 0f, 1f, null);
        _recorder.Append("other_scene", LongDefeatRecorder.VolleyKind, 4f, 1f, 1f, 0f, 1f, null);
        File.AppendAllText(_recorder.FilePath, "not json\n");

        var removed = _recorder.Clear(Scene, LongDefeatRecorder.VolleyKind, null);

        Assert.AreEqual(2, removed);
        var left = _recorder.ReadAll(out var bad);
        Assert.AreEqual(1, bad, "a line that is not a record is left where it was");
        CollectionAssert.AreEqual(new[] { "keep", "volley" }, left.Select(r => r.Kind).ToArray());
        Assert.AreEqual("other_scene", left.Last().Scene);
    }

    [TestMethod]
    public void Clear_WithATower_RemovesThatTowersRecordsOfThatKindOnly()
    {
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 1f, 1f, 1f, 0f, 1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 2f, 1f, 1f, 0f, 1f, null, "wall");
        _recorder.Append(Scene, LongDefeatRecorder.TowerVolleyKind, 3f, 1f, 1f, 0f, 1f, null, "hornburg");

        Assert.AreEqual(1, _recorder.Clear(Scene, LongDefeatRecorder.PostKind, "hornburg"));

        var left = _recorder.ReadAll(out _);
        CollectionAssert.AreEqual(new[] { "wall", "hornburg" }, left.Select(r => r.Tower).ToArray());
        CollectionAssert.AreEqual(new[] { "post", "tower_volley" }, left.Select(r => r.Kind).ToArray());
    }

    [TestMethod]
    public void Clear_WithoutATower_RemovesTheKindForEveryTower()
    {
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 1f, 1f, 1f, 0f, 1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 2f, 1f, 1f, 0f, 1f, null, "wall");

        Assert.AreEqual(2, _recorder.Clear(Scene, LongDefeatRecorder.PostKind, null));
        Assert.AreEqual(0, _recorder.ReadAll(out _).Count);
    }

    [TestMethod]
    public void Clear_WithNothingToRemove_ReturnsZero_AndLeavesTheFileAlone()
    {
        Assert.AreEqual(0, _recorder.Clear(Scene, LongDefeatRecorder.VolleyKind, null));
        Assert.IsFalse(File.Exists(_recorder.FilePath));

        _recorder.Append(Scene, LongDefeatRecorder.KeepKind, 1f, 1f, 1f, 0f, 1f, null);
        var before = File.ReadAllText(_recorder.FilePath);
        Assert.AreEqual(0, _recorder.Clear(Scene, LongDefeatRecorder.VolleyKind, null));
        Assert.AreEqual(before, File.ReadAllText(_recorder.FilePath));
    }

    [TestMethod]
    public void TheTowersAndVolleyBlocksOfAnExport_AreReadBackByTheFortressCatalog()
    {
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 1f, 2f, 3f, 0f, 1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.PostKind, 4f, 5f, 6f, 0.6f, 0.8f, null, "hornburg");
        for (var i = 2; i < 8; i++)
            _recorder.Append(Scene, LongDefeatRecorder.PostKind, i, 5f, 6f, 0f, 1f, null, "hornburg"); // the shipped shop's top tower level asks 8 posts, and a tower with fewer costs a warning
        _recorder.Append(Scene, LongDefeatRecorder.TowerVolleyKind, 7f, 8f, 9f, 0f, 1f, null, "hornburg");
        _recorder.Append(Scene, LongDefeatRecorder.VolleyKind, 10f, 11f, 12f, 0f, 1f, null);
        var block = JObject.Parse(File.ReadAllText(ExportedPath()));
        var fortresses = JObject.Parse(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "fortresses.json")));
        fortresses["fortresses"]![0]!["towers"] = block["towers"];
        fortresses["fortresses"]![0]!["volley"] = block["volley"];
        var logger = Substitute.For<IModLogger>();

        var catalog = LongDefeatConfigCatalog.FromTexts(fortresses.ToString(),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "waves.json")),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "shop.json")), logger);

        var fortress = catalog.Fortresses[0];
        var tower = fortress.Towers!.Single();
        Assert.AreEqual("hornburg", tower.Id);
        Assert.AreEqual(8, tower.Posts.Count);
        Assert.AreEqual(0.6f, tower.Posts[1].Fx, 1e-5f);
        Assert.AreEqual(9f, tower.VolleyOrigins.Single().Z);
        Assert.AreEqual(12f, fortress.Volley!.Origins.Single().Z);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TheArmouryAndYardBlocksOfAnExport_AreReadBackByTheFortressCatalog_AsTheRecordedPlaces()
    {
        _recorder.Append(Scene, LongDefeatRecorder.ArmouryKind, 10f, 20f, 3f, 0.6f, 0.8f, null);
        _recorder.Append(Scene, LongDefeatRecorder.YardKind, 40f, 50f, 6f, 1f, 0f, null);
        var block = JObject.Parse(File.ReadAllText(ExportedPath()));
        var fortresses = JObject.Parse(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "fortresses.json")));
        fortresses["fortresses"]![0]!["armoury"] = block["armoury"];
        fortresses["fortresses"]![0]!["yard"] = block["yard"];
        var logger = Substitute.For<IModLogger>();

        var catalog = LongDefeatConfigCatalog.FromTexts(fortresses.ToString(),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "waves.json")),
            File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "shop.json")), logger);

        var fortress = catalog.Fortresses[0];
        Assert.AreEqual(10f, fortress.ArmouryPoint!.X);
        Assert.AreEqual(3f, fortress.ArmouryPoint.Z);
        Assert.AreEqual(0.6f, fortress.ArmouryPoint.Fx, 1e-5f);
        Assert.AreEqual(0.8f, fortress.ArmouryPoint.Fy, 1e-5f);
        Assert.AreEqual(40f, fortress.YardPoint!.X);
        Assert.AreEqual(1f, fortress.YardPoint.Fx, 1e-5f);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    private string ExportedPath()
    {
        _recorder.Export(Scene, out var path);
        return path;
    }
}
