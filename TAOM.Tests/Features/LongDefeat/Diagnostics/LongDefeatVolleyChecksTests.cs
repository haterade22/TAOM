using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>volley</c> verdict (phase3-plan.md section 6): per use, the shots added, one distinct target per arrow while targets lasted, no agent spawned, hits within the window at or over half the arrows with
/// blocked hits apart, no friendly hit that was not cancelled, and the A24 and A25 counts reported, never judged. A window cut short by the clear or the run's end can pass but never fail on the share.
/// </summary>
[TestClass]
public class LongDefeatVolleyChecksTests
{
    private static VolleyUse Use(int added = 12, int candidates = 30, int distinct = 12, int spawned = 0, VolleyAimMode mode = VolleyAimMode.Auto, bool bot = false, float? gap = null, int shots = -1) =>
        new(1, 1, VolleySource.Elven, mode, bot, gap, 12, shots < 0 ? added : shots, candidates, distinct, added, spawned, "fow_rohan_eastfold_bowman", 1f);

    private static LdVolleyHit Hit(LdVolleyHitKind kind = LdVolleyHitKind.Foe, bool kill = false, bool nullAffector = false, bool wrongSide = false) =>
        new("ld_elven_arrow", kind, kill, nullAffector, wrongSide, 45f, 30f);

    private static VolleyWatch Watch(VolleyUse use, int foe = 0, int blocked = 0, int friendly = 0, int cancelled = 0, int kills = 0)
    {
        var watch = new VolleyWatch(use);
        for (var i = 0; i < foe; i++)
            watch.Add(Hit(kill: i < kills));
        for (var i = 0; i < blocked; i++)
            watch.Add(Hit(LdVolleyHitKind.Blocked));
        for (var i = 0; i < friendly; i++)
            watch.Add(Hit(LdVolleyHitKind.Friendly));
        for (var i = 0; i < cancelled; i++)
            watch.Add(Hit(LdVolleyHitKind.Cancelled));
        return watch;
    }

    private static CheckVerdict Judge(VolleyUse use, VolleyWatch watch, float seconds = 6f, string? ended = null) => LongDefeatVolleyChecks.Volley(use, watch, seconds, ended);

    [TestMethod]
    public void TheWatch_CountsEachKindApart_AndAKillOnlyOnAFoeHit()
    {
        var watch = new VolleyWatch(Use());
        watch.Add(Hit(kill: true));
        watch.Add(Hit());
        watch.Add(Hit(LdVolleyHitKind.Blocked));
        watch.Add(Hit(LdVolleyHitKind.Friendly));
        watch.Add(Hit(LdVolleyHitKind.Cancelled));
        watch.Add(Hit(nullAffector: true));
        watch.Add(Hit(wrongSide: true));

        Assert.AreEqual(4, watch.Hits);
        Assert.AreEqual(1, watch.Kills);
        Assert.AreEqual(1, watch.Blocked);
        Assert.AreEqual(1, watch.Friendly);
        Assert.AreEqual(1, watch.Cancelled);
        Assert.AreEqual(1, watch.AffectorNull);
        Assert.AreEqual(1, watch.AffectorSide);
        Assert.AreEqual(45f, watch.BaseSpeed);
        Assert.AreEqual(30f, watch.ImpactSpeed, "the first hit's impact speed is written beside its base speed (A25): an impact under the base shows the air drag");
    }

    [TestMethod]
    public void AUseWithEnoughHits_Passes_AndTheLineNamesWhatItJudged()
    {
        var verdict = Judge(Use(), Watch(Use(), foe: 7, blocked: 1, kills: 3));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.StartsWith(verdict.Detail, "w=1 use=1 source=elven mode=auto arrows=12 shots=12 candidates=30 distinct=12 spawned=0 hits=7 blocked=1 friendly=0 cancelled=0 kills=3 ");
        StringAssert.Contains(verdict.Detail, " affector_null=0 affector_side=0 owner=fow_rohan_eastfold_bowman mult=1.00 base_speed=45.0 impact_speed=30.0 window=6.0");
    }

    [TestMethod]
    public void HalfTheArrows_IsEnough_AndOneFewerIsNot()
    {
        Assert.AreEqual(CheckResult.Pass, Judge(Use(), Watch(Use(), foe: 6)).Result);
        Assert.AreEqual(CheckResult.Fail, Judge(Use(), Watch(Use(), foe: 5)).Result);
    }

    [TestMethod]
    public void BlockedHits_AreNotInTheShare()
    {
        Assert.AreEqual(CheckResult.Fail, Judge(Use(), Watch(Use(), foe: 5, blocked: 6)).Result);
    }

