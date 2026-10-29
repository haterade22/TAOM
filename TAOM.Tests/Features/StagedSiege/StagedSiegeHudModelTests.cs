using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;
using TAOM.Features.StagedSiege.UI;
using static TAOM.Tests.Features.StagedSiege.StagedSiegeFixtures;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The pure reading behind the staged siege HUD.</summary>
[TestClass]
public class StagedSiegeHudModelTests
{
    private static StageSnapshot Snap(StagedSide side, StageId? force, float progress = 0f, CaptureStatus status = CaptureStatus.Idle,
        bool finish = false)
    {
        var m = new StageMachine(side);
        if (finish)
            m.Force(null);
        else if (force.HasValue)
            m.Force(force);
        return StageSnapshot.Build(new object(), LinePlan(), m, progress, status);
    }

    [TestMethod]
    public void From_AttackerB_TakeB()
    {
        var model = StagedSiegeHudModel.From(Snap(StagedSide.Attacker, StageId.B, 0.5f, CaptureStatus.Rising));

        Assert.AreEqual(HudObjectiveKind.Take, model.ObjectiveKind);
        Assert.AreEqual('B', model.PointLetter);
        Assert.AreEqual(CaptureStatus.Rising, model.CaptureStatus);
    }

    [TestMethod]
    public void From_DefenderC_HoldC()
    {
        var model = StagedSiegeHudModel.From(Snap(StagedSide.Defender, StageId.C));

        Assert.AreEqual(HudObjectiveKind.Hold, model.ObjectiveKind);
        Assert.AreEqual('C', model.PointLetter);
    }

    [TestMethod]
    public void From_FinishedAttacker_Hall() =>
        Assert.AreEqual(HudObjectiveKind.Hall, StagedSiegeHudModel.From(Snap(StagedSide.Attacker, null, finish: true)).ObjectiveKind);

    [TestMethod]
    public void From_FinishedDefender_Fallen() =>
        Assert.AreEqual(HudObjectiveKind.Fallen, StagedSiegeHudModel.From(Snap(StagedSide.Defender, null, finish: true)).ObjectiveKind);

    [TestMethod]
    public void From_ProgressNaN_Zero() =>
        Assert.AreEqual(0, StagedSiegeHudModel.From(Snap(StagedSide.Attacker, null, float.NaN)).ProgressPercent);

    [TestMethod]
    public void From_Progress042_42() =>
        Assert.AreEqual(42, StagedSiegeHudModel.From(Snap(StagedSide.Attacker, null, 0.42f)).ProgressPercent);

    [TestMethod]
    public void From_ProgressAboveOne_100() =>
        Assert.AreEqual(100, StagedSiegeHudModel.From(Snap(StagedSide.Attacker, null, 3f)).ProgressPercent);

    [TestMethod]
    public void Strip_Attacker_HasEMarksCaptured() =>
        Assert.AreEqual("A+ B+ [C] D E", StagedSiegeHudModel.From(Snap(StagedSide.Attacker, StageId.C)).Strip);

    [TestMethod]
    public void Strip_Defender_NoE() =>
        Assert.AreEqual("A+ B+ [C] D", StagedSiegeHudModel.From(Snap(StagedSide.Defender, StageId.C)).Strip);
}
