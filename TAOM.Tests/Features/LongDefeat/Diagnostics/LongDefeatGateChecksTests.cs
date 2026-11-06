using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The gate verdicts of #1079 (phase3-plan.md section 6): <c>gate-level</c> (the levelled maximum, the share of hit points kept, the state index not falling, E9b), <c>gate-repair</c> (the vanilla reset
/// worked: standing, enabled, closed, whole, state 0, the hidden gate still down) and <c>gate-attacked</c> (the repaired gate took blows, and nobody came through it). Pure: the director reads the field.
/// </summary>
[TestClass]
public class LongDefeatGateChecksTests
{
    private const float Vanilla = 15000f;

    private static LdGateFacts Facts(float hp, float max, bool destroyed = false, bool open = false, float vanilla = Vanilla) =>
        new(GateMath.StateIndex(hp, max, 4), hp, max, vanilla, destroyed, open);

    private static LdGateChange Change(LdGateFacts before, LdGateFacts after, bool disabled = false, bool closed = true, int near = 0, bool touched = true) =>
        new(before, after, disabled, closed, near, touched);

    private static LdGateWrite Write(params LdGateChange[] gates) => new(gates, 1, 0, true);

    // ---- gate-level ----

    /// <summary>A level bought on a gate that had lost a share: what the adapter writes through <see cref="GateMath.KeepShare"/>.</summary>
    private static LdGateChange Levelled(float hp, float oldMax, float newMax)
    {
        var after = GateMath.KeepShare(hp, oldMax, newMax);
        return Change(Facts(hp, oldMax, hp <= 0f), Facts(after, newMax, hp <= 0f));
    }

    [TestMethod]
    public void GateLevel_AStandingGateThatKeptItsShareAtTheLevelledMaximum_Passes()
    {
        var verdict = LongDefeatGateChecks.GateLevel(1, 1.5f, Write(Levelled(11000f, 15000f, 22500f)));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        Assert.AreEqual("l=1 factor=1.50 gates=1 max=22500.0/22500.0 hp=11000.0->16499.0 share=0.733->0.733 index=1->1", verdict.Detail);
    }

