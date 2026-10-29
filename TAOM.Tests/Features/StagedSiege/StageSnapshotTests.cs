using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;
using static TAOM.Tests.Features.StagedSiege.StagedSiegeFixtures;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The immutable view the AI thread, the spawn patch and the HUD read.</summary>
[TestClass]
public class StageSnapshotTests
{
    [TestMethod]
    public void Build_StageC_CarriesZoneSpawnsSupport()
    {
        var token = new object();
        var m = new StageMachine(StagedSide.Defender);
        m.Force(StageId.C);

        var s = StageSnapshot.Build(token, LinePlan(), m, 0.4f, CaptureStatus.Rising);

        Assert.AreSame(token, s.MissionToken);
        Assert.AreEqual(StagedSide.Defender, s.PlayerSide);
        Assert.AreEqual(StageId.C, s.Stage);
        Assert.AreEqual(0b11, s.CapturedMask);
        Assert.AreEqual(0.4f, s.Progress);
        Assert.AreEqual(CaptureStatus.Rising, s.Status);
        Assert.AreEqual(200f, s.Zone.Center.Y);
        Assert.AreEqual(20f, s.DefenderHold!.Value.Position.X);
        Assert.AreEqual(100f, s.AttackerSupport!.Value.Y);
        Assert.AreEqual(100f, s.AttackerSpawn!.Value.Position.Y);
        Assert.AreEqual(20f, s.DefenderSpawn!.Value.Position.X);
        Assert.IsFalse(s.Finished);
    }

    [TestMethod]
    public void Build_StageA_NoHoldNoSupport()
    {
        var s = StageSnapshot.Build(new object(), LinePlan(), new StageMachine(StagedSide.Attacker), 0f, CaptureStatus.Idle);

        Assert.AreEqual(StageId.A, s.Stage);
        Assert.IsNull(s.DefenderHold);
        Assert.IsNull(s.AttackerSupport);
        Assert.IsNull(s.AttackerSpawn);
        Assert.IsNull(s.DefenderSpawn);
    }

    [TestMethod]
    public void Build_Finished_CarriesEnd()
    {
        var m = new StageMachine(StagedSide.Attacker);
        m.Force(null);

        var s = StageSnapshot.Build(new object(), LinePlan(), m, 1f, CaptureStatus.Captured);

        Assert.IsTrue(s.Finished);
        Assert.AreEqual(StagedEnd.DefenderPushedBack, s.End);
        Assert.AreEqual(0b1111, s.CapturedMask);
    }
}
