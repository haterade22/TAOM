using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// What the mission's clock does each frame at each speed. 10x is the engine's fast forward unless a person drives a live main agent: native
/// IMBMission.Tick calls Mission_OnPreTick on every call, so fast forward would read one frame's key presses once per step.
/// </summary>
[TestClass]
public class LongDefeatSpeedFrameTests
{
    private const float RealFrame = 0.016f;

    [TestMethod]
    public void TenTimes_WithAPersonDriving_IsAFixedStepCappedAtATenthOfASecond_NotFastForward()
    {
        var (fastForward, step) = LongDefeatSpeedFrame.For(10, RealFrame, personDriving: true);

        Assert.IsFalse(fastForward);
        Assert.AreEqual(0.1f, step, 1e-6f, "0.16 s of mission time in one step is over the cap");
    }

    [TestMethod]
    public void TenTimes_WithAPersonDriving_AtAHundredFpsOrMore_IsTrueTenTimes()
    {
        Assert.AreEqual(0.1f, LongDefeatSpeedFrame.For(10, 0.01f, true).FixedStep, 1e-6f);
        Assert.AreEqual(0.05f, LongDefeatSpeedFrame.For(10, 0.005f, true).FixedStep, 1e-6f);
        Assert.AreEqual(0.06f, LongDefeatSpeedFrame.For(10, 0.006f, true).FixedStep, 1e-6f);
    }

    [TestMethod]
    public void TenTimes_WithNoPersonDriving_IsTheEnginesFastForward()
    {
        var (fastForward, step) = LongDefeatSpeedFrame.For(10, RealFrame, personDriving: false);

        Assert.IsTrue(fastForward, "a dead player, or an AI-driven main agent in an unattended run");
        Assert.AreEqual(0f, step);
    }

    [TestMethod]
    public void TenTimes_WithAPersonDriving_AndNoFrameTime_HasNoStep()
    {
        Assert.AreEqual((false, 0f), LongDefeatSpeedFrame.For(10, 0f, true));
        Assert.AreEqual((false, 0f), LongDefeatSpeedFrame.For(10, -1f, true));
    }

    [TestMethod]
    public void OneTwoAndFour_AreUnchanged_WhoeverDrives()
    {
        foreach (var driving in new[] { true, false })
        {
            Assert.AreEqual((false, 0f), LongDefeatSpeedFrame.For(1, RealFrame, driving));
            Assert.AreEqual((false, 0.032f), LongDefeatSpeedFrame.For(2, RealFrame, driving));
            Assert.AreEqual((false, 0.064f), LongDefeatSpeedFrame.For(4, RealFrame, driving));
            Assert.AreEqual((false, 0.1f), LongDefeatSpeedFrame.For(4, 0.05f, driving), "capped");
        }
    }
}