    [TestMethod]
    public void GateLevel_ABrokenGate_RaisesOnlyItsMaximum_AndPasses()
    {
        var verdict = LongDefeatGateChecks.GateLevel(2, 2f, Write(Levelled(0f, 15000f, 30000f)));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "hp=0.0->0.0");
    }

    [TestMethod]
    public void GateLevel_AMaximumOffTheLevelBy1Percent_Fails_AndWithin1PercentPasses()
    {
        var off = Change(Facts(15000f, 15000f), Facts(15000f, 22500f * 0.985f));
        var near = Change(Facts(15000f, 15000f), Facts(22500f * 0.995f - 1f, 22500f * 0.995f));

        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateLevel(1, 1.5f, Write(off)).Result);
        Assert.AreEqual(CheckResult.Pass, LongDefeatGateChecks.GateLevel(1, 1.5f, Write(near)).Result);
    }

    [TestMethod]
    public void GateLevel_AShareHealedOrLostByTheWrite_Fails()
    {
        var healed = Change(Facts(11000f, 15000f), Facts(22500f, 22500f));
        var lost = Change(Facts(11000f, 15000f), Facts(10000f, 22500f));

        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateLevel(1, 1.5f, Write(healed)).Result, "a level never heals the missing share");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateLevel(1, 1.5f, Write(lost)).Result);
    }

    [TestMethod]
    public void GateLevel_AStateIndexThatFell_Fails_TheNextHitWouldReadMinusOne()
    {
        // E9b: 11000 of 15000 is state 1; plus 7500 each (18500 of 22500) the next hit computes 0 and throws. The write keeps the share, so the index cannot fall; a write that added the same hit points would.
        var added = Change(Facts(11000f, 15000f), Facts(18500f, 22500f));

        var verdict = LongDefeatGateChecks.GateLevel(1, 1.5f, Write(added));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "index=1->0");
    }

    [TestMethod]
    public void GateLevel_NoVisibleGate_OrANonNumber_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateLevel(1, 1.5f, Write()).Result, "a level bought where no gate stands changed nothing");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateLevel(1, 1.5f, Write(Change(Facts(11000f, 15000f), Facts(float.NaN, 22500f)))).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateLevel(1, float.NaN, Write(Levelled(11000f, 15000f, 22500f))).Result);
    }

    [TestMethod]
    public void GateLevel_TwoGates_AreEachJudged()
    {
        var good = Levelled(11000f, 15000f, 22500f);
        var bad = Change(Facts(15000f, 15000f), Facts(15000f, 20000f));

        Assert.AreEqual(CheckResult.Pass, LongDefeatGateChecks.GateLevel(1, 1.5f, Write(good, good)).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateLevel(1, 1.5f, Write(good, bad)).Result);
    }

    // ---- gate-repair ----

    private static LdGateWrite Repaired(float beforeHp, bool beforeDestroyed, float max = Vanilla, bool disabled = false, bool closed = true, bool open = false, int near = 0, int hiddenStanding = 0,
        bool resetFound = true, float afterHp = -1f)
    {
        var after = Facts(afterHp < 0f ? max : afterHp, max, false, open);
        return new LdGateWrite(new[] { Change(Facts(beforeHp, max, beforeDestroyed, beforeDestroyed), after, disabled, closed, near) }, 1, hiddenStanding, resetFound);
    }

    [TestMethod]
    public void GateRepair_ABrokenGateMadeWhole_Passes_NamesThePath_AndCountsWhoStandsNear()
    {
        var verdict = LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, near: 3));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        Assert.AreEqual("w=2 path=broken gates=1 hp=15000.0/15000.0 index=0 disabled=false closed=true open=false near=3 hidden_down=true reset=found", verdict.Detail);
    }

    [TestMethod]
    public void GateRepair_ADamagedGate_IsPathDamaged()
    {
        var verdict = LongDefeatGateChecks.GateRepair(3, 1f, Repaired(9000f, false));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "path=damaged");
    }

    [TestMethod]
    public void GateRepair_ALevelledGate_IsMadeWholeAtTheLevelledMaximum()
    {
        Assert.AreEqual(CheckResult.Pass, LongDefeatGateChecks.GateRepair(3, 1.5f, Repaired(0f, true, max: 22500f)).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(3, 1.5f, Repaired(0f, true, max: Vanilla)).Result, "the maximum is the vanilla one, not the level's");
    }

    [TestMethod]
    public void GateRepair_AnyStateTheResetShouldHaveClearedFails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, afterHp: 0f)).Result, "still destroyed");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, afterHp: 14000f)).Result, "not at the maximum");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, disabled: true)).Result, "still disabled");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, closed: false)).Result, "State is not Closed");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, open: true)).Result, "open");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, hiddenStanding: 1)).Result, "the hidden gate stood up");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Repaired(0f, true, resetFound: false)).Result, "the reflection found nothing (R10)");
    }

    [TestMethod]
    public void GateRepair_AGateTheRepairDidNotTouch_OrNone_Fails_AndAWholeOneIsLeftOut()
    {
        var untouched = Change(Facts(15000f, 15000f), Facts(15000f, 15000f), touched: false);
        var broken = Repaired(0f, true).Gates[0];

        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Write()).Result, "nothing was repaired");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateRepair(2, 1f, Write(untouched)).Result, "a bought repair touched no gate");
        var verdict = LongDefeatGateChecks.GateRepair(2, 1f, Write(broken, untouched));
        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "gates=1");
    }

    [TestMethod]
    public void GateRepair_OneBrokenAndOneDamaged_IsPathBroken()
    {
        var damaged = Repaired(9000f, false).Gates[0];
        var broken = Repaired(0f, true).Gates[0];

        Assert.AreEqual(CheckResult.Pass, LongDefeatGateChecks.GateRepair(2, 1f, Write(damaged, broken)).Result);
        StringAssert.Contains(LongDefeatGateChecks.GateRepair(2, 1f, Write(damaged, broken)).Detail, "path=broken");
    }

    // ---- gate-attacked ----

    private static GateWatch Watch(float baseline = 15000f, int samples = 12, int through = 0)
    {
        var watch = new GateWatch(2, baseline, "broken");
        for (var i = 0; i < samples; i++)
            watch.Sample(i == 0 ? through : 0);
        return watch;
    }

    [TestMethod]
    public void GateAttacked_ARepairedGateThatLostHitPoints_AndNoOneCameThrough_Passes()
    {
        var verdict = LongDefeatGateChecks.GateAttacked(Watch(), 12000f, false, false);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        Assert.AreEqual("w=2 path=broken start=15000.0 now=12000.0 lost=3000.0 fell=false through=0 samples=12", verdict.Detail);
    }

    [TestMethod]
    public void GateAttacked_AGateThatFellAgain_Passes_WithTheEndOfTheRunNamed()
    {
        var verdict = LongDefeatGateChecks.GateAttacked(Watch(), 0f, true, true);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.EndsWith(verdict.Detail, " ended=run");
        StringAssert.Contains(verdict.Detail, "fell=true");
    }

    [TestMethod]
    public void GateAttacked_AnUntouchedGate_IsASkipWithNoBlow()
    {
        var verdict = LongDefeatGateChecks.GateAttacked(Watch(), 15000f, false, false);

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.EndsWith(verdict.Detail, " reason=no-blow");
    }

    [TestMethod]
    public void GateAttacked_AnAttackerBehindTheStandingGate_Fails_BlowOrNot()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateAttacked(Watch(through: 2), 12000f, false, false).Result, "the repaired gate did not block");
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateAttacked(Watch(through: 1), 15000f, false, false).Result, "nobody should stand behind an untouched gate");
    }

    [TestMethod]
    public void AWatch_CountsOnlyTheSamplesThatMeasuredAPlane_ForTheFieldAnswersMinusOneWhenItCouldNot()
    {
        var watch = new GateWatch(2, 15000f, "broken");

        watch.Sample(-1);
        watch.Sample(0);
        watch.Sample(2);

        Assert.AreEqual(2, watch.Samples, "a sample with no keep or no plane measured nothing");
        Assert.AreEqual(2, watch.Through);
        var neverMeasured = new GateWatch(2, 15000f, "broken");
        neverMeasured.Sample(-1);
        StringAssert.Contains(LongDefeatGateChecks.GateAttacked(neverMeasured, 12000f, false, false).Detail, "through=0 samples=0");
    }

    [TestMethod]
    public void GateAttacked_NotNumbers_Fail()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateAttacked(Watch(), float.NaN, false, false).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.GateAttacked(Watch(float.NaN), 12000f, false, false).Result);
    }

    [TestMethod]
    public void GateAttacked_AHitPointLossUnderHalfAPoint_IsNotABlow()
    {
        Assert.AreEqual(CheckResult.Skip, LongDefeatGateChecks.GateAttacked(Watch(), 14999.8f, false, false).Result, "float noise");
    }

    [TestMethod]
    public void GateWatch_Rebase_MovesTheBaseline_AndANegativeSampleIsNotCounted()
    {
        var watch = new GateWatch(2, 15000f, "damaged");
        watch.Rebase(22499f);
        watch.Sample(-1);
        watch.Sample(2);

        Assert.AreEqual((22499f, 1, 2), (watch.Baseline, watch.Samples, watch.Through));
    }

    // ---- fort-points ----

    [TestMethod]
    public void FortPoints_NothingRecorded_IsASkip()
    {
        var verdict = LongDefeatGateChecks.FortPoints(new List<(string, LdPointFacts)>());

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        Assert.AreEqual("points=0 reason=none-recorded", verdict.Detail);
    }

    [TestMethod]
    public void FortPoints_EveryPointWithANavmeshInsideTheBoundary_Passes()
    {
        var points = new List<(string, LdPointFacts)> { ("hornburg.post[0]", new LdPointFacts(true, true)), ("volley[0]", new LdPointFacts(true, true)) };

        var verdict = LongDefeatGateChecks.FortPoints(points);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        Assert.AreEqual("points=2 failed=0", verdict.Detail);
    }

    [TestMethod]
    public void FortPoints_APointOffTheMeshOrOutsideTheBoundary_FailsAndIsNamed()
    {
        var points = new List<(string, LdPointFacts)>
        {
            ("hornburg.post[0]", new LdPointFacts(true, true)),
            ("hornburg.post[1]", new LdPointFacts(false, true)),
            ("volley[2]", new LdPointFacts(true, false)),
        };

        var verdict = LongDefeatGateChecks.FortPoints(points);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        Assert.AreEqual("points=3 failed=2 bad=hornburg.post[1](navmesh=no boundary=yes),volley[2](navmesh=yes boundary=no)", verdict.Detail);
    }

    [TestMethod]
    public void FortPoints_ALongListOfBadPoints_NamesTheFirstFew()
    {
        var points = Enumerable.Range(0, 20).Select(i => ($"volley[{i}]", new LdPointFacts(false, false))).ToList();

        var verdict = LongDefeatGateChecks.FortPoints(points);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "failed=20");
        StringAssert.Contains(verdict.Detail, "volley[4]");
        Assert.IsFalse(verdict.Detail.Contains("volley[5]"), "the line names five and counts the rest");
    }
}
