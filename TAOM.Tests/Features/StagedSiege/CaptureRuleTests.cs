using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The capture arithmetic (docs/features/staged-siege.md): dominance, decay, clamps and the NaN guards.</summary>
[TestClass]
public class CaptureRuleTests
{
    private static readonly StagedSiegeTunables T = StagedSiegeTunables.Default;

    [TestMethod]
    public void Step_ThreeAttackersNoDefenders_RisesOverCaptureSeconds()
    {
        var step = CaptureRule.Step(0f, 3, 0, 1f, T);

        Assert.AreEqual(1f / T.CaptureSeconds, step.Progress, 1e-6f);
        Assert.AreEqual(CaptureStatus.Rising, step.Status);
    }

    [TestMethod]
    public void Step_TwoAttackersNoDefenders_HoldsIdle()
    {
        var step = CaptureRule.Step(0.4f, 2, 0, 1f, T);

        Assert.AreEqual(0.4f, step.Progress);
        Assert.AreEqual(CaptureStatus.Idle, step.Status);
    }

    [TestMethod]
    public void Step_NineAttackersThreeDefenders_Rises()
    {
        var step = CaptureRule.Step(0f, 9, 3, 1f, T);

        Assert.AreEqual(CaptureStatus.Rising, step.Status);
        Assert.IsTrue(step.Progress > 0f);
    }

    [TestMethod]
    public void Step_EightAttackersThreeDefenders_HoldsContested()
    {
        var step = CaptureRule.Step(0.4f, 8, 3, 1f, T);

        Assert.AreEqual(0.4f, step.Progress);
        Assert.AreEqual(CaptureStatus.Contested, step.Status);
    }

    [TestMethod]
    public void Step_OneDefenderTwoAttackers_HoldsContested()
    {
        var step = CaptureRule.Step(0.4f, 2, 1, 1f, T);

        Assert.AreEqual(0.4f, step.Progress);
        Assert.AreEqual(CaptureStatus.Contested, step.Status);
    }

    [TestMethod]
    public void Step_DefendersOnly_FallsOverDecaySeconds()
    {
        var step = CaptureRule.Step(0.5f, 0, 1, 1f, T);

        Assert.AreEqual(0.5f - 1f / T.DecaySeconds, step.Progress, 1e-6f);
        Assert.AreEqual(CaptureStatus.Falling, step.Status);
    }

    [TestMethod]
    public void Step_NobodyPresent_HoldsIdle()
    {
        var step = CaptureRule.Step(0.3f, 0, 0, 1f, T);

        Assert.AreEqual(0.3f, step.Progress);
        Assert.AreEqual(CaptureStatus.Idle, step.Status);
    }

    [TestMethod]
    public void Step_ReachesOne_ReportsCaptured()
    {
        var step = CaptureRule.Step(0.99f, 5, 0, 1f, T);

        Assert.AreEqual(1f, step.Progress);
        Assert.AreEqual(CaptureStatus.Captured, step.Status);
    }

    [TestMethod]
    public void Step_NeverBelowZero()
    {
        var step = CaptureRule.Step(0.01f, 0, 5, 2f, T);

        Assert.AreEqual(0f, step.Progress);
    }

    [TestMethod]
    public void Step_NaNDt_ChangesNothing()
    {
        var step = CaptureRule.Step(0.4f, 9, 0, float.NaN, T);

        Assert.AreEqual(0.4f, step.Progress);
        Assert.AreNotEqual(CaptureStatus.Captured, step.Status);
    }

    [TestMethod]
    public void Step_NegativeDt_ChangesNothing()
    {
        var step = CaptureRule.Step(0.4f, 9, 0, -1f, T);

        Assert.AreEqual(0.4f, step.Progress);
    }

    [TestMethod]
    public void Step_DtAboveCap_IsClamped()
    {
        var step = CaptureRule.Step(0f, 9, 0, 100f, T);

        Assert.AreEqual(2f * T.ReconcileSeconds / T.CaptureSeconds, step.Progress, 1e-6f);
    }

    [TestMethod]
    public void Step_NaNProgress_RestartsFromZero()
    {
        var step = CaptureRule.Step(float.NaN, 9, 0, 1f, T);

        Assert.AreEqual(1f / T.CaptureSeconds, step.Progress, 1e-6f);
    }
}
