using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

// The 2026-07-11 rework moves the "elite tax" from inflating the member COUNT to deflating the party-size
// LIMIT, so counts read raw everywhere. ComputeSizePenalty is the pure clamp behind that deflation:
// subtract the weight surplus (weighted − raw) from the limit, floored so the limit never drops below 1.
[TestClass]
public class SizePenaltyTests
{
    [TestMethod]
    public void ComputeSizePenalty_LightParty_NoPenalty()
        => Assert.AreEqual(0, TroopWeightService.ComputeSizePenalty(rawCount: 100, weightedCount: 100, baseLimit: 300));

    [TestMethod]
    public void ComputeSizePenalty_EliteParty_SubtractsWeightSurplus()
    {
        // 159 raw / 325 weighted (the reported case): surplus 166 subtracted -> effective limit 300-166=134,
        // so raw 159 > 134 = "over cap", matching weighted 325 > 300.
        Assert.AreEqual(166, TroopWeightService.ComputeSizePenalty(rawCount: 159, weightedCount: 325, baseLimit: 300));
    }

    [TestMethod]
    public void ComputeSizePenalty_ExtremeWeight_ClampsSoLimitStaysAtLeastOne()
    {
        // 60 raw / 180 weighted, base 100: raw surplus 120 would drop the limit below 0; clamp to base-1=99.
        Assert.AreEqual(99, TroopWeightService.ComputeSizePenalty(rawCount: 60, weightedCount: 180, baseLimit: 100));
    }

    [TestMethod]
    public void ComputeSizePenalty_EmptyParty_NoPenalty()
        => Assert.AreEqual(0, TroopWeightService.ComputeSizePenalty(rawCount: 0, weightedCount: 0, baseLimit: 300));

    [TestMethod]
    public void ComputeSizePenalty_BaseLimitOne_CannotReduce()
        => Assert.AreEqual(0, TroopWeightService.ComputeSizePenalty(rawCount: 5, weightedCount: 12, baseLimit: 1));

    [TestMethod]
    public void ComputeSizePenalty_ExactBoundary_LeavesOneSlot()
        // penalty 50 vs base 50 -> clamp to 49 so the limit floors at 1, not 0.
        => Assert.AreEqual(49, TroopWeightService.ComputeSizePenalty(rawCount: 10, weightedCount: 60, baseLimit: 50));

    // `(int)float.NaN` and `(int)float.PositiveInfinity` both == int.MinValue on net472/x64, and
    // `int.MinValue - 1` UNDERFLOWS (unchecked) to int.MaxValue -- which silently defeats the
    // `maxReducible <= 0` guard that exists to reject a degenerate baseLimit. A poisoned baseLimit must
    // yield NO penalty, never a plausible-looking one. (deep-review 2026-07-17, data-flow MED.)
    [TestMethod]
    public void ComputeSizePenalty_IntMinValueBaseLimit_UnderflowCannotManufactureAPenalty()
        => Assert.AreEqual(0, TroopWeightService.ComputeSizePenalty(rawCount: 10, weightedCount: 200, baseLimit: int.MinValue));

    [TestMethod]
    public void ComputeSizePenalty_NegativeBaseLimit_NoPenalty()
        => Assert.AreEqual(0, TroopWeightService.ComputeSizePenalty(rawCount: 10, weightedCount: 200, baseLimit: -50));
}

// ExplainedNumber.Add lands in the BASE frame (BaseNumber += v) and the result is
// BaseNumber * (1 + SumOfFactors) -- so an Add is ALWAYS scaled by the accumulated factors, whatever the
// call order. ComputeSizePenalty works in the RESULT frame (it is an absolute body count derived from
// limit.ResultNumber), so subtracting it raw over-deflated every factor-boosted culture by
// penalty * SumOfFactors. SubtractResultFramePenalty divides the factor back out.
[TestClass]
public class ResultFramePenaltyTests
{
    private const float Tolerance = 0.01f;

