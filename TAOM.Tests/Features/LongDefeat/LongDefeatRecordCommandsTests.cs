using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

    [TestMethod]
    public void Record_ASallyWithAVerticalLook_IsRefused()
    {
        var refused = Record(Spot(lookX: 0f, lookY: 0f), "sally");

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

        StringAssert.Contains(reply, "Paste its sally into");
        StringAssert.Contains(reply, "fortresses.json");
        Assert.IsFalse(reply.Contains("Paste its keep"), reply);
    }

    [TestMethod]
    public void Export_WithNoKeepRecorded_DoesNotTellThePlayerToPasteOne()
    {
        Record(Spot(), "muster_foot");

        var reply = LongDefeatCommands.Export(_recorder, Scene);

        Assert.IsFalse(reply.Contains("fortresses.json"), reply);
        StringAssert.Contains(reply, "keep");
    }
}
