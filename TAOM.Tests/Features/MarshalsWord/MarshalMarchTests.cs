using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The march (build-spec 6.4): paces that make companies arrive together, its two ends, and the spread the check judges.</summary>
[TestClass]
public class MarshalMarchTests
{
    private static MarchLeg Leg(int company, float remaining, float speed = 4f, float shift = 0f) => new(company, remaining, speed, shift);

    private static float PaceOf(MarchPass pass, int company) => pass.Paces.Single(p => p.Company == company).Pace;

    [TestMethod]
    public void Update_TwoCompaniesWithDifferentWalks_SlowsTheShorterOneInProportionToItsEta()
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 100), Leg(2, 50) });

        Assert.IsFalse(pass.Ended);
        Assert.AreEqual(1f, PaceOf(pass, 1), 1e-4f);
        Assert.AreEqual(0.5f, PaceOf(pass, 2), 1e-4f);
    }

    [TestMethod]
    public void Update_ACompanyThatIsSlowerByNature_SetsTheLongestEta()
    {
        // 80 m at 8 m/s is 10 s; 60 m at 2 m/s is 30 s, so the second company sets the pace for the first.
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 80, 8f), Leg(2, 60, 2f) });

        Assert.AreEqual(1f / 3f, PaceOf(pass, 1), 1e-4f);
        Assert.AreEqual(1f, PaceOf(pass, 2), 1e-4f);
    }

    [TestMethod]
    public void Update_APaceNeverFallsBelowTheFloor()
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 400), Leg(2, 8) });

        Assert.AreEqual(MarshalTuning.MinPace, PaceOf(pass, 2), 1e-6f);
    }

    [TestMethod]
    public void Update_ASpeedBelowOne_IsReadAsOne()
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 20, 0.1f), Leg(2, 20, 1f) });

        Assert.AreEqual(1f, PaceOf(pass, 1), 1e-4f);
        Assert.AreEqual(1f, PaceOf(pass, 2), 1e-4f);
    }

    [TestMethod]
    public void Update_WhenTheLongestEtaIsUnderTwoSeconds_TheMarchEnds()
    {
        var march = new MarshalMarch();

        var pass = march.Update(5, new[] { Leg(1, 7, 4f), Leg(2, 1, 4f) });

        Assert.IsTrue(pass.Ended);
        Assert.AreEqual(MarchEnd.Arrived, pass.Reason);
        Assert.AreEqual(0, pass.Paces.Count, "an ended march publishes no pace");
    }

    [TestMethod]
    public void Update_ACompanyWhoseOrderPointMovedMoreThanAMetre_EndsTheMarchAsReordered()
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 100), Leg(2, 50, shift: 1.5f) });

        Assert.IsTrue(pass.Ended);
        Assert.AreEqual(MarchEnd.Reordered, pass.Reason);
    }

    [TestMethod]
    public void Update_ACompanyThatLeftItsMove_ReportsTheLargestShift_AndTheMarchEndsAsReordered()
    {
        // The adapter reports float.MaxValue for a company that was given Charge, Stop or Retreat, or taken by the AI (they move no order point).
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 100), Leg(2, 50, shift: float.MaxValue) });

        Assert.IsTrue(pass.Ended);
        Assert.AreEqual(MarchEnd.Reordered, pass.Reason, "a finite check that skips the maximum would keep the group slowed for a company that is charging");
        Assert.AreEqual(0, pass.Paces.Count);
    }

    [TestMethod]
    public void Update_AShiftOfExactlyOneMetre_DoesNotEndTheMarch()
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 100), Leg(2, 50, shift: 1f) });

        Assert.IsFalse(pass.Ended);
    }

    [TestMethod]
    public void Update_ReorderedAndArrivedTogether_ReadsAsReordered()
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 1), Leg(2, 1, shift: 5f) });

        Assert.AreEqual(MarchEnd.Reordered, pass.Reason, "a later order is not an arrival, and the check must not judge it");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Update_ANonFiniteRemainingDistance_EndsTheMarchWithNoPace(float remaining)
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, remaining), Leg(2, 50) });

        Assert.IsTrue(pass.Ended);
        Assert.AreEqual(MarchEnd.Invalid, pass.Reason);
        Assert.AreEqual(0, pass.Paces.Count);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Update_ANonFiniteSpeed_EndsTheMarchWithNoPace(float speed)
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 100), Leg(2, 50, speed) });

        Assert.AreEqual(MarchEnd.Invalid, pass.Reason);
        Assert.AreEqual(0, pass.Paces.Count);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Update_ANonFiniteShift_EndsTheMarchWithNoPace(float shift)
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 100), Leg(2, 50, shift: shift) });

        Assert.AreEqual(MarchEnd.Invalid, pass.Reason, "a shift that is not a number must not read as no shift");
        Assert.AreEqual(0, pass.Paces.Count);
    }

    [TestMethod]
    public void Update_AGroupLeftWithOneCompany_EndsAsEmptied()
    {
        // ReadMarch leaves out a company with no units: a march of one has nobody to arrive together with.
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, 100) });

        Assert.IsTrue(pass.Ended);
        Assert.AreEqual(MarchEnd.Emptied, pass.Reason);
        Assert.AreEqual(0, pass.Paces.Count);
    }

    [TestMethod]
    public void Update_NoCompanyLeft_EndsAsEmptied()
    {
        var march = new MarshalMarch();

        Assert.AreEqual(MarchEnd.Emptied, march.Update(0, System.Array.Empty<MarchLeg>()).Reason);
    }

    [TestMethod]
    public void Update_AGroupThatShrinksMidMarch_EndsAsEmptied_AtTheFirstPassWithFewerThanTwo()
    {
        var march = new MarshalMarch();
        Assert.IsFalse(march.Update(0, new[] { Leg(1, 100), Leg(2, 80), Leg(3, 60) }).Ended);
        Assert.IsFalse(march.Update(1, new[] { Leg(1, 96), Leg(3, 56) }).Ended, "two companies still make a march");

        Assert.AreEqual(MarchEnd.Emptied, march.Update(2, new[] { Leg(1, 92) }).Reason);
    }

    [TestMethod]
    public void Update_PastThePatienceOfAStuckCompany_EndsAsPatience_CountedFromTheFirstPass()
    {
        // 100 m at 4 m/s is 25 s on the first pass, so the march gives up after 2.5 * 25 = 62.5 s. A company stuck behind a wall
        // would keep the others slowed for the rest of the battle otherwise.
        var march = new MarshalMarch();
        var legs = new[] { Leg(1, 100), Leg(2, 100) };
        Assert.IsFalse(march.Update(1000, legs).Ended);

        Assert.IsFalse(march.Update(1000 + 62.4, legs).Ended);
        var pass = march.Update(1000 + 62.6, legs);

        Assert.IsTrue(pass.Ended);
        Assert.AreEqual(MarchEnd.Patience, pass.Reason);
        Assert.AreEqual(0, pass.Paces.Count);
    }

    [TestMethod]
    public void Update_AShortMarch_GivesUpAfterTheMinimumPatience()
    {
        // 40 m at 4 m/s is 10 s; 2.5 * 10 = 25 s equals the queue's minimum, and anything shorter gets the minimum.
        var march = new MarshalMarch();
        var legs = new[] { Leg(1, 20), Leg(2, 12) };
        march.Update(0, legs);

        Assert.IsFalse(march.Update(MarshalTuning.MinPatience - 0.5, legs).Ended);
        Assert.AreEqual(MarchEnd.Patience, march.Update(MarshalTuning.MinPatience + 0.5, legs).Reason);
    }

    [TestMethod]
    public void Update_PatienceFollowsTheQueuesFormula_SoAStuckMarchAndAStuckStepGiveUpAlike()
    {
        // 400 m at 4 m/s is 100 s, so 250 s: the same figure the queue gives that walk.
        var march = new MarshalMarch();
        var legs = new[] { Leg(1, 400), Leg(2, 400) };
        march.Update(0, legs);
        var queueGivesUp = MarshalOrderQueue.PatienceFor(400, 4);

        Assert.AreEqual(250.0, queueGivesUp, 1e-9);
        Assert.IsFalse(march.Update(queueGivesUp - 0.1, legs).Ended);
        Assert.AreEqual(MarchEnd.Patience, march.Update(queueGivesUp + 0.1, legs).Reason);
    }

    [TestMethod]
    public void Update_ArrivedAndPastThePatienceInOnePass_ReadsAsArrived()
    {
        // The deadline passing on the pass that finds them in is still an arrival, and the spread check judges it.
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 40), Leg(2, 20) });

        var pass = march.Update(MarshalTuning.MinPatience + 5, new[] { Leg(1, 1), Leg(2, 1) });

        Assert.AreEqual(MarchEnd.Arrived, pass.Reason);
    }

    [TestMethod]
    public void Update_AShiftAtTheDeadline_ReadsAsReordered_NotPatience()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 40), Leg(2, 20) });

        var pass = march.Update(MarshalTuning.MinPatience + 5, new[] { Leg(1, 40), Leg(2, 20, shift: 5f) });

        Assert.AreEqual(MarchEnd.Reordered, pass.Reason);
    }

    [TestMethod]
    public void Update_ANegativeRemainingDistance_IsReadAsZero()
    {
        var march = new MarshalMarch();

        var pass = march.Update(0, new[] { Leg(1, -5), Leg(2, 100) });

        Assert.AreEqual(MarshalTuning.MinPace, PaceOf(pass, 1), 1e-6f);
    }

    [TestMethod]
    public void Update_TheFirstPassTimesToTheSlack_AreKept_AndLaterPassesDoNotReplaceThem()
    {
        var march = new MarshalMarch();

        march.Update(0, new[] { Leg(1, 100), Leg(2, 60) });
        march.Update(4, new[] { Leg(1, 84), Leg(2, 44) });

        Assert.AreEqual(24.25f, march.LongestToSlack, 1e-4f, "97 m to the 3 m slack at 4 m/s");
        Assert.AreEqual(14.25f, march.ShortestToSlack, 1e-4f, "57 m to the slack at 4 m/s");
    }

    [TestMethod]
    public void Update_ACompanyInsideTheSlack_HasNoTimeToTheSlack()
    {
        var march = new MarshalMarch();

        march.Update(0, new[] { Leg(1, 100), Leg(2, 2) });

        Assert.AreEqual(0f, march.ShortestToSlack);
    }

    [TestMethod]
    public void TimesToTheSlack_BeforeAnyPass_AreZero()
    {
        var march = new MarshalMarch();

        Assert.AreEqual(0f, march.LongestToSlack);
        Assert.AreEqual(0f, march.ShortestToSlack);
    }

    [TestMethod]
    public void MembersChanged_BeforeAnyPass_IsFalse()
    {
        Assert.IsFalse(new MarshalMarch().MembersChanged);
    }

    [TestMethod]
    public void MembersChanged_PassesThatAllNameTheSameCompanies_IsFalse()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 100), Leg(2, 80), Leg(3, 60) });
        Assert.IsFalse(march.MembersChanged, "the first pass has nothing to differ from");

        march.Update(1, new[] { Leg(1, 96), Leg(2, 76), Leg(3, 56) });

        Assert.IsFalse(march.MembersChanged);
    }

    [TestMethod]
    public void MembersChanged_ACompanyMissingFromALaterPass_IsTrue()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 100), Leg(2, 80), Leg(3, 60) });

        march.Update(1, new[] { Leg(2, 76), Leg(3, 56) });

        Assert.IsTrue(march.MembersChanged);
    }

    [TestMethod]
    public void MembersChanged_ACompanyThatLeavesAndComesBack_StaysTrue()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 100), Leg(2, 80), Leg(3, 60) });
        march.Update(1, new[] { Leg(2, 76), Leg(3, 56) });

        march.Update(2, new[] { Leg(1, 92), Leg(2, 72), Leg(3, 52) });
        Assert.IsTrue(march.MembersChanged, "a company that came back missed part of the walk");

        march.Update(3, new[] { Leg(1, 88), Leg(2, 68), Leg(3, 48) });

        Assert.IsTrue(march.MembersChanged, "later passes with every company do not undo it");
    }

    [TestMethod]
    public void MembersChanged_OneCompanyTakingAnothersPlace_IsTrue_WhateverTheCount()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 100), Leg(2, 80) });

        march.Update(1, new[] { Leg(2, 76), Leg(3, 56) });

        Assert.IsTrue(march.MembersChanged);
    }

    [TestMethod]
    public void MembersChanged_TheSameCompaniesInAnotherOrder_IsFalse()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 100), Leg(2, 80), Leg(3, 60) });

        march.Update(1, new[] { Leg(3, 56), Leg(1, 96), Leg(2, 76) });

        Assert.IsFalse(march.MembersChanged);
    }

    [TestMethod]
    public void Spread_TwoCompaniesThatReachTheirPointsAtDifferentTimes_IsTheGapBetweenTheFirstAndTheLastArrival()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 40), Leg(2, 100) });
        march.Update(10, new[] { Leg(1, 2.5f), Leg(2, 60) });
        march.Update(20, new[] { Leg(1, 0f), Leg(2, 20) });
        var last = march.Update(30, new[] { Leg(1, 0f), Leg(2, 5f) });

        Assert.IsTrue(last.Ended);
        // Company 1 was first within the slack at t=10 (2.5 m <= 3 m); company 2 is still 5 m out, so it is estimated at 30 + (5 - 3) / 4 s.
        Assert.AreEqual(20.5, march.Spread(), 1e-6);
    }

    [TestMethod]
    public void Spread_NoCompanyHasArrived_UsesTheEstimatedArrivals()
    {
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 8, 4f), Leg(2, 4, 4f) });

        // Estimated: (8 - 3) / 4 = 1.25 s and (4 - 3) / 4 = 0.25 s after t=0.
        Assert.AreEqual(1.0, march.Spread(), 1e-6);
    }

    [TestMethod]
    public void Spread_ACompanyAlreadyAtItsPointOnTheFirstPass_ArrivedAtThatTime()
    {
        var march = new MarshalMarch();
        march.Update(7, new[] { Leg(1, 0f), Leg(2, 12, 4f) });

        // Company 1 at 7; company 2 estimated at 7 + (12 - 3) / 4.
        Assert.AreEqual(2.25, march.Spread(), 1e-6);
    }

    [TestMethod]
    public void Spread_ACompanyEmptiedMidMarch_DoesNotCount()
    {
        // 90, 30 and 27 m at 3 m/s: 30 s, 10 s and 9 s on the first pass, so the allowance is 2 + 7.5 + 0 = 9.5 s. The far company is wiped out
        // at 10 s; its last estimate (5 + 25 = 30 s) must not be measured against the two that arrive at about 16.5 s.
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 90, 3f), Leg(2, 30, 3f), Leg(3, 27, 3f) });
        march.Update(5, new[] { Leg(1, 75, 3f), Leg(2, 25, 3f), Leg(3, 22.5f, 3f) });
        march.Update(10, new[] { Leg(2, 20, 3f), Leg(3, 18, 3f) });

        var last = march.Update(16.5, new[] { Leg(2, 3f, 3f), Leg(3, 2.4f, 3f) });

        Assert.AreEqual(MarchEnd.Arrived, last.Reason);
        Assert.AreEqual(0.0, march.Spread(), 1e-6, "both survivors were within the slack at the same pass");
        Assert.IsTrue(march.MembersChanged, "the first pass's longest and shortest belong to three companies, the spread to two");
    }

    [TestMethod]
    public void Spread_AThirdCompanyWipedOutWhileTheSurvivorsArriveTogether_IsNotTwelveSeconds()
    {
        // The estimate of company 3 (1 s + 48 m / 4 m/s = 13 s) would otherwise stand 12 s from the survivors' 25 s.
        var march = new MarshalMarch();
        march.Update(0, new[] { Leg(1, 100), Leg(2, 100), Leg(3, 50) });
        march.Update(1, new[] { Leg(1, 96), Leg(2, 96), Leg(3, 48) });
        march.Update(2, new[] { Leg(1, 92), Leg(2, 92) });

        var last = march.Update(24, new[] { Leg(1, 4), Leg(2, 4) });

        Assert.AreEqual(MarchEnd.Arrived, last.Reason);
        Assert.AreEqual(0.0, march.Spread(), 1e-6);
    }

    [TestMethod]
    public void SpreadPasses_AGroupThatArrivesWithinTheAllowance_Passes()
    {
        // 60 m and 30 m at 3 m/s: longest 20 s, shortest 10 s, allowance 2 + 5 + max(0, 20 - 10 / 0.3) = 7.
        Assert.AreEqual(7.0, MarshalChecks.SpreadAllowance(longest: 20.0, shortest: 10.0), 1e-9);
        Assert.IsTrue(MarshalChecks.SpreadPasses(spread: 7.0, longest: 20.0, shortest: 10.0));
        Assert.IsFalse(MarshalChecks.SpreadPasses(spread: 7.1, longest: 20.0, shortest: 10.0));
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void SpreadPasses_ANonFiniteSpread_Fails(double spread)
    {
        Assert.IsFalse(MarshalChecks.SpreadPasses(spread, 20.0, 10.0));
    }

    private static double SecondsToTheSlack(double metres, double speed) => (metres - MarshalTuning.ArrivalSlack) / speed;

    [DataTestMethod]
    [DataRow(90.0, 18.0, 3.0, 13.2)]
    [DataRow(80.0, 10.0, 3.0, 18.7)]
    [DataRow(100.0, 20.0, 3.0, 14.3)]
    public void SpreadPasses_AVeryShortWalk_IsAllowedTheTimeThatNoPaceCanRemove(double far, double near, double speed, double spreadWithPace)
    {
        var longest = SecondsToTheSlack(far, speed);
        var shortest = SecondsToTheSlack(near, speed);

        Assert.IsTrue(MarshalChecks.SpreadPasses(spreadWithPace, longest, shortest));
        Assert.IsTrue(spreadWithPace > MarshalTuning.SpreadBaseSeconds + MarshalTuning.SpreadShareOfLongest * longest, "without the floor term this spread would fail");
    }

    [DataTestMethod]
    [DataRow(60.0, 60.0, 3.0, 3.0)]
    [DataRow(60.0, 45.0, 3.0, 3.0)]
    [DataRow(100.0, 76.0, 3.0, 3.0)]
    [DataRow(60.0, 60.0, 3.0, 4.0)]
    [DataRow(80.0, 10.0, 3.0, 3.0)]
    [DataRow(20.0, 48.0, 1.0, 4.0)]
    [DataRow(90.0, 18.0, 3.0, 3.0)]
    [DataRow(100.0, 20.0, 3.0, 3.0)]
    public void SpreadProvesPace_AGroupThatAPacelessGroupWouldAlsoPass_ProvesNothing(double far, double near, double farSpeed, double nearSpeed)
    {
        var longest = Math.Max(SecondsToTheSlack(far, farSpeed), SecondsToTheSlack(near, nearSpeed));
        var shortest = Math.Min(SecondsToTheSlack(far, farSpeed), SecondsToTheSlack(near, nearSpeed));

        Assert.IsFalse(MarshalChecks.SpreadProvesPace(longest, shortest));
    }

    [DataTestMethod]
    [DataRow(100.0, 3.0, 50.0, 3.0)]
    [DataRow(60.0, 3.0, 30.0, 3.0)]
    [DataRow(60.0, 3.0, 20.0, 3.0)]
    [DataRow(120.0, 3.0, 60.0, 3.0)]
    [DataRow(100.0, 5.0, 40.0, 5.0)]
    [DataRow(100.0, 6.0, 40.0, 6.0)]
    public void SpreadProvesPace_WalksAboutTwoToOneOrMore_ProveThePace(double far, double farSpeed, double near, double nearSpeed)
    {
        Assert.IsTrue(MarshalChecks.SpreadProvesPace(SecondsToTheSlack(far, farSpeed), SecondsToTheSlack(near, nearSpeed)));
    }

    [DataTestMethod]
    [DataRow(14.99, 4.0, false)]
    [DataRow(15.0, 4.0, true)]
    public void SpreadProvesPace_ALongestUnderTheFloor_ProvesNothing_WhateverTheGap(double longest, double shortest, bool proves)
    {
        Assert.AreEqual(proves, MarshalChecks.SpreadProvesPace(longest, shortest));
    }

    [TestMethod]
    public void SpreadProvesPace_AGapJustUnderTheMarginTimesTheAllowance_ProvesNothing_AndJustOverProvesIt()
    {
        const double longest = 20.0;
        var margin = MarshalTuning.SpreadProofMargin * MarshalChecks.SpreadAllowance(longest, 10.0);

        Assert.IsFalse(MarshalChecks.SpreadProvesPace(longest, longest - margin + 0.01));
        Assert.IsTrue(MarshalChecks.SpreadProvesPace(longest, longest - margin - 0.01));
    }

    [TestMethod]
    public void SpreadProvesPace_AnUnpacedGroupOfProvableWalks_FailsTheSpread()
    {
        var longest = SecondsToTheSlack(100.0, 3.0);
        var shortest = SecondsToTheSlack(50.0, 3.0);

        Assert.IsTrue(MarshalChecks.SpreadProvesPace(longest, shortest));
        Assert.IsFalse(MarshalChecks.SpreadPasses(longest - shortest, longest, shortest));
    }

    private const double SimulatedPassSeconds = 0.25;

    private static (MarshalMarch March, MarchEnd Reason, double Now) WalkStraightToThePoints(float[] metres, float[] speeds, bool withPace)
    {
        var march = new MarshalMarch();
        var remaining = (float[])metres.Clone();
        var pace = Enumerable.Repeat(1f, metres.Length).ToArray();
        for (var step = 0; step < 4000; step++)
        {
            var now = step * SimulatedPassSeconds;
            var legs = remaining.Select((r, i) => new MarchLeg(i + 1, r, speeds[i], 0f)).ToArray();
            var pass = march.Update(now, legs);
            if (pass.Ended)
                return (march, pass.Reason, now);
            if (withPace)
                foreach (var p in pass.Paces)
                    pace[p.Company - 1] = p.Pace;
            for (var i = 0; i < remaining.Length; i++)
                remaining[i] = Math.Max(0f, remaining[i] - speeds[i] * pace[i] * (float)SimulatedPassSeconds);
        }

        throw new AssertFailedException("the simulated march never ended");
    }

    [DataTestMethod]
    [DataRow(60.0, 3.0, 30.0, 3.0, 19.0, 9.0, 6.75, true, 10.0)]
    [DataRow(60.0, 3.0, 20.0, 3.0, 19.0, 5.67, 6.86, true, 13.3)]
    [DataRow(120.0, 3.0, 60.0, 3.0, 39.0, 19.0, 11.75, true, 20.0)]
    [DataRow(100.0, 5.0, 40.0, 5.0, 19.4, 7.4, 6.85, true, 12.0)]
    [DataRow(100.0, 6.0, 40.0, 6.0, 16.17, 6.17, 6.04, true, 10.0)]
    [DataRow(60.0, 5.0, 6.0, 1.0, 11.4, 3.0, 6.25, false, 0.0)]
    [DataRow(60.0, 4.0, 30.0, 4.0, 14.25, 6.75, 5.56, false, 0.0)]
    [DataRow(73.0, 7.0, 6.0, 1.0, 10.0, 3.0, 4.5, false, 0.0)]
    [DataRow(123.0, 3.0, 81.0, 3.0, 40.0, 26.0, 12.0, false, 0.0)]
    [DataRow(20.0, 1.0, 48.0, 4.0, 17.0, 11.25, 6.25, false, 0.0)]
    [DataRow(80.0, 3.0, 10.0, 3.0, 25.67, 2.33, 26.31, false, 0.0)]
    [DataRow(90.0, 3.0, 18.0, 3.0, 29.0, 5.0, 21.58, false, 0.0)]
    [DataRow(100.0, 3.0, 20.0, 3.0, 32.33, 5.67, 23.53, false, 0.0)]
    [DataRow(30.0, 3.0, 15.0, 3.0, 9.0, 4.0, 4.25, false, 0.0)]
    public void ASimulatedMarch_OnTheseWalks_FailsWithNoPaceAndPassesWithIt_WhereTheRuleSaysTheWalkProvesThePace(
        double farMetres, double farSpeed, double nearMetres, double nearSpeed, double longest, double shortest, double allowance, bool proves, double spreadWithNoPace)
    {
        var metres = new[] { (float)farMetres, (float)nearMetres };
        var speeds = new[] { (float)farSpeed, (float)nearSpeed };

        var none = WalkStraightToThePoints(metres, speeds, withPace: false);
        var paced = WalkStraightToThePoints(metres, speeds, withPace: true);

        Assert.AreEqual(MarchEnd.Arrived, none.Reason);
        Assert.AreEqual(MarchEnd.Arrived, paced.Reason);
        Assert.AreEqual(longest, none.March.LongestToSlack, 0.01, "the first pass's longest time to the slack");
        Assert.AreEqual(shortest, none.March.ShortestToSlack, 0.01, "the first pass's shortest time to the slack");
        Assert.AreEqual(allowance, MarshalChecks.SpreadAllowance(none.March.LongestToSlack, none.March.ShortestToSlack), 0.01);
        Assert.AreEqual(proves, MarshalChecks.SpreadProvesPace(none.March.LongestToSlack, none.March.ShortestToSlack));
        if (!proves)
            return;

        var noPaceSpread = none.March.Spread();
        Assert.AreEqual(spreadWithNoPace, noPaceSpread, SimulatedPassSeconds + 0.1);
        Assert.IsFalse(MarshalChecks.SpreadPasses(noPaceSpread, none.March.LongestToSlack, none.March.ShortestToSlack), "a group with no pace fails where the walk proves the pace");
        Assert.IsTrue(MarshalChecks.SpreadPasses(paced.March.Spread(), paced.March.LongestToSlack, paced.March.ShortestToSlack), "a group with the pace passes");
    }

    [TestMethod]
    public void SpreadProvesPace_ANonFiniteWalk_ProvesNothing()
    {
        Assert.IsFalse(MarshalChecks.SpreadProvesPace(double.NaN, 5.0));
        Assert.IsFalse(MarshalChecks.SpreadProvesPace(20.0, double.NaN));
    }

    [DataTestMethod]
    [DataRow(7, 2, false)]
    [DataRow(8, 2, false)]
    [DataRow(8, 3, true)]
    [DataRow(12, 3, false)]
    [DataRow(12, 4, true)]
    [DataRow(40, 10, false)]
    [DataRow(40, 11, true)]
    public void PatienceTooOften_JudgesOnlyFromEightFinishedSteps_AndFailsAboveOneInFour(int finished, int byPatience, bool fails)
    {
        // 7 finished steps with 2 by patience is not judged yet; 8 with 3 is above one in four.
        Assert.AreEqual(fails, MarshalChecks.PatienceTooOften(finished, byPatience));
    }

    [TestMethod]
    public void PatienceTooOften_ManyPatienceEndsBeforeEightFinishedSteps_IsNotJudged()
    {
        Assert.IsFalse(MarshalChecks.PatienceTooOften(7, 7));
    }
}
