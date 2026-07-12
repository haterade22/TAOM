using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
