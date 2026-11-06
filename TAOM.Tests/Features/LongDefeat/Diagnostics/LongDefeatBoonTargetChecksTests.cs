using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The read-back half of the <c>boon</c> check (phase2-plan.md section 6, Slice 6): what a troop or class boon must have done to a soldier of its target, read from the soldier
/// standing at the pick and from one spawned after it. The arithmetic is written here apart from <c>RunModifiers</c>, so a check that shared it could not catch a change in it.
/// </summary>
[TestClass]
public class LongDefeatBoonTargetChecksTests
{
    private static BoonRow Boon(string id, params (string Key, float Value, BoonTarget Target)[] effects) =>
        new(id, BoonCategory.Infantry, "{=K}" + id, "{=T}text", effects.Select(e => new BoonEffect(e.Key, e.Value, e.Target)).ToList());

    private static LdTargetSample Sample(float limit, float max, params (string Stat, float Base, float Actual)[] readings) =>
        new("fow_rohan_westfold_militiaman", limit, max, readings.Select(r => new LdStatReading(r.Stat, r.Base, r.Actual)).ToList());

    [TestMethod]
    public void EachStatKey_ExpectsTheDrivenPropertiesTheEngineApplierWrites()
    {
        var owned = new[]
        {
            Boon("a", (BoonKeys.SwingSpeedPercent, 10f, BoonTarget.Infantry), (BoonKeys.MoveSpeedPercent, 5f, BoonTarget.Infantry), (BoonKeys.DrawSpeedPercent, 20f, BoonTarget.Infantry),
                (BoonKeys.ReloadSpeedPercent, 15f, BoonTarget.Infantry), (BoonKeys.BlockPercent, 25f, BoonTarget.Infantry), (BoonKeys.ParryPercent, 30f, BoonTarget.Infantry),
                (BoonKeys.AimErrorPercent, -20f, BoonTarget.Infantry)),
        };

        var expected = LongDefeatBoonChecks.TargetExpectations(owned, BoonTarget.Infantry);

        CollectionAssert.AreEquivalent(new[]
        {
            "SwingSpeedMultiplier", "MaxSpeedMultiplier", "CombatMaxSpeedMultiplier", "ThrustOrRangedReadySpeedMultiplier", "ReloadSpeed",
            "AIBlockOnDecideAbility", "AIParryOnDecideAbility", "AiShooterError",
        }, expected.Select(e => e.Stat).ToArray());
        Assert.AreEqual(AffixMath.Chance, expected.Single(e => e.Stat == "AIBlockOnDecideAbility").Math);
        Assert.AreEqual(-20f, expected.Single(e => e.Stat == "AiShooterError").Percent);
        Assert.AreEqual(0, LongDefeatBoonChecks.TargetExpectations(owned, BoonTarget.Archers).Count, "another target's properties are not expected to change");
    }

    [TestMethod]
    public void AMoveSpeedBoon_OfTheCavalryOrThePlayer_AlsoExpectsTheHorsesMountSpeed_ButNotInfantrys()
    {
        var cavalry = new[] { Boon("a", (BoonKeys.MoveSpeedPercent, 12f, BoonTarget.Cavalry)) };
        var lord = new[] { Boon("b", (BoonKeys.MoveSpeedPercent, 8f, BoonTarget.Player)) };
        var infantry = new[] { Boon("c", (BoonKeys.MoveSpeedPercent, 5f, BoonTarget.Infantry)) };

        var horse = LongDefeatBoonChecks.TargetExpectations(cavalry, BoonTarget.Cavalry).Single(e => e.Stat == "MountSpeed");

        Assert.AreEqual(AffixMath.Times, horse.Math);
        Assert.AreEqual(12f, horse.Percent);
        Assert.IsTrue(LongDefeatBoonChecks.TargetExpectations(lord, BoonTarget.Player).Any(e => e.Stat == "MountSpeed"));
        Assert.IsFalse(LongDefeatBoonChecks.TargetExpectations(infantry, BoonTarget.Infantry).Any(e => e.Stat == "MountSpeed"), "a foot group has no horse, and the applier gives it no mount speed");
    }

