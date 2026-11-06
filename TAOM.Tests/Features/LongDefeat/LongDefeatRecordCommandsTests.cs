using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary><c>taom.ld_record</c> and <c>taom.ld_export</c>: the bodies run over a temp folder and a stated spot, so no engine is needed.</summary>
[TestClass]
public class LongDefeatRecordCommandsTests
{
    private const string Scene = "taom_rohan_castle_helms_deep_forceatmo";

    private string _dir = null!;
    private LongDefeatRecorder _recorder = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom-ld-record-cmd-" + Guid.NewGuid().ToString("N"));
        _recorder = new LongDefeatRecorder(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private static LdSpot Spot(float x = 10f, float y = 20f, float z = 30f, float lookX = 0f, float lookY = 1f) => new(Scene, x, y, z, lookX, lookY);

    private string Record(LdSpot spot, params string[] args) => LongDefeatCommands.Record(args, _recorder, spot);

    [TestMethod]
    public void Record_Keep_AppendsTheSpotWithNoRadiusUnlessOneIsTyped()
    {
        var reply = Record(Spot(), "keep");

        var record = _recorder.ReadAll(out _).Single();
        Assert.AreEqual(LongDefeatRecorder.KeepKind, record.Kind);
        Assert.AreEqual(Scene, record.Scene);
        Assert.AreEqual(30f, record.Z);
        Assert.IsNull(record.Radius, "without a typed radius the rules' keepRadius stays in charge");
        StringAssert.Contains(reply, _recorder.FilePath);
    }

    [TestMethod]
    public void Record_Keep_ATypedRadiusIsWritten()
    {
        Record(Spot(), "keep", "14");

        Assert.AreEqual(14f, _recorder.ReadAll(out _).Single().Radius);
    }

    [TestMethod]
    public void Record_ABadRadius_IsRefusedAndWritesNothing()
    {
        var reply = Record(Spot(), "keep", "200");

        StringAssert.Contains(reply, "Radius");
        Assert.IsFalse(File.Exists(_recorder.FilePath));
    }

    [TestMethod]
    public void Record_MusterFoot_StoresTheNormalizedLook()
    {
        Record(Spot(lookX: 3f, lookY: 4f), "muster_foot");

        var record = _recorder.ReadAll(out _).Single();
        Assert.AreEqual(0.6f, record.Fx, 1e-5f);
        Assert.AreEqual(0.8f, record.Fy, 1e-5f);
    }

    [TestMethod]
    public void Record_AMusterWithAVerticalLook_IsRefused_ButAKeepKeepsItsPoint()
    {
        var refused = Record(Spot(lookX: 0f, lookY: 0f), "muster_archers");
        Assert.IsFalse(File.Exists(_recorder.FilePath), refused);

        Record(Spot(lookX: 0f, lookY: 0f), "keep");
        Assert.AreEqual(1, _recorder.ReadAll(out _).Count, "a keep reads no facing, so a steep look still records its point");
    }

    [TestMethod]
    public void Record_ANonFiniteOrFarPosition_IsRefused()
    {
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, 100_001f })
        {
            Record(Spot(x: bad), "keep");
            Record(Spot(y: bad), "keep");
            Record(Spot(z: bad), "keep");
        }

