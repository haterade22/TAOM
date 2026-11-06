using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>The <c>commander</c> and <c>summon</c> verdicts (phase2-plan.md section 6): the plan's rank, steps and health, abilities that fired when due, nothing after his end, every summon sent.</summary>
[TestClass]
public class LongDefeatCommanderChecksTests
{
    private static readonly CommanderSpec Lieutenant = CommanderPlanTests.Plan().ForWave(8)!;

    private static CommanderRecord Record(CommanderOutcome outcome = CommanderOutcome.Killed, float alive = 100f, int cry = 2, int summon = 1, int afterEnd = 0, string troop = "ugluk",
        string rank = "lieutenant", int steps = 0) =>
        new(troop, rank, steps, outcome, new Dictionary<string, int> { ["war_cry"] = cry, ["summon_uruks"] = summon }, afterEnd, alive);

    private static LdScaleSample Sample(float ratio) => new("ugluk", ratio * 100f, 100f, 0);

    [TestMethod]
    public void ACommanderAtTheRankStepsAndHealthThePlanGives_Passes()
    {
        var verdict = LongDefeatCommanderChecks.Commander(8, Record(), Lieutenant, 1f, Sample(3f));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "w=8 troop=ugluk rank=lieutenant steps=0");
        StringAssert.Contains(verdict.Detail, "uses=war_cry:2,summon_uruks:1");
    }

    [TestMethod]
    public void TheHealthIsTheRankFactorTimesTheEndlessFactor_WithinOnePercent()
    {
        Assert.AreEqual(CheckResult.Pass, LongDefeatCommanderChecks.Commander(8, Record(), Lieutenant, 1.5f, Sample(4.5f * 1.009f)).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(), Lieutenant, 1.5f, Sample(4.5f * 1.02f)).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(), Lieutenant, 1.5f, Sample(3f)).Result, "the endless factor was left out");
    }

    [TestMethod]
    public void AnUnreadHealth_FailsWithAnAdapter_AndIsLeftOutWithout()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(), Lieutenant, 1f, new LdScaleSample("", 0f, 0f, 0)).Result);
        Assert.AreEqual(CheckResult.Pass, LongDefeatCommanderChecks.Commander(8, Record(), Lieutenant, 1f, null).Result);
    }

    [TestMethod]
    public void AWrongTroopRankOrSteps_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(troop: "lurtz"), Lieutenant, 1f, null).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(rank: "general"), Lieutenant, 1f, null).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(steps: 1), Lieutenant, 1f, null).Result);
    }

    [TestMethod]
    public void AUseAfterHisEnd_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(afterEnd: 1), Lieutenant, 1f, null).Result);
    }

    [TestMethod]
    public void AStandingCommanderAtTheClear_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(CommanderOutcome.Standing), Lieutenant, 1f, null).Result);
    }

    [TestMethod]
    public void AnAbilityDueWhileHeStood_MustHaveFired_ButNotOneThatWasNotDueYet()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(alive: 100f, summon: 0), Lieutenant, 1f, null).Result, "the summon is due at 30 s");
        Assert.AreEqual(CheckResult.Pass, LongDefeatCommanderChecks.Commander(8, Record(alive: 25f, cry: 1, summon: 0), Lieutenant, 1f, null).Result, "25 s in: the cry fired, the summon is not due");
        Assert.AreEqual(CheckResult.Pass, LongDefeatCommanderChecks.Commander(8, Record(alive: 5f, cry: 0, summon: 0), Lieutenant, 1f, null).Result, "killed within seconds");
        Assert.AreEqual(CheckResult.Pass, LongDefeatCommanderChecks.Commander(8, Record(CommanderOutcome.Left, alive: 0f, cry: 0, summon: 0), Lieutenant, 1f, null).Result, "a spawn that failed");
    }

    [TestMethod]
    public void AnAbilityUsedMoreOftenThanItsCooldownAllows_Fails()
    {
        // The war cry is due at 20 s and every 60 s after: in 200 s it can have fired at 20, 80, 140 and 200.
        Assert.AreEqual(CheckResult.Pass, LongDefeatCommanderChecks.Commander(8, Record(alive: 200f, cry: 4, summon: 2), Lieutenant, 1f, null).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Commander(8, Record(alive: 200f, cry: 5, summon: 2), Lieutenant, 1f, null).Result);
    }

    [TestMethod]
    public void TheLegendCooldownBoon_StretchesWhatMayHaveFired()
    {
        var silence = new BoonRow("the_long_silence", BoonCategory.Legend, "{=K}n", "{=T}t", new[] { new BoonEffect(BoonKeys.CommanderCooldownPercent, 50f) });
        var owned = new[] { silence };

        var stretched = LongDefeatCommanderChecks.Commander(8, Record(alive: 200f, cry: 4, summon: 1), Lieutenant, 1f, null, owned);
        var fine = LongDefeatCommanderChecks.Commander(8, Record(alive: 200f, cry: 3, summon: 1), Lieutenant, 1f, null, owned);

        Assert.AreEqual(CheckResult.Fail, stretched.Result, "with 90 s between cries only 20, 110 and 200 fit: a fourth proves the boon never reached the clock");
        Assert.AreEqual(CheckResult.Pass, fine.Result, fine.Detail);
        StringAssert.Contains(fine.Detail, "cooldown_x=1.50");
    }

    [TestMethod]
    public void ASummon_PassesWhenItQueuedThePlansCountAndEverySoldierSpawned()
    {
        var verdict = LongDefeatCommanderChecks.Summon(8, 1, 20, 20, 20, 30.4f, 41f, 212f);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "w=8 s=1 queued=20 expected=20 spawned=20 fired_at=30.40 spawned_at=41.00");
    }

    [TestMethod]
    public void ASummon_FailsOnAWrongCount_OrSoldiersThatNeverCame()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Summon(8, 1, 15, 20, 15, 30f, 41f, 212f).Result, "queued fewer than the plan gives");
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Summon(8, 1, 20, 20, 17, 30f, 41f, 212f).Result, "three never spawned");
        Assert.AreEqual(CheckResult.Fail, LongDefeatCommanderChecks.Summon(8, 1, 0, 0, 0, 30f, 41f, 212f).Result, "a summon of nobody proves nothing");
    }

    [TestMethod]
    public void ASummonTheRunEndedOn_IsASkip()
    {
        Assert.AreEqual(CheckResult.Skip, LongDefeatCommanderChecks.SummonUnfinished(8, 1, 20, 6).Result);
    }
}
