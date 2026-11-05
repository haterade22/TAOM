using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The judgments the S6 and S7 trial steps pass on (build-spec section 12), with no engine type so each edge is pinned.</summary>
[TestClass]
public class SeatTrialTimeRulesTests
{
    [DataTestMethod]
    [DataRow(0f, true)]
    [DataRow(0.04f, true)]
    [DataRow(0.05f, false)]
    [DataRow(1.2f, false)]
    [DataRow(float.NaN, false)]
    public void PausedMissionStood_NeedsTheClockToMoveUnderFiftyMilliseconds(float missionSeconds, bool stood)
    {
        Assert.AreEqual(stood, SeatTrialRules.PausedMissionStood(missionSeconds));
    }

    [DataTestMethod]
    [DataRow(0.5f, true)]
    [DataRow(0.376f, true)]
    [DataRow(0.624f, true)]
    [DataRow(0.37f, false)]
    [DataRow(0.63f, false)]
    [DataRow(1f, false)]
    [DataRow(float.NaN, false)]
    public void SlowRatio_IsHalfWithinAQuarterOfIt(float ratio, bool matches)
    {
        Assert.AreEqual(matches, SeatTrialRules.SlowRatio(ratio));
    }

    [DataTestMethod]
    [DataRow(2, 0.0166667f, 0.0333333f)]
    [DataRow(4, 0.0166667f, 0.0666667f)]
    [DataRow(4, 0.05f, 0.1f)]
    [DataRow(2, 0.05f, 0.1f)]
    [DataRow(2, 0.2f, 0.1f)]
    [DataRow(4, 0.01f, 0.04f)]
    [DataRow(2, 0f, 0f)]
    [DataRow(2, -0.01f, 0f)]
    [DataRow(2, float.NaN, 0f)]
    [DataRow(2, float.PositiveInfinity, 0f)]
    [DataRow(1, 0.0166667f, 0f)]
    [DataRow(1, 0.05f, 0f)]
    [DataRow(10, 0.0166667f, 0f)]
    [DataRow(10, 0.05f, 0f)]
    public void FixedStepSeconds_IsTheLevelTimesTheFrame_ButNeverMoreThanATenth_AndNoneAtOneOrTen(int level, float frame, float expected)
    {
        Assert.AreEqual(expected, SeatTrialRules.FixedStepSeconds(level, frame), 0.00001f);
    }

    [DataTestMethod]
    [DataRow(2.0f, true)]
    [DataRow(4.0f, true)]
    [DataRow(1.4f, true)]
    [DataRow(1.34f, true)]
    [DataRow(1.32f, false)]
    [DataRow(1.0f, false)]
    [DataRow(0.8f, false)]
    [DataRow(0.5f, true)]
    [DataRow(0.0f, false)]
    [DataRow(float.NaN, false)]
    [DataRow(float.PositiveInfinity, false)]
    public void CanTellFromNormal_NeedsAnExpectedRatioMoreThanAQuarterOfItselfFromOne(float expectedRatio, bool tells)
    {
        Assert.AreEqual(tells, SeatTrialRules.CanTellFromNormal(expectedRatio));
    }

    [DataTestMethod]
    [DataRow(2, 2.0f, 2.0f, true)]
    [DataRow(2, 1.0f, 2.0f, false)]
    [DataRow(2, 2.5f, 2.0f, true)]
    [DataRow(2, 2.6f, 2.0f, false)]
    [DataRow(2, 1.5f, 2.0f, true)]
    [DataRow(2, 1.4f, 2.0f, false)]
    [DataRow(4, 4.0f, 4.0f, true)]
    [DataRow(4, 2.0f, 4.0f, false)]
    [DataRow(4, 2.0f, 2.0f, true)]
    [DataRow(4, 2.4f, 2.4f, true)]
    [DataRow(4, 4.0f, 2.4f, false)]
    [DataRow(2, 2.0f, 0.0f, false)]
    [DataRow(2, 0.0f, 0.0f, false)]
    [DataRow(2, 2.0f, float.NaN, false)]
    [DataRow(2, float.NaN, 2.0f, false)]
    public void FastRatio_MatchesTheExpectedRatioWithinAQuarter(int level, float ratio, float expectedRatio, bool matches)
    {
        Assert.AreEqual(matches, SeatTrialRules.FastRatio(level, ratio, expectedRatio));
    }

    [DataTestMethod]
    [DataRow(10, 5.01f, true)]
    [DataRow(10, 5.0f, false)]
    [DataRow(10, 1.0f, false)]
    [DataRow(10, 9f, true)]
    [DataRow(10, float.NaN, false)]
    public void FastRatio_AtTenIsTheEnginesFastForward_AndNeedsMoreThanFiveWhateverTheExpectedRatio(int level, float ratio, bool matches)
    {
        Assert.AreEqual(matches, SeatTrialRules.FastRatio(level, ratio, 0f));
    }

    [DataTestMethod]
    [DataRow(10f, true)]
    [DataRow(7.51f, true)]
    [DataRow(12.49f, true)]
    [DataRow(7.49f, false)]
    [DataRow(12.51f, false)]
    [DataRow(5f, false)]
    [DataRow(20f, false)]
    [DataRow(40f, false)]
    [DataRow(1f, false)]
    [DataRow(float.NaN, false)]
    [DataRow(float.PositiveInfinity, false)]
    public void AdoptedRatio_IsTheEnginesTenWithinAQuarter_SoARequestOrAStepLeftUnderTheirFlagFails(float ratio, bool adopted)
    {
        Assert.AreEqual(adopted, SeatTrialRules.AdoptedRatio(ratio));
    }

    [DataTestMethod]
    [DataRow(100f, 103.5f, 7f, true)]
    [DataRow(100f, 103.49f, 7f, false)]
    [DataRow(100f, 107f, 7f, true)]
    [DataRow(100f, 101.5f, 3f, true)]
    [DataRow(100f, 101.4f, 3f, false)]
    [DataRow(100f, 100f, 7f, false)]
    [DataRow(100f, 110f, 0f, false)]
    [DataRow(100f, float.NaN, 7f, false)]
    public void LiftSeen_NeedsAtLeastHalfTheHeightAboveTheValueBeforeOpening(float before, float after, float height, bool seen)
    {
        Assert.AreEqual(seen, SeatTrialRules.LiftSeen(before, after, height));
    }

    [DataTestMethod]
    [DataRow(100f, 100f, true)]
    [DataRow(100f, 100.9f, true)]
    [DataRow(100f, 99.1f, true)]
    [DataRow(100f, 101.1f, false)]
    [DataRow(100f, 103.5f, false)]
    [DataRow(100f, float.NaN, false)]
    public void SankBack_NeedsTheViewWithinAMetreOfWhereItWas(float before, float after, bool sank)
    {
        Assert.AreEqual(sank, SeatTrialRules.SankBack(before, after));
    }

    [DataTestMethod]
    [DataRow(0f, 0f, 0f, true)]
    [DataRow(0f, 0f, 0.0001f, false)]
    [DataRow(0.0001f, 0f, 0f, false)]
    [DataRow(0f, -0.0001f, 0f, false)]
    [DataRow(0f, 0f, float.NaN, false)]
    public void ExactlyZero_AcceptsNoSmallLeftover_BecauseTheTargetLockTestsExactZero(float x, float y, float z, bool zero)
    {
        Assert.AreEqual(zero, SeatTrialRules.ExactlyZero(x, y, z));
    }
}
