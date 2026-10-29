using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The defender detachment at point A: its size, its slots on the ground and its charge-or-hold decision.</summary>
[TestClass]
public class DetachmentRuleTests
{
    [TestMethod]
    public void Size_QuarterOfInfantry_Floors()
    {
        Assert.AreEqual(25, DetachmentRule.Size(100, 0.25f, 60));
        Assert.AreEqual(1, DetachmentRule.Size(7, 0.25f, 60));
    }

    [TestMethod]
    public void Size_CappedAtCap()
    {
        Assert.AreEqual(60, DetachmentRule.Size(1000, 0.25f, 60));
    }

    [TestMethod]
    public void Size_ZeroInfantry_Zero()
    {
        Assert.AreEqual(0, DetachmentRule.Size(0, 0.25f, 60));
    }

    [TestMethod]
    public void Size_NaNShare_Zero()
    {
        Assert.AreEqual(0, DetachmentRule.Size(100, float.NaN, 60));
        Assert.AreEqual(0, DetachmentRule.Size(100, float.PositiveInfinity, 60));
    }

    [TestMethod]
    public void Decide_AttackerWithinTwoRadii_Charges()
    {
        Assert.AreEqual(DetachmentOrder.Charge, DetachmentRule.Decide(DetachmentOrder.Move, 1, 1));
    }

    [TestMethod]
    public void Decide_ChargingNoneWithinThreeRadii_Moves()
    {
        Assert.AreEqual(DetachmentOrder.Move, DetachmentRule.Decide(DetachmentOrder.Charge, 0, 0));
    }

    [TestMethod]
    public void Decide_ChargingAttackerBetweenTwoAndThree_KeepsCharging()
    {
        Assert.AreEqual(DetachmentOrder.Charge, DetachmentRule.Decide(DetachmentOrder.Charge, 0, 2));
    }

    [TestMethod]
    public void Decide_MovingNoneWithinTwoRadii_KeepsMoving()
    {
        Assert.AreEqual(DetachmentOrder.Move, DetachmentRule.Decide(DetachmentOrder.Move, 0, 4));
    }

    [TestMethod]
    public void Slots_TenPerRowCentredPerpendicular()
    {
        // Facing +Y: the row runs along X, rows step back (toward -Y).
        var frame = new StageFrame(new StagePoint(100f, 200f, 5f), new StageFacing(0f, 1f));

        var slots = DetachmentRule.Slots(frame, 12);

        Assert.AreEqual(12, slots.Count);
        Assert.AreEqual(100f - 4.5f * 1.5f, slots[0].X, 1e-4f);
        Assert.AreEqual(100f + 4.5f * 1.5f, slots[9].X, 1e-4f);
        for (var i = 0; i < 10; i++)
        {
            Assert.AreEqual(200f, slots[i].Y, 1e-4f);
            Assert.AreEqual(5f, slots[i].Z);
        }

        // The second row holds two units, centred on the frame and 1.5 m behind.
        Assert.AreEqual(100f - 0.75f, slots[10].X, 1e-4f);
        Assert.AreEqual(100f + 0.75f, slots[11].X, 1e-4f);
        Assert.AreEqual(200f - 1.5f, slots[10].Y, 1e-4f);
        Assert.AreEqual(1.5f, Math.Abs(slots[1].X - slots[0].X), 1e-4f);
    }

    [TestMethod]
    public void Slots_Zero_Empty()
    {
        var frame = new StageFrame(new StagePoint(0f, 0f, 0f), new StageFacing(0f, 1f));

        Assert.AreEqual(0, DetachmentRule.Slots(frame, 0).Count);
        Assert.AreEqual(0, DetachmentRule.Slots(frame, -3).Count);
    }
}