    [TestMethod]
    public void SubtractResultFramePenalty_NoFactors_SubtractsPenaltyExactly()
    {
        var limit = new ExplainedNumber(100f);

        TroopWeightService.SubtractResultFramePenalty(ref limit, 9);

        Assert.AreEqual(91f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void SubtractResultFramePenalty_FactorBoostedCulture_PenaltyIsNotAmplified()
    {
        // Mordor-style +25% party size: base 100 -> ResultNumber 125. A 9-man weight surplus must cost
        // exactly 9 slots (-> 116). The pre-fix code did limit.Add(-9), yielding 91 * 1.25 = 113.75 -> 113.
        var limit = new ExplainedNumber(100f);
        limit.AddFactor(0.25f);
        Assert.AreEqual(125f, limit.ResultNumber, Tolerance, "precondition: the factor frame is as expected");

        TroopWeightService.SubtractResultFramePenalty(ref limit, 9);

        Assert.AreEqual(116f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void SubtractResultFramePenalty_ClampedPenaltyUnderFactor_LimitStaysAtLeastOne()
    {
        // The documented ">= 1" floor: ComputeSizePenalty clamps to baseLimit-1 in the RESULT frame, so the
        // conversion must land the result on exactly 1 -- pre-fix this went to 0.8*1.25 in the wrong
        // direction and could drive a factor-boosted culture's limit to <= 0.
        var limit = new ExplainedNumber(100f);
        limit.AddFactor(0.25f);
        int penalty = TroopWeightService.ComputeSizePenalty(rawCount: 10, weightedCount: 200, baseLimit: (int)limit.ResultNumber);
        Assert.AreEqual(124, penalty, "precondition: penalty clamps to baseLimit-1");

        TroopWeightService.SubtractResultFramePenalty(ref limit, penalty);

        Assert.AreEqual(1f, limit.ResultNumber, Tolerance);
        Assert.IsTrue(limit.ResultNumber >= 1f, "the limit must never drop below 1");
    }

    [TestMethod]
    public void SubtractResultFramePenalty_ZeroPenalty_LeavesLimitUntouched()
    {
        var limit = new ExplainedNumber(100f);
        limit.AddFactor(0.25f);

        TroopWeightService.SubtractResultFramePenalty(ref limit, 0);

        Assert.AreEqual(125f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void SubtractResultFramePenalty_NaNFactor_SkipsPenaltyRatherThanPoisoningTheLimit()
    {
        // Engine-float gate rule: NaN must FAIL the gate, not sail through it. A NaN scale would make the
        // converted penalty NaN and poison the whole party-size limit.
        var limit = new ExplainedNumber(100f);
        limit.AddFactor(float.NaN);

        TroopWeightService.SubtractResultFramePenalty(ref limit, 9);

        Assert.IsFalse(float.IsNaN(limit.BaseNumber), "a NaN factor must not poison BaseNumber");
    }

    [TestMethod]
    public void SubtractResultFramePenalty_FactorsCancelTheLimitToZero_SkipsPenalty()
    {
        // SumOfFactors == -1 => scale 0 => division by zero. The limit is already degenerate; skip.
        var limit = new ExplainedNumber(100f);
        limit.AddFactor(-1f);

        TroopWeightService.SubtractResultFramePenalty(ref limit, 9);

        Assert.AreEqual(100f, limit.BaseNumber, Tolerance, "BaseNumber must be untouched when the scale is degenerate");
        Assert.IsFalse(float.IsInfinity(limit.BaseNumber));
    }

    [TestMethod]
    public void SubtractResultFramePenalty_NegativeScale_SkipsPenalty()
    {
        // SumOfFactors < -1 => scale < 0 => dividing would ADD capacity instead of removing it.
        var limit = new ExplainedNumber(100f);
        limit.AddFactor(-1.5f);

        TroopWeightService.SubtractResultFramePenalty(ref limit, 9);

        Assert.AreEqual(100f, limit.BaseNumber, Tolerance);
    }
}
