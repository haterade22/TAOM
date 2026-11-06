using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>The <c>sally</c>, <c>sally-return</c> and <c>sally-contact</c> verdicts (phase2-plan.md section 6, spike S9).</summary>
[TestClass]
public class LongDefeatSallyChecksTests
{
    private static readonly LdPlace Point = new(100f, 200f, 30f, 0f, 1f);
    private static readonly LdPlace Home = new(10f, 20f, 30f, 0f, 1f);

    private static LdLordFacts Lord(float x, float y, bool mount = true, bool present = true) =>
        new(present, true, "lord_4_1", mount, new LdPoint(x, y, 30f), true, "Cavalry", null, 3f, 9f, "closed");

    [TestMethod]
    public void AMountedLordWithin2m_AfterATeleport_Passes()
    {
        var verdict = LongDefeatSallyChecks.Out(1, "teleport", Lord(101f, 200f), Point, null, 0, 0);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.StartsWith(verdict.Detail, "w=1 how=teleport mount=yes dist=1.0 ");
    }

    [TestMethod]
    public void MoreThan2mFromThePoint_Fails()
    {
        Assert.AreEqual(CheckResult.Pass, LongDefeatSallyChecks.Out(1, "teleport", Lord(102f, 200f), Point, null, 0, 0).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatSallyChecks.Out(1, "teleport", Lord(102.1f, 200f), Point, null, 0, 0).Result);
    }

    [TestMethod]
    public void ALordWithNoHorseAfterTheMove_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatSallyChecks.Out(1, "remount", Lord(100f, 200f, mount: false), Point, Okay(), 0, 0).Result);
    }

    [TestMethod]
    public void ADeathCountedByTheMove_Fails()
    {
        var verdict = LongDefeatSallyChecks.Out(1, "remount", Lord(100f, 200f), Point, Okay(), 0, 1);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "deaths=0->1");
    }

    [TestMethod]
    public void ARemountWhoseOldBodyWasReportedLost_Fails_AndNamesItsState()
    {
        var verdict = LongDefeatSallyChecks.Out(1, "remount", Lord(100f, 200f), Point, new LdRemount(true, "Killed", true, 0.5f, ""), 0, 0);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "old_state=Killed old_lost=yes");
    }

    [TestMethod]
    public void ARemountThatWasNotDone_Fails()
    {
        var verdict = LongDefeatSallyChecks.Out(1, "remount", Lord(100f, 200f), Point, new LdRemount(false, "", false, 0f, "no body"), 0, 0);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
    }

    [TestMethod]
    public void ALordNotStandingAfterTheMove_Fails_AndANonNumberDistanceFails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatSallyChecks.Out(1, "teleport", LdLordFacts.None, Point, null, 0, 0).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatSallyChecks.Out(1, "teleport", Lord(float.NaN, 200f), Point, null, 0, 0).Result);
    }

    [TestMethod]
    public void TheReturn_PassesWithinTheKeepRadiusOfTheHomePoint_AndFailsBeyondIt()
    {
        Assert.AreEqual(CheckResult.Pass, LongDefeatSallyChecks.Return(2, Lord(14f, 20f), Home, 14f, 0, 0).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatSallyChecks.Return(2, Lord(24.1f, 20f), Home, 14f, 0, 0).Result);
    }

    [TestMethod]
    public void TheReturn_FailsWhenADeathWasCountedSinceTheMoveOut_OrNobodyStands()
    {
        var counted = LongDefeatSallyChecks.Return(2, Lord(10f, 20f), Home, 14f, 0, 1);

        Assert.AreEqual(CheckResult.Fail, counted.Result);
        StringAssert.Contains(counted.Detail, "deaths=0->1");
        Assert.AreEqual(CheckResult.Fail, LongDefeatSallyChecks.Return(2, LdLordFacts.None, Home, 14f, 0, 0).Result);
    }

    [TestMethod]
    public void TheContactPass_NamesTheCountsAndTheSecond()
    {
        var verdict = LongDefeatSallyChecks.Contact(1, 12.5f, "hits_given=1 hits_taken=0 mount_hits=0 kills=0");

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        Assert.AreEqual("w=1 t=12.5 hits_given=1 hits_taken=0 mount_hits=0 kills=0", verdict.Detail);
    }

    [TestMethod]
    public void NoContact_IsASkip_NeverAFail_BecauseTheJudgeReportsItAndDoesNotRequireIt()
    {
        var verdict = LongDefeatSallyChecks.NoContact(1, "none-in-180s", 181f, "hits_given=0 hits_taken=0 mount_hits=0 kills=0");

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=none-in-180s");
    }

    [TestMethod]
    public void ASkippedSally_NamesItsReason()
    {
        var verdict = LongDefeatSallyChecks.Skipped(4, "taken-over");

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        Assert.AreEqual("w=4 reason=taken-over", verdict.Detail);
    }

    [TestMethod]
    public void ALateLossReportOnThePlannedBody_IsAFailWithItsStateAndCallback()
    {
        var verdict = LongDefeatSallyChecks.OldBodyLost(3, new LdBodyEvent("removed", "Killed", true, true));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "old_state=Killed");
        StringAssert.Contains(verdict.Detail, "callback=removed");
    }

    private static LdRemount Okay() => new(true, "Routed", false, 0.5f, "");
}
