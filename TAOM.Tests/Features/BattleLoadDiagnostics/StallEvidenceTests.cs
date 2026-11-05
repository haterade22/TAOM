using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// The next-session notice says what the last load was doing, from the prior log's tail. The [Freeze]
/// lines are built with FreezeLog so the contract stays one.
/// </summary>
[TestClass]
public class StallEvidenceTests
{
    private const string Stamp = "[2026-11-01 20:48:25] [INFO] ";

    private static string Phase(int seq, string phase, string detail = "")
        => $"{Stamp}[BattleLoad] seq={seq} t=+{seq * 100}ms phase={phase} {detail}\n";

    private static string RenderWait(int seq, int shaders)
        => Phase(seq, "WaitingForRender", $"waitedMs=2259 shaders={shaders}");

    private static string Freeze(int silent, int sample, bool rglAvailable, int compiles)
        => Stamp + FreezeLog.FormatSample(silent, sample, "battle-load", 806, rglAvailable, compiles, null,
            "gc=1/1/1 heapMB=466", "phase=WaitingForRender seq=40 waitedMs=2259 shaders=806")
           + "\n" + FreezeLog.NoManagedFrames + "\n";

    [TestMethod]
    public void Parse_RenderWaitThenFreezeWithCompiles_ClassifiesCompiling()
    {
        var e = StallEvidence.Parse(Phase(1, "MissionInitialize") + RenderWait(2, 806) + Freeze(40, 3, true, 300) + Freeze(80, 4, true, 812))!;

        Assert.AreEqual(StallEvidenceKind.Compiling, e.Classify());
        Assert.AreEqual("WaitingForRender", e.LastPhase);
        Assert.AreEqual(806, e.LastShaders);
        Assert.AreEqual(80, e.FreezeSilentSeconds);
        Assert.AreEqual(812, e.FreezeRglCompiles);
    }

    [TestMethod]
    public void Parse_RenderWaitThenFreezeWithNoCompilesForEightySeconds_ClassifiesStuck()
    {
        var e = StallEvidence.Parse(Phase(1, "MissionInitialize") + RenderWait(2, 806) + Freeze(80, 4, true, 0))!;

        Assert.AreEqual(StallEvidenceKind.Stuck, e.Classify());
        Assert.AreEqual(80, e.FreezeSilentSeconds);
    }

    [TestMethod]
    public void Parse_RenderWaitWithoutFreezeLines_ClassifiesRenderWaitUnknown()
    {
        var e = StallEvidence.Parse(Phase(1, "MissionInitialize") + RenderWait(2, 806))!;
        Assert.AreEqual(StallEvidenceKind.RenderWaitUnknown, e.Classify());

        var unavailable = StallEvidence.Parse(Phase(1, "MissionInitialize") + RenderWait(2, 806) + Freeze(80, 4, false, 0))!;
        Assert.IsTrue(unavailable.FreezeRglUnavailable);
        Assert.AreEqual(StallEvidenceKind.RenderWaitUnknown, unavailable.Classify());
    }

    [TestMethod]
    public void Parse_LastPhaseAgentEquipBegin_ClassifiesNone()
    {
        var e = StallEvidence.Parse(Phase(1, "MissionInitialize") + RenderWait(2, 806) + Freeze(80, 4, true, 812) + Phase(3, "AgentEquipBegin"))!;

        Assert.AreEqual("AgentEquipBegin", e.LastPhase);
        Assert.AreEqual(StallEvidenceKind.None, e.Classify());
    }

    [TestMethod]
    public void Parse_LinesBeforeTheLastMissionInitialize_AreIgnored()
    {
        var e = StallEvidence.Parse(
            Phase(1, "MissionInitialize") + RenderWait(2, 1569) + Freeze(80, 4, true, 812)
            + Phase(3, "MissionInitialize") + RenderWait(4, 806))!;

        Assert.AreEqual(806, e.LastShaders);
        Assert.IsNull(e.FreezeSilentSeconds);
        Assert.AreEqual(StallEvidenceKind.RenderWaitUnknown, e.Classify());
    }

    [TestMethod]
    public void Parse_NoBattleLoadLine_ReturnsNull()
    {
        Assert.IsNull(StallEvidence.Parse(Stamp + "[MemSample] privMB=1\n"));
        Assert.IsNull(StallEvidence.Parse(""));
    }
}