        Assert.IsFalse(File.Exists(_recorder.FilePath), "the fortress reader would skip such a keep block");
    }

    [DataTestMethod]
    [DataRow]
    [DataRow("point")]
    [DataRow("MUSTER")]
    public void Record_ANoKindOrAnUnknownKind_AnswersTheUsage(params string[] args)
    {
        var reply = Record(Spot(), args);

        StringAssert.Contains(reply, "keep");
        StringAssert.Contains(reply, "muster_foot");
        Assert.IsFalse(File.Exists(_recorder.FilePath));
    }

    [TestMethod]
    public void Record_TheKindIsMatchedWithoutCase()
    {
        Record(Spot(), "KEEP");

        Assert.AreEqual(LongDefeatRecorder.KeepKind, _recorder.ReadAll(out _).Single().Kind);
    }

    [TestMethod]
    public void Export_NamesThePiecesStillMissing_ThenSaysCompleteOnceAllAreIn()
    {
        Record(Spot(), "keep");
        var partial = LongDefeatCommands.Export(_recorder, Scene);
        Record(Spot(), "muster_foot");
        Record(Spot(), "muster_archers");
        var complete = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(partial, "muster_foot");
        StringAssert.Contains(partial, "muster_archers");
        StringAssert.Contains(complete, "complete");
        StringAssert.Contains(complete, _recorder.ExportPath(Scene));
    }

    [TestMethod]
    public void Record_Sally_StoresThePointAndTheNormalizedLook_AndTakesNoRadius()
    {
        var reply = Record(Spot(x: -20.5f, y: 70f, z: 12f, lookX: 0f, lookY: -2f), "sally");

        var record = _recorder.ReadAll(out _).Single();
        Assert.AreEqual(LongDefeatRecorder.SallyKind, record.Kind);
        Assert.AreEqual(-20.5f, record.X);
        Assert.AreEqual(-1f, record.Fy, 1e-5f);
        StringAssert.Contains(reply, _recorder.FilePath);
        Assert.AreEqual("Only a keep record takes a radius.", Record(Spot(), "sally", "12"));
    }

    [DataTestMethod]
    [DataRow("armoury")]
    [DataRow("yard")]
    public void Record_ArmouryAndYard_StoreThePointAndTheNormalizedLook_AndTakeNoRadius(string kind)
    {
        Record(Spot(x: 30f, lookX: 2f, lookY: 0f), kind);

        var record = _recorder.ReadAll(out _).Single();
        Assert.AreEqual(kind, record.Kind);
        Assert.AreEqual(30f, record.X);
        Assert.AreEqual(1f, record.Fx, 1e-5f);
        Assert.AreEqual("Only a keep record takes a radius.", Record(Spot(), kind, "12"));
        StringAssert.Contains(LongDefeatCommands.RecordUsage, $"  {kind}: taom.ld_record {kind}.");
    }

    [TestMethod]
    public void Export_ArmouryAndYard_ArePrintedAsPasteBlocks_AndNamedWhileUnrecorded()
    {
        Record(Spot(), "volley");
        var before = LongDefeatCommands.Export(_recorder, Scene);
        Record(Spot(x: 30f), "armoury");
        Record(Spot(x: 60f), "yard");
        var after = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(before, "No record yet for: post, tower_volley, armoury, yard.");
        StringAssert.Contains(after, "Paste as the \"armoury\" field");
        StringAssert.Contains(after, "Paste as the \"yard\" field");
        StringAssert.Contains(after, "armoury 1, yard 1");
    }

    [TestMethod]
    public void Record_ClearOfAKindWithNoTower_RefusesATowerWord_InPlainWording()
    {
        var reply = Record(Spot(), "clear", "armoury", "hornburg");

        StringAssert.StartsWith(reply, "The armoury kind has no tower to name.");
    }

    [DataTestMethod]
    [DataRow("sally")]
    [DataRow("armoury")]
    [DataRow("yard")]
    public void Record_ASallyArmouryOrYardWithAVerticalLook_IsRefused(string kind)
    {
        var refused = Record(Spot(lookX: 0f, lookY: 0f), kind);

        Assert.IsFalse(File.Exists(_recorder.FilePath), refused);
    }

    [TestMethod]
    public void Record_TheUsageNamesTheSallyKind()
    {
        StringAssert.Contains(Record(Spot(), "nothing"), "sally");
        StringAssert.Contains(LongDefeatCommands.RecordUsage, "sally");
    }

    [TestMethod]
    public void Export_ASallyIsNotAMissingPiece_ButItsPasteIsMentioned()
    {
        Record(Spot(), "keep");
        Record(Spot(), "muster_foot");
        Record(Spot(), "muster_archers");
        var without = LongDefeatCommands.Export(_recorder, Scene);
        Record(Spot(), "sally");
        var with = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(without, "complete");
        Assert.IsFalse(without.Contains("sally"), without);
        StringAssert.Contains(with, "complete");
        StringAssert.Contains(with, "sally");
    }

    [TestMethod]
    public void Export_ARecordedKeepWithNoMusterPoints_StillSaysWhereToPasteIt()
    {
        Record(Spot(), "keep");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(reply, "fortresses.json");
        StringAssert.Contains(reply, "muster_foot");
    }

    [TestMethod]
    public void Export_ASallyWithNoKeepRecorded_StillSaysWhereToPasteTheSally()
    {
        Record(Spot(), "sally");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(reply, "Paste as the \"sally\" field");
        StringAssert.Contains(reply, "fortresses.json");
        Assert.IsFalse(reply.Contains("Paste as the \"keep\" field"), reply);
        Assert.IsFalse(reply.Contains("Paste its"), "the sally is offered once, as a block, not also in the first sentence: " + reply);
    }

    [TestMethod]
    public void Export_WithOnlyAMusterRecorded_PrintsTheMusterBlock_AndNoKeepBlock_AndNamesTheKeepAsMissing()
    {
        Record(Spot(), "muster_foot");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(reply, "Paste as the \"muster\" field");
        Assert.IsFalse(reply.Contains("Paste as the \"keep\" field"), reply);
        StringAssert.Contains(reply, "no record yet: keep");
    }

    [TestMethod]
    public void Export_APlainRecordedKeep_PrintsItsBlockWithTheRadiusWhenTyped()
    {
        Record(Spot(x: 7f), "keep", "14");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(reply, "Paste as the \"keep\" field");
        StringAssert.Contains(reply, "\"keep\": {\"x\":7.0,\"y\":20.0,\"z\":30.0,\"radius\":14.0}");
    }

    [DataTestMethod]
    [DataRow("clear", true)]
    [DataRow("CLEAR", true)]
    [DataRow("keep", false)]
    [DataRow("post", false)]
    public void IsClear_IsTrueForTheClearWordOnly_SoTheConsoleNeedsNoAgentForIt(string first, bool expected)
    {
        Assert.AreEqual(expected, LongDefeatCommands.IsClear(new[] { first, "volley" }));
        Assert.IsFalse(LongDefeatCommands.IsClear(Array.Empty<string>()));
    }

    [TestMethod]
    public void Record_Post_StoresTheTowerAndTheNormalizedLook_AndLowerCasesTheTowerWord()
    {
        var reply = Record(Spot(x: 1f, y: 2f, z: 3f, lookX: 3f, lookY: 4f), "POST", "Hornburg");

        var record = _recorder.ReadAll(out _).Single();
        Assert.AreEqual("post", record.Kind);
        Assert.AreEqual("hornburg", record.Tower);
        Assert.AreEqual(0.8f, record.Fy, 1e-5f);
        StringAssert.Contains(reply, _recorder.FilePath);
    }

    [TestMethod]
    public void Record_TowerVolley_StoresTheTower()
    {
        Record(Spot(), "tower_volley", "wall");

        var record = _recorder.ReadAll(out _).Single();
        Assert.AreEqual(LongDefeatRecorder.TowerVolleyKind, record.Kind);
        Assert.AreEqual("wall", record.Tower);
    }

    [TestMethod]
    public void Record_Volley_StoresAPlainPoint()
    {
        Record(Spot(x: 5f, y: 6f, z: 7f), "volley");

        var record = _recorder.ReadAll(out _).Single();
        Assert.AreEqual(LongDefeatRecorder.VolleyKind, record.Kind);
        Assert.AreEqual(7f, record.Z);
        Assert.IsNull(record.Tower);
    }

    [DataTestMethod]
    [DataRow("post")]
    [DataRow("tower_volley")]
    public void Record_ATowerKindWithNoTower_IsRefusedWithTheUsage_AndWritesNothing(string kind)
    {
        var reply = Record(Spot(), kind);

        StringAssert.Contains(reply, "tower");
        StringAssert.Contains(reply, "taom.ld_record " + kind + " hornburg");
        Assert.IsFalse(File.Exists(_recorder.FilePath));
    }

    [DataTestMethod]
    [DataRow("post", "tower 1")]
    [DataRow("post", "tower1")]
    [DataRow("tower_volley", "a/b")]
    [DataRow("post", "12")]
    public void Record_ATowerWordOutsideLettersAndUnderscores_IsRefused(string kind, string tower)
    {
        var reply = Record(Spot(), kind, tower);

        StringAssert.Contains(reply, "letters");
        Assert.IsFalse(File.Exists(_recorder.FilePath));
    }

    [TestMethod]
    public void Record_ATowerKindTakesNoRadius_AndVolleyTakesNoTower()
    {
        StringAssert.Contains(Record(Spot(), "post", "hornburg", "12"), "radius");
        StringAssert.Contains(Record(Spot(), "volley", "hornburg"), "tower");
        Assert.IsFalse(File.Exists(_recorder.FilePath));
    }

    [TestMethod]
    public void Record_ASteepLook_IsRefusedForAPost_ButAllowedForAVolleyAndATowerVolley()
    {
        var steep = Spot(lookX: 0f, lookY: 0f);

        StringAssert.Contains(Record(steep, "post", "hornburg"), "Look level");
        Assert.IsFalse(File.Exists(_recorder.FilePath));

        Record(steep, "volley");
        Record(steep, "tower_volley", "hornburg");
        Assert.AreEqual(2, _recorder.ReadAll(out _).Count, "an origin has no facing, so a steep look still records its point");
    }

    [DataTestMethod]
    [DataRow("keep")]
    [DataRow("muster_foot")]
    [DataRow("muster_archers")]
    [DataRow("sally")]
    [DataRow("post")]
    [DataRow("tower_volley")]
    [DataRow("volley")]
    [DataRow("clear")]
    public void TheUsage_NamesEveryKindWithAnExampleCommand(string word)
    {
        StringAssert.Contains(LongDefeatCommands.RecordUsage, "taom.ld_record " + word);
    }

    [TestMethod]
    public void TheUsage_ExampleLinesForTowerKindsNameATower_AndTheBadKindReplyListsEveryKind()
    {
        StringAssert.Contains(LongDefeatCommands.RecordUsage, "taom.ld_record post hornburg");
        StringAssert.Contains(LongDefeatCommands.RecordUsage, "taom.ld_record tower_volley hornburg");
        var reply = Record(Spot(), "towers");
        foreach (var kind in LongDefeatRecorder.AllKinds)
            StringAssert.Contains(reply, kind);
        StringAssert.Contains(reply, "clear");
    }

    [TestMethod]
    public void Clear_RemovesTheKindForThisScene_AndSaysHowMany()
    {
        Record(Spot(), "volley");
        Record(Spot(), "volley");
        Record(Spot(), "keep");

        var reply = Record(Spot(), "clear", "volley");

        StringAssert.Contains(reply, "2");
        CollectionAssert.AreEqual(new[] { "keep" }, _recorder.ReadAll(out _).Select(r => r.Kind).ToArray());
    }

    [TestMethod]
    public void Clear_WithATower_RemovesOnlyThatTowers()
    {
        Record(Spot(), "post", "hornburg");
        Record(Spot(), "post", "wall");

        var reply = Record(Spot(), "clear", "post", "Hornburg");

        StringAssert.Contains(reply, "hornburg");
        Assert.AreEqual("wall", _recorder.ReadAll(out _).Single().Tower);
    }

    [TestMethod]
    public void Clear_WorksOnTheOlderKindsToo()
    {
        Record(Spot(), "sally");

        Record(Spot(), "clear", "sally");

        Assert.AreEqual(0, _recorder.ReadAll(out _).Count);
    }

    [TestMethod]
    public void Clear_WithNothingRecorded_SaysSo()
    {
        StringAssert.Contains(Record(Spot(), "clear", "volley"), "Nothing");
    }

    [DataTestMethod]
    [DataRow]
    [DataRow("nothing")]
    [DataRow("volley", "hornburg")]
    [DataRow("keep", "hornburg")]
    [DataRow("post", "tower 1")]
    [DataRow("post", "hornburg", "extra")]
    public void Clear_ANoKindABadKindOrABadTower_AnswersTheUsage_AndRemovesNothing(params string[] clearArgs)
    {
        Record(Spot(), "volley");
        Record(Spot(), "post", "hornburg");

        var reply = Record(Spot(), new[] { "clear" }.Concat(clearArgs).ToArray());

        StringAssert.Contains(reply, "taom.ld_record clear");
        Assert.AreEqual(2, _recorder.ReadAll(out _).Count);
    }

    [TestMethod]
    public void Clear_NeedsNoUsablePosition_ButARecordDoes()
    {
        Record(Spot(), "volley");

        Record(Spot(x: float.NaN), "clear", "volley");

        Assert.AreEqual(0, _recorder.ReadAll(out _).Count);
    }

    [TestMethod]
    public void Export_PrintsOneBlockPerRecordedKindWithAnInstructionLine()
    {
        Record(Spot(x: 1f), "sally");
        Record(Spot(x: 2f), "post", "hornburg");
        Record(Spot(x: 3f), "tower_volley", "hornburg");
        Record(Spot(x: 4f), "volley");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(reply, "\"sally\":");
        StringAssert.Contains(reply, "\"towers\":");
        StringAssert.Contains(reply, "\"volley\":");
        Assert.AreEqual(3, reply.Split('\n').Count(l => l.StartsWith("Paste as the ")), reply);
        foreach (var field in new[] { "sally", "towers", "volley" })
            StringAssert.Contains(reply, $"Paste as the \"{field}\" field");
        StringAssert.Contains(reply, "fortresses.json");
    }

    [TestMethod]
    public void Export_EachPrintedBlock_IsPasteReadyJson_ThatTheReaderTakes()
    {
        Record(Spot(x: 1f), "sally");
        Record(Spot(x: 2f, lookX: 1f, lookY: 0f), "post", "hornburg");
        Record(Spot(x: 3f), "tower_volley", "hornburg");
        Record(Spot(x: 4f), "volley");
        Record(Spot(x: 5f), "volley");
        Record(Spot(x: 8f), "keep");
        Record(Spot(x: 9f), "muster_foot");
        Record(Spot(x: 10f), "muster_archers");
        Record(Spot(x: 11f), "armoury");
        Record(Spot(x: 12f), "yard");

        var lines = LongDefeatCommands.Export(_recorder, Scene).Split('\n');

        var fields = new JObject();
        foreach (var field in new[] { "keep", "muster", "sally", "towers", "volley", "armoury", "yard" })
        {
            var start = Array.FindIndex(lines, l => l.StartsWith($"Paste as the \"{field}\" field"));
            Assert.IsTrue(start >= 0, field);
            var body = lines.Skip(start + 1).TakeWhile(l => l.Length > 0 && !l.StartsWith("Paste as the ")).ToArray();
            var parsed = JObject.Parse("{" + string.Join("\n", body).TrimEnd().TrimEnd(',') + "}");
            fields.Merge(parsed);
        }

        Assert.AreEqual(8f, (float)fields["keep"]!["x"]!);
        Assert.AreEqual(9f, (float)fields["muster"]!["foot"]!["x"]!);
        Assert.AreEqual(10f, (float)fields["muster"]!["archers"]!["x"]!);
        Assert.AreEqual(1f, (float)fields["sally"]!["x"]!);
        Assert.AreEqual("hornburg", (string?)fields["towers"]![0]!["id"]);
        Assert.AreEqual(1f, (float)fields["towers"]![0]!["posts"]![0]!["fx"]!);
        Assert.AreEqual(2, fields["volley"]!["origins"]!.Count());
        Assert.AreEqual(1, fields["towers"]![0]!["volley"]!.Count());
        Assert.AreEqual(11f, (float)fields["armoury"]!["x"]!);
        Assert.AreEqual(12f, (float)fields["yard"]!["x"]!);
    }

    [TestMethod]
    public void Export_ShowsTheCountsPerKindAndPerTower_SoTheWorkCanBeChecked()
    {
        for (var i = 0; i < 3; i++)
            Record(Spot(x: i), "post", "hornburg");
        Record(Spot(), "post", "wall");
        Record(Spot(), "tower_volley", "wall");
        Record(Spot(), "tower_volley", "wall");
        Record(Spot(), "volley");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(reply, "post 4 (hornburg 3, wall 1)");
        StringAssert.Contains(reply, "tower_volley 2 (wall 2)");
        StringAssert.Contains(reply, "volley 1");
    }

    [TestMethod]
    public void Export_NamesTheNewKindsStillUnrecorded_ButNeverTheSally()
    {
        Record(Spot(), "volley");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        StringAssert.Contains(reply, "No record yet for: post, tower_volley");
        Assert.IsFalse(reply.Contains("sally"), reply);
    }

    [TestMethod]
    public void Export_WithOnlyTheOlderKinds_PrintsNoTowersOrVolleyBlock()
    {
        Record(Spot(), "keep");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        Assert.IsFalse(reply.Contains("\"towers\""), reply);
        Assert.IsFalse(reply.Contains("\"volley\""), reply);
    }
}
