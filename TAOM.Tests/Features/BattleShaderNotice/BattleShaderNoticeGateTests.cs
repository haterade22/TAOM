using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleShaderNotice;

namespace TAOM.Tests.Features.BattleShaderNotice;

[TestClass]
public class BattleShaderNoticeGateTests
{
    [TestMethod]
    public void Observe_WaitingWithManyShadersQueued_Starts()
    {
        var gate = new BattleShaderNoticeGate();

        Assert.AreEqual(BattleShaderNoticeAction.Start, gate.Observe(waitingForRender: true, queuedShaders: 1192));
        Assert.IsTrue(gate.Shown);
    }

    [TestMethod]
    public void Observe_WaitingWithExactlyTheThreshold_Starts()
    {
        var gate = new BattleShaderNoticeGate();

        Assert.AreEqual(BattleShaderNoticeAction.Start, gate.Observe(true, 50));
    }

    [TestMethod]
    public void Observe_WaitingWithOneBelowTheThreshold_DoesNothing()
    {
        var gate = new BattleShaderNoticeGate();

        Assert.AreEqual(BattleShaderNoticeAction.None, gate.Observe(true, 49));
        Assert.IsFalse(gate.Shown);
    }

    [TestMethod]
    public void Observe_WaitingWithAWarmCache_DoesNothing()
    {
        var gate = new BattleShaderNoticeGate();

        Assert.AreEqual(BattleShaderNoticeAction.None, gate.Observe(true, 0));
        Assert.IsFalse(gate.Shown);
    }

    [TestMethod]
    public void Observe_WaitingWithAnUnreadableCount_DoesNothing()
    {
        var gate = new BattleShaderNoticeGate();

        Assert.AreEqual(BattleShaderNoticeAction.None, gate.Observe(true, -1));
        Assert.IsFalse(gate.Shown);
    }

    [TestMethod]
    public void Observe_CountRisesPastTheThresholdOnALaterFrame_StartsThen()
    {
        var gate = new BattleShaderNoticeGate();
        gate.Observe(true, 10);

        Assert.AreEqual(BattleShaderNoticeAction.Start, gate.Observe(true, 400));
    }

    [TestMethod]
    public void Observe_SecondWaitingFrameAfterStart_DoesNotStartAgain()
    {
        var gate = new BattleShaderNoticeGate();
        gate.Observe(true, 1192);

        Assert.AreEqual(BattleShaderNoticeAction.None, gate.Observe(true, 1166));
        Assert.IsTrue(gate.Shown);
    }

    [TestMethod]
    public void Observe_NotWaitingAndNothingShown_DoesNothing()
    {
        var gate = new BattleShaderNoticeGate();

        Assert.AreEqual(BattleShaderNoticeAction.None, gate.Observe(false, 1192));
        Assert.IsFalse(gate.Shown);
    }

    [TestMethod]
    public void Observe_WaitEndsWhileShown_Stops()
    {
        var gate = new BattleShaderNoticeGate();
        gate.Observe(true, 1192);

        Assert.AreEqual(BattleShaderNoticeAction.Stop, gate.Observe(false, -1));
        Assert.IsFalse(gate.Shown);
    }

    [TestMethod]
    public void Observe_FrameAfterTheStop_DoesNothing()
    {
        var gate = new BattleShaderNoticeGate();
        gate.Observe(true, 1192);
        gate.Observe(false, -1);

        Assert.AreEqual(BattleShaderNoticeAction.None, gate.Observe(false, -1));
    }

    [TestMethod]
    public void Observe_NextBattleLoadAfterAStop_StartsAgain()
    {
        var gate = new BattleShaderNoticeGate();
        gate.Observe(true, 1192);
        gate.Observe(false, -1);

        Assert.AreEqual(BattleShaderNoticeAction.Start, gate.Observe(true, 714));
    }
}
