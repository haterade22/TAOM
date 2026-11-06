using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>affix-effect</c> verdict of Morgul Blades (phase2-plan.md section 6, Slice 9), read from the run's own counts for the wave: it fails when any poison tick found its owner not active at the call
/// (<c>owner_missing</c>), passes when a dose took health, and skips a wave in which nothing could show the effect.
/// </summary>
[TestClass]
public class LongDefeatBladesChecksTests
{
    private static PoisonTally Tally(int doses, int hits, int damage, int ownerMissing = 0, int withheld = 0) =>
        new() { Doses = doses, Hits = hits, Damage = damage, OwnerMissing = ownerMissing, Withheld = withheld };

    [TestMethod]
    public void ADoseThatTookHealth_WithNoOwnerMissing_Passes()
    {
        var verdict = LongDefeatAffixChecks.Blades(5, Tally(12, 40, 96, withheld: 1));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        Assert.AreEqual("power=blades w=5 doses=12 hits=40 damage=96 owner_missing=0 withheld=1", verdict.Detail);
    }

    [TestMethod]
    public void AnyOwnerMissing_Fails_EvenWithDamageDone()
    {
        var verdict = LongDefeatAffixChecks.Blades(5, Tally(12, 40, 96, ownerMissing: 1));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "owner_missing=1");
    }

    [TestMethod]
    public void AWaveWithNoHit_IsASkip()
    {
        var verdict = LongDefeatAffixChecks.Blades(5, Tally(0, 0, 0));

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-hit");
    }

    [TestMethod]
    public void ADoseThatNeverTookHealth_IsASkip_NotAPass()
    {
        // Every victim was removed before the first whole point: the effect was not shown.
        var verdict = LongDefeatAffixChecks.Blades(5, Tally(3, 3, 0));

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-damage");
    }

    [TestMethod]
    public void OwnerMissing_FailsBeforeAnySkip()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Blades(5, Tally(0, 0, 0, ownerMissing: 2)).Result);
    }

    [TestMethod]
    public void Blades_HasNoStatExpectation_SoTheStatReadAsksNothingOfIt()
    {
        var plan = new AffixPlan(new[] { new AffixStage(5, 1) }, new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyDictionary<string, float>>(),
            new System.Collections.Generic.Dictionary<string, string>());

        Assert.AreEqual(0, LongDefeatAffixChecks.Expectations(plan, AffixPlan.Blades).Count);
    }
}