    [TestMethod]
    public void AFriendlyHitThatWasNotCancelled_Fails_AndACancelledOneDoesNot()
    {
        Assert.AreEqual(CheckResult.Fail, Judge(Use(), Watch(Use(), foe: 8, friendly: 1)).Result);
        var cancelled = Judge(Use(), Watch(Use(), foe: 8, cancelled: 2));
        Assert.AreEqual(CheckResult.Pass, cancelled.Result);
        StringAssert.Contains(cancelled.Detail, "cancelled=2");
    }

    [TestMethod]
    public void AnAgentSpawnedByTheFire_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, Judge(Use(spawned: 1), Watch(Use(spawned: 1), foe: 12)).Result);
    }

    [TestMethod]
    public void OneDistinctTargetPerArrow_WhileTargetsLasted_AndTheyWrapAfter()
    {
        Assert.AreEqual(CheckResult.Pass, Judge(Use(candidates: 5, distinct: 5), Watch(Use(), foe: 12)).Result, "12 arrows over 5 attackers: all 5 aimed at by name");
        Assert.AreEqual(CheckResult.Fail, Judge(Use(candidates: 30, distinct: 9), Watch(Use(), foe: 12)).Result, "30 attackers in reach and only 9 aimed at");
    }

    [TestMethod]
    public void FewerArrowsAddedThanShotsPlanned_Fails_AndSoDoesAUseThatAddedNone()
    {
        Assert.AreEqual(CheckResult.Fail, Judge(Use(added: 10, shots: 12), Watch(Use(), foe: 12)).Result);
        Assert.AreEqual(CheckResult.Fail, Judge(Use(added: 0, shots: 0, distinct: 0), Watch(Use(), foe: 0)).Result);
    }

    [TestMethod]
    public void AWindowCutShort_CanPass_ButNeverFailsOnTheShare()
    {
        var cut = Judge(Use(), Watch(Use(), foe: 2), seconds: 1.5f, ended: "run");
        Assert.AreEqual(CheckResult.Skip, cut.Result);
        StringAssert.Contains(cut.Detail, "ended=run");
        StringAssert.Contains(cut.Detail, "reason=window-cut");

        var enough = Judge(Use(), Watch(Use(), foe: 7), seconds: 1.5f, ended: "wave");
        Assert.AreEqual(CheckResult.Pass, enough.Result);
        StringAssert.Contains(enough.Detail, "ended=wave");
    }

    [TestMethod]
    public void AWindowCutShort_StillFailsOnAFriendlyHit()
    {
        Assert.AreEqual(CheckResult.Fail, Judge(Use(), Watch(Use(), foe: 1, friendly: 1), seconds: 1.5f, ended: "run").Result);
    }

    [TestMethod]
    public void TheCrosshairLine_NamesWhoAimedIt_AndHowFarTheAimWasFromAnAttacker()
    {
        var person = Judge(Use(mode: VolleyAimMode.Crosshair, bot: false, gap: 2.1f), Watch(Use(), foe: 8));
        var bot = Judge(Use(mode: VolleyAimMode.Crosshair, bot: true, gap: 0.5f), Watch(Use(), foe: 8));

        StringAssert.Contains(person.Detail, "mode=crosshair aim=person ray_gap=2.1 arrows=");
        StringAssert.Contains(bot.Detail, "mode=crosshair aim=bot ray_gap=0.5 arrows=");
        Assert.IsFalse(Judge(Use(), Watch(Use(), foe: 8)).Detail.Contains("aim="), "an automatic volley has no aim token");
    }

    [TestMethod]
    public void ANullAffectorAndAWrongSideAffector_AreReported_NotJudged()
    {
        var watch = Watch(Use(), foe: 8);
        watch.Add(Hit(nullAffector: true));
        watch.Add(Hit(wrongSide: true));

        var verdict = Judge(Use(), watch);

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, "affector_null=1 affector_side=1");
    }

    [TestMethod]
    public void ATowerSource_IsNamedByItsId()
    {
        var use = new VolleyUse(3, 2, VolleySource.Tower("hornburg"), VolleyAimMode.Auto, false, null, 6, 6, 9, 6, 6, 0, "fow_rohan_eastfold_bowman", 1.1f);

        var verdict = Judge(use, Watch(use, foe: 4));

        StringAssert.StartsWith(verdict.Detail, "w=3 use=2 source=tower-hornburg mode=auto arrows=6 shots=6");
        StringAssert.Contains(verdict.Detail, "mult=1.10");
    }

    [TestMethod]
    public void TheOwnersMultiplier_IsReported_NotJudged()
    {
        var use = Use() with { Multiplier = float.NaN };

        Assert.AreEqual(CheckResult.Pass, Judge(use, Watch(use, foe: 8)).Result);
    }
}
