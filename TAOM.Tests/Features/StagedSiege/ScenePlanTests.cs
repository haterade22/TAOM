using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;
using static TAOM.Tests.Features.StagedSiege.StagedSiegeFixtures;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>Where reinforcements appear: attackers at the previous point facing the next, defenders at the recorded frames.</summary>
[TestClass]
public class ScenePlanTests
{
    [TestMethod]
    public void AttackerSpawn_A_Null()
    {
        Assert.IsNull(LinePlan().AttackerSpawn(StageId.A));
    }

    [TestMethod]
    public void AttackerSpawn_C_IsBCentreFacingC()
    {
        var frame = LinePlan().AttackerSpawn(StageId.C)!.Value;

        Assert.AreEqual(0f, frame.Position.X);
        Assert.AreEqual(100f, frame.Position.Y);
        Assert.AreEqual(10f, frame.Position.Z);
        Assert.AreEqual(0f, frame.Facing.X, 1e-5f);
        Assert.AreEqual(1f, frame.Facing.Y, 1e-5f);
    }

    [TestMethod]
    public void AttackerSpawn_CoincidentPoints_DefaultFacing()
    {
        var plan = new ScenePlan("same",
            new[] { Zone(StageId.A, 5f, 5f), Zone(StageId.B, 5f, 5f), Zone(StageId.C, 50f, 5f), Zone(StageId.D, 90f, 5f) },
            Frame(0f, 0f, 0f, 0f, 1f), Frame(0f, 0f, 0f, 0f, 1f), Frame(0f, 0f, 0f, 0f, 1f), Frame(0f, 0f, 0f, 0f, 1f));

        var frame = plan.AttackerSpawn(StageId.B)!.Value;

        Assert.AreEqual(0f, frame.Facing.X);
        Assert.AreEqual(1f, frame.Facing.Y);
    }

    [TestMethod]
    public void DefenderSpawn_A_Null_BToDRecorded()
    {
        var plan = LinePlan();

        Assert.IsNull(plan.DefenderSpawn(StageId.A));
        Assert.IsNull(plan.DefenderFrame(StageId.A));
        Assert.AreEqual(110f, plan.DefenderSpawn(StageId.B)!.Value.Position.Y);
        Assert.AreEqual(20f, plan.DefenderSpawn(StageId.C)!.Value.Position.X);
        Assert.AreEqual(13f, plan.DefenderSpawn(StageId.D)!.Value.Position.Z);
    }

    [TestMethod]
    public void Detachment_AndZones_AreKept()
    {
        var plan = LinePlan("keep");

        Assert.AreEqual("keep", plan.Scene);
        Assert.AreEqual(2f, plan.Detachment.Position.Y);
        Assert.AreEqual(StageId.C, plan.Zone(StageId.C).Id);
        Assert.AreEqual(200f, plan.Zone(StageId.C).Center.Y);
    }
}