    [TestMethod]
    public void ARiderWhoseOwnSpeedIsRight_ButWhoseHorseKeptItsOldSpeed_FailsTheRead()
    {
        var owned = new[] { Boon("a", (BoonKeys.MoveSpeedPercent, 12f, BoonTarget.Cavalry)) };
        var sample = Sample(100f, 100f, ("MaxSpeedMultiplier", 1f, 1.12f), ("CombatMaxSpeedMultiplier", 1f, 1.12f), ("MountSpeed", 1f, 1f));

        var verdict = LongDefeatBoonChecks.TargetRead(3, "spawned", BoonTarget.Cavalry, owned, sample);

        Assert.AreEqual(CheckResult.Fail, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "MountSpeed=1.000->1.000 want 1.120 WRONG");
    }

    [TestMethod]
    public void ARiderWhoseHorseHoldsTheSpeed_Passes_AndAFootSoldierWithNoHorseIsNotFailedForIt()
    {
        var owned = new[] { Boon("a", (BoonKeys.MoveSpeedPercent, 12f, BoonTarget.Cavalry)) };
        var mounted = Sample(100f, 100f, ("MaxSpeedMultiplier", 1f, 1.12f), ("CombatMaxSpeedMultiplier", 1f, 1.12f), ("MountSpeed", 1f, 1.12f));
        var onFoot = Sample(100f, 100f, ("MaxSpeedMultiplier", 1f, 1.12f), ("CombatMaxSpeedMultiplier", 1f, 1.12f));

        Assert.AreEqual(CheckResult.Pass, LongDefeatBoonChecks.TargetRead(3, "standing", BoonTarget.Cavalry, owned, mounted).Result);
        var foot = LongDefeatBoonChecks.TargetRead(3, "standing", BoonTarget.Cavalry, owned, onFoot);
        Assert.AreEqual(CheckResult.Pass, foot.Result, foot.Detail);
        Assert.IsFalse(foot.Detail.Contains("MountSpeed"), "no horse stood, so no mount reading is claimed");
    }

    [TestMethod]
    public void PercentsOfOneProperty_SumAcrossBoons_DrawbacksIncluded()
    {
        var owned = new[]
        {
            Boon("a", (BoonKeys.SwingSpeedPercent, 10f, BoonTarget.Player)),
            Boon("b", (BoonKeys.SwingSpeedPercent, 5f, BoonTarget.Player)) with
            {
                Drawback = new[] { new BoonEffect(BoonKeys.SwingSpeedPercent, -3f, BoonTarget.Player) },
                DrawbackText = "{=D}d",
            },
        };

        Assert.AreEqual(12f, LongDefeatBoonChecks.TargetExpectations(owned, BoonTarget.Player).Single().Percent, 1e-4f);
    }

    [TestMethod]
    public void AHealthBoon_ExpectsTheProductOfEveryOwnedFactorForItsTarget()
    {
        var owned = new[] { Boon("a", (BoonKeys.HealthPercent, 20f, BoonTarget.Infantry)), Boon("b", (BoonKeys.HealthPercent, 10f, BoonTarget.Infantry)) };

        Assert.AreEqual(1.32f, LongDefeatBoonChecks.ExpectedHealth(owned, BoonTarget.Infantry), 1e-5f);
        Assert.AreEqual(1f, LongDefeatBoonChecks.ExpectedHealth(owned, BoonTarget.Cavalry));
    }

