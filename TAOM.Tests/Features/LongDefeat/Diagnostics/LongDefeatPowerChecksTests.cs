using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>affix-effect</c> verdicts of the Hammer of the Underworld and Winged Terror (phase2-plan.md section 6, Slice 8): the hammer proves the write on every live engine and logs the
/// gate's state, the terror reads the pulses' own counts (<c>drained</c>, <c>pushed_below</c>) and their cost against the 16 ms budget.
/// </summary>
[TestClass]
public class LongDefeatPowerChecksTests
{
    private static LdEngineReading Ram(float vanilla, float max, float hit) => new("ram", vanilla, max, hit);

    private static LdEngineReading Tower(float vanilla, float max, float hit) => new("tower", vanilla, max, hit);

    // ---- hammer ----

    [TestMethod]
    public void Hammer_EveryEngineAtTheFactorOverItsVanillaHitPoints_Passes()
    {
        var report = new LdEngineReport(new[] { Ram(3000f, 4500f, 4500f), Tower(5000f, 7500f, 7000f), Tower(5000f, 7500f, 7500f) }, Gates: 2, GatesDestroyed: 0);

        var verdict = LongDefeatAffixChecks.Hammer(1.5f, report);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.StartsWith(verdict.Detail, "power=hammer ");
        StringAssert.Contains(verdict.Detail, "engines=3");
        StringAssert.Contains(verdict.Detail, "want=1.500");
        StringAssert.Contains(verdict.Detail, "gate=standing");
    }

    [TestMethod]
    public void Hammer_AnEngineStillAtItsVanillaHitPoints_FailsAndNamesIt()
    {
        var report = new LdEngineReport(new[] { Ram(3000f, 3000f, 3000f), Tower(5000f, 7500f, 7500f) }, Gates: 1, GatesDestroyed: 0);

        var verdict = LongDefeatAffixChecks.Hammer(1.5f, report);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "ram=1.000 WRONG");
    }

    [TestMethod]
    public void Hammer_TheGateStateIsLogged_AndADestroyedGateDoesNotFailTheWrite()
    {
        var report = new LdEngineReport(new[] { Ram(3000f, 4500f, 4500f) }, Gates: 1, GatesDestroyed: 1);

        var verdict = LongDefeatAffixChecks.Hammer(1.5f, report);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, "the write is proven on the ram whatever the gate does");
        StringAssert.Contains(verdict.Detail, "gate=destroyed");
    }

    [TestMethod]
    public void Hammer_WithNoLiveEngine_IsASkip_NotAPass()
    {
        var verdict = LongDefeatAffixChecks.Hammer(1.5f, new LdEngineReport(new LdEngineReading[0], Gates: 0, GatesDestroyed: 0));

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-engine");
        StringAssert.Contains(verdict.Detail, "gate=none");
    }

    [TestMethod]
    public void Hammer_AFactorOfOne_CouldNotShowAWrite_SoItIsASkip()
    {
        var report = new LdEngineReport(new[] { Ram(3000f, 3000f, 3000f) }, Gates: 1, GatesDestroyed: 0);

        var verdict = LongDefeatAffixChecks.Hammer(1f, report);

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-signal");
    }

    [TestMethod]
    public void Hammer_AnEngineWithNoVanillaHitPoints_Fails()
    {
        var report = new LdEngineReport(new[] { Ram(0f, 0f, 0f) }, Gates: 1, GatesDestroyed: 0);

        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Hammer(1.5f, report).Result);
    }

    [TestMethod]
    public void Hammer_TheRatioHasAOnePercentTolerance()
    {
        var near = new LdEngineReport(new[] { Ram(3000f, 4510f, 4510f) }, Gates: 1, GatesDestroyed: 0);
        var off = new LdEngineReport(new[] { Ram(3000f, 4700f, 4700f) }, Gates: 1, GatesDestroyed: 0);

        Assert.AreEqual(CheckResult.Pass, LongDefeatAffixChecks.Hammer(1.5f, near).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Hammer(1.5f, off).Result);
    }

    // ---- terror ----

    private static LdTerrorTally Tally(params LdPulseResult[] pulses)
    {
        var tally = new LdTerrorTally();
        foreach (var pulse in pulses)
            tally.Add(pulse);
        return tally;
    }

    [TestMethod]
    public void Terror_PulsesThatDrainedAndPushedNobodyBelow_Pass()
    {
        var tally = Tally(new LdPulseResult(200, 40, 120f, 0, 0.4), new LdPulseResult(198, 35, 100f, 0, 0.6));

        var verdict = LongDefeatAffixChecks.Terror(5, 35f, tally);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.StartsWith(verdict.Detail, "power=terror ");
        StringAssert.Contains(verdict.Detail, "w=5");
        StringAssert.Contains(verdict.Detail, "pulses=2");
        StringAssert.Contains(verdict.Detail, "drained=75");
        StringAssert.Contains(verdict.Detail, "pushed_below=0");
        StringAssert.Contains(verdict.Detail, "max_ms=0.6");
    }

    [TestMethod]
    public void Terror_AnyDefenderPushedBelowTheFloor_Fails()
    {
        var tally = Tally(new LdPulseResult(200, 40, 120f, 0, 0.4), new LdPulseResult(198, 35, 100f, 1, 0.6));

        var verdict = LongDefeatAffixChecks.Terror(5, 35f, tally);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "pushed_below=1");
    }

    [TestMethod]
    public void Terror_APulseOverTheSixteenMillisecondBudget_Fails()
    {
        var tally = Tally(new LdPulseResult(200, 40, 120f, 0, 0.4), new LdPulseResult(198, 35, 100f, 0, 16.0));

        var verdict = LongDefeatAffixChecks.Terror(5, 35f, tally);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "max_ms=16.0");
        Assert.AreEqual(CheckResult.Pass, LongDefeatAffixChecks.Terror(5, 35f, Tally(new LdPulseResult(200, 40, 120f, 0, 15.9))).Result);
    }

    [TestMethod]
    public void Terror_NoPulseAtAll_IsASkip_AsIsAWaveWhereNobodyWasDrained()
    {
        var none = LongDefeatAffixChecks.Terror(5, 35f, Tally());
        var idle = LongDefeatAffixChecks.Terror(5, 35f, Tally(new LdPulseResult(200, 0, 0f, 0, 0.2)));

        Assert.AreEqual(CheckResult.Skip, none.Result);
        StringAssert.Contains(none.Detail, "reason=no-pulse");
        Assert.AreEqual(CheckResult.Skip, idle.Result);
        StringAssert.Contains(idle.Detail, "reason=no-signal");
    }

    [TestMethod]
    public void TheTally_KeepsTotalsAndTheSlowestPulse()
    {
        var tally = Tally(new LdPulseResult(10, 4, 12f, 0, 1.0), new LdPulseResult(10, 3, 9f, 2, 3.0), new LdPulseResult(10, 1, 3f, 0, 2.0));

        Assert.AreEqual(3, tally.Pulses);
        Assert.AreEqual(8, tally.Drained);
        Assert.AreEqual(24f, tally.Lost, 1e-4f);
        Assert.AreEqual(2, tally.PushedBelow);
        Assert.AreEqual(3.0, tally.MaxMilliseconds, 1e-9);
    }
}
