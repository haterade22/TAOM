using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The A to D progression: gates open when B falls, D ends the siege for whichever side the player is on.</summary>
[TestClass]
public class StageMachineTests
{
    private static StageMachine At(StageId target, StagedSide player = StagedSide.Attacker)
    {
        var m = new StageMachine(player);
        while (m.Current != target) m.Capture();
        return m;
    }

    [TestMethod]
    public void New_StartsAtAWithNothingCaptured()
    {
        var m = new StageMachine(StagedSide.Attacker);

        Assert.AreEqual(StageId.A, m.Current);
        Assert.AreEqual(0, m.CapturedMask);
        Assert.IsFalse(m.GatesOpened);
        Assert.AreEqual(StagedEnd.None, m.End);
    }

    [TestMethod]
    public void Capture_A_MovesToB_GatesStayShut()
    {
        var m = new StageMachine(StagedSide.Attacker);

        var t = m.Capture();

        Assert.IsTrue(t.Changed);
        Assert.AreEqual(StageId.A, t.From);
        Assert.AreEqual(StageId.B, t.To);
        Assert.IsFalse(t.OpenGates);
        Assert.AreEqual(StageId.B, m.Current);
        Assert.AreEqual(1, m.CapturedMask);
    }

    [TestMethod]
    public void Capture_B_MovesToC_OpensGates()
    {
        var m = At(StageId.B);

        var t = m.Capture();

        Assert.AreEqual(StageId.C, t.To);
        Assert.IsTrue(t.OpenGates);
        Assert.IsTrue(m.GatesOpened);
    }

    [TestMethod]
    public void Capture_C_MovesToD()
    {
        var m = At(StageId.C);

        var t = m.Capture();

        Assert.AreEqual(StageId.D, t.To);
        Assert.IsFalse(t.OpenGates);
    }

    [TestMethod]
    public void Capture_D_PlayerAttacks_EndsDefenderPushedBack()
    {
        var t = At(StageId.D, StagedSide.Attacker).Capture();

        Assert.IsNull(t.To);
        Assert.AreEqual(StagedEnd.DefenderPushedBack, t.End);
    }

    [TestMethod]
    public void Capture_D_PlayerDefends_EndsPlayerDefeated()
    {
        var m = At(StageId.D, StagedSide.Defender);

        var t = m.Capture();

        Assert.AreEqual(StagedEnd.PlayerDefeated, t.End);
        Assert.AreEqual(StagedEnd.PlayerDefeated, m.End);
    }

    [TestMethod]
    public void Capture_AfterEnd_ChangesNothing()
    {
        var m = At(StageId.D);
        m.Capture();
        var mask = m.CapturedMask;

        var t = m.Capture();

        Assert.IsFalse(t.Changed);
        Assert.AreEqual(mask, m.CapturedMask);
        Assert.IsFalse(m.Force(null).Changed);
    }

    [TestMethod]
    public void Capture_Mask_OnlyGrows()
    {
        var m = new StageMachine(StagedSide.Attacker);
        var previous = 0;
        for (var i = 0; i < 6; i++)
        {
            m.Capture();
            Assert.AreEqual(previous, m.CapturedMask & previous);
            previous = m.CapturedMask;
        }

        Assert.AreEqual(0b1111, m.CapturedMask);
    }

    [TestMethod]
    public void Force_CFromA_CapturesAAndB_OpensGates()
    {
        var m = new StageMachine(StagedSide.Attacker);

        var t = m.Force(StageId.C);

        Assert.IsTrue(t.Changed);
        Assert.IsTrue(t.OpenGates);
        Assert.AreEqual(StageId.A, t.From);
        Assert.AreEqual(StageId.C, t.To);
        Assert.AreEqual(0b11, m.CapturedMask);
        Assert.AreEqual(StageId.C, m.Current);
    }

    [TestMethod]
    public void Force_BFromA_GatesStayShut()
    {
        var m = new StageMachine(StagedSide.Attacker);

        var t = m.Force(StageId.B);

        Assert.IsTrue(t.Changed);
        Assert.IsFalse(t.OpenGates);
        Assert.AreEqual(StageId.B, m.Current);
    }

    [TestMethod]
    public void Force_Null_EndsAsCaptureOfD()
    {
        var m = new StageMachine(StagedSide.Defender);

        var t = m.Force(null);

        Assert.IsTrue(t.Changed);
        Assert.IsTrue(t.OpenGates);
        Assert.IsNull(t.To);
        Assert.AreEqual(StagedEnd.PlayerDefeated, t.End);
        Assert.AreEqual(0b1111, m.CapturedMask);
    }

    [TestMethod]
    public void Force_Backward_IsRefused()
    {
        var m = At(StageId.C);

        var t = m.Force(StageId.A);

        Assert.IsFalse(t.Changed);
        Assert.AreEqual(StageId.C, m.Current);
    }

    [TestMethod]
    public void Force_SameStage_IsNoOp()
    {
        var m = At(StageId.B);
        var mask = m.CapturedMask;

        var t = m.Force(StageId.B);

        Assert.IsFalse(t.Changed);
        Assert.AreEqual(mask, m.CapturedMask);
    }
}