    [TestMethod]
    public void ASoldierWhoseHealthAndStatsMatch_Passes()
    {
        var owned = new[] { Boon("a", (BoonKeys.HealthPercent, 20f, BoonTarget.Infantry), (BoonKeys.SwingSpeedPercent, 10f, BoonTarget.Infantry)) };
        var sample = Sample(120f, 100f, ("SwingSpeedMultiplier", 1f, 1.1f));

        var verdict = LongDefeatBoonChecks.TargetRead(3, "standing", BoonTarget.Infantry, owned, sample);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "p=3 read=standing target=infantry troop=fow_rohan_westfold_militiaman");
        StringAssert.Contains(verdict.Detail, "health=1.200 want 1.200");
    }

    [TestMethod]
    public void ASoldierWhoseHealthLimitIsUnchanged_FailsTheRead()
    {
        var owned = new[] { Boon("a", (BoonKeys.HealthPercent, 20f, BoonTarget.Infantry)) };

        var verdict = LongDefeatBoonChecks.TargetRead(3, "spawned", BoonTarget.Infantry, owned, Sample(100f, 100f));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "WRONG");
        StringAssert.Contains(verdict.Detail, "read=spawned");
    }

    [TestMethod]
    public void ASoldierWhoseStatIsUnchanged_FailsTheRead()
    {
        var owned = new[] { Boon("a", (BoonKeys.MoveSpeedPercent, 12f, BoonTarget.Cavalry)) };
        var sample = Sample(100f, 100f, ("MaxSpeedMultiplier", 1f, 1f), ("CombatMaxSpeedMultiplier", 1f, 1.12f));

        var verdict = LongDefeatBoonChecks.TargetRead(3, "standing", BoonTarget.Cavalry, owned, sample);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
    }

    [TestMethod]
    public void NoSoldierOfTheGroup_IsASkip_NotAFailure()
    {
        var owned = new[] { Boon("a", (BoonKeys.HealthPercent, 20f, BoonTarget.Cavalry)) };

        var verdict = LongDefeatBoonChecks.TargetRead(3, "standing", BoonTarget.Cavalry, owned, LdTargetSample.None);

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-soldier");
    }

    [TestMethod]
    public void ARead_ThatCouldNotShowAChange_IsASkip()
    {
        var owned = new[] { Boon("a", (BoonKeys.BlockPercent, 25f, BoonTarget.Infantry)) };
        var sample = Sample(100f, 100f, ("AIBlockOnDecideAbility", 1f, 1f));

        var verdict = LongDefeatBoonChecks.TargetRead(3, "standing", BoonTarget.Infantry, owned, sample);

        Assert.AreEqual(CheckResult.Skip, verdict.Result, "a chance already at its ceiling shows no change; it is no pass");
        StringAssert.Contains(verdict.Detail, "reason=no-signal");
    }

    private static CheckVerdict PickWith(params CheckVerdict[] reads)
    {
        var record = new BoonPickRecord(1, 3);
        record.Draws.Add(new BoonDraw(new[] { "a", "b", "c" }, 3, 3, 0));
        record.Taken = "a";
        var owned = new[] { Boon("a", (BoonKeys.HealthPercent, 20f, BoonTarget.Infantry)) };
        return LongDefeatBoonChecks.Pick(record, owned, RunRules.Default, RunRules.Default, GoldFactors.One, 0, reads);
    }

    [TestMethod]
    public void ThePickVerdict_FailsWhenAnyReadFails_AndNamesTheWorstRead()
    {
        var verdict = PickWith(new CheckVerdict(CheckResult.Pass, "x"), new CheckVerdict(CheckResult.Fail, "y"));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "read=fail");
    }

    [TestMethod]
    public void ThePickVerdict_PassesOnPassingOrSkippedReads_AndSaysWhichKind()
    {
        Assert.AreEqual(CheckResult.Pass, PickWith(new CheckVerdict(CheckResult.Pass, "x")).Result);
        StringAssert.Contains(PickWith(new CheckVerdict(CheckResult.Pass, "x")).Detail, "read=pass");
        StringAssert.Contains(PickWith(new CheckVerdict(CheckResult.Skip, "x")).Detail, "read=skip");
        StringAssert.Contains(PickWith(new CheckVerdict(CheckResult.Pass, "x"), new CheckVerdict(CheckResult.Skip, "y")).Detail, "read=pass");
    }

    [TestMethod]
    public void ThePickVerdict_WithNoReads_KeepsItsShapeAndNamesNone()
    {
        var verdict = PickWith();

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        Assert.IsFalse(verdict.Detail.Contains("read="), "a war chest or fortress pick has nothing to read back");
        StringAssert.StartsWith(verdict.Detail, "p=1 cards=3 eligible=3 offered=a,b,c taken=a");
    }
}
