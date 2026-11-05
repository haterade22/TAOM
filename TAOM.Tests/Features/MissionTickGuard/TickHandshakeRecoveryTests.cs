using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionTickGuard;

namespace TAOM.Tests.Features.MissionTickGuard;

/// <summary>
/// The decision cells of Patch91b's state machine (port decision D17): a restore happens only when
/// the call entered armed with the flag set, threw, launched no agent tick and left the flag false.
/// </summary>
[TestClass]
public class TickHandshakeRecoveryTests
{
    private static TickHandshakeRecovery Armed()
    {
        var recovery = new TickHandshakeRecovery();
        Assert.IsTrue(recovery.Arm(fieldResolved: true, launchMarkInstalled: true, recoveryInstalled: true));
        return recovery;
    }

    [TestMethod]
    public void Exit_WhenNeverArmed_NeverRestores()
    {
        var recovery = new TickHandshakeRecovery();
        bool s = recovery.Enter(true);
        Assert.IsFalse(s);
        Assert.AreEqual(RecoveryDecision.None, recovery.Exit(s, threw: true, tickCompletedNow: false));
    }

    [TestMethod]
    public void Arm_WithAnyPieceMissing_StaysOff()
    {
        var recovery = new TickHandshakeRecovery();
        Assert.IsFalse(recovery.Arm(false, true, true));
        Assert.IsFalse(recovery.Armed);
        Assert.IsFalse(recovery.Arm(true, false, true));
        Assert.IsFalse(recovery.Armed);
        Assert.IsFalse(recovery.Arm(true, true, false));
        Assert.IsFalse(recovery.Armed);
    }

    [TestMethod]
    public void Exit_ThrowAfterTheClearWithNoLaunch_RestoresAndLogsTheFirst()
    {
        var recovery = Armed();
        bool s = recovery.Enter(true);
        Assert.AreEqual(RecoveryDecision.RestoreAndLog, recovery.Exit(s, true, false));
        Assert.AreEqual(1, recovery.RestoreCount);
    }

    [TestMethod]
    public void Exit_ThrowAfterTheLaunch_NeverRestores()
    {
        var recovery = Armed();
        bool s = recovery.Enter(true);
        recovery.MarkLaunch();
        Assert.AreEqual(RecoveryDecision.None, recovery.Exit(s, true, false));
        Assert.AreEqual(0, recovery.RestoreCount);
    }

    [TestMethod]
    public void Enter_WithTheFlagAlreadyFalse_DisarmsTheCall()
    {
        var recovery = Armed();
        bool s = recovery.Enter(false);
        Assert.IsFalse(s);
        Assert.AreEqual(RecoveryDecision.None, recovery.Exit(s, true, false));
    }

    [TestMethod]
    public void Exit_WithoutAnException_DoesNothing()
    {
        var recovery = Armed();
        bool s = recovery.Enter(true);
        Assert.AreEqual(RecoveryDecision.None, recovery.Exit(s, false, false));
    }

    [TestMethod]
    public void Exit_WithTheFlagAlreadyTrue_DoesNothing()
    {
        var recovery = Armed();
        bool s = recovery.Enter(true);
        Assert.AreEqual(RecoveryDecision.None, recovery.Exit(s, true, true));
    }

    [TestMethod]
    public void TheLaunchMark_LastsOneCall()
    {
        var recovery = Armed();
        bool s1 = recovery.Enter(true);
        recovery.MarkLaunch();
        recovery.Exit(s1, false, false);

        bool s2 = recovery.Enter(true);
        Assert.AreEqual(RecoveryDecision.RestoreAndLog, recovery.Exit(s2, true, false));
    }

    [TestMethod]
    public void Exit_ClearsTheLaunchMark_EvenWhenTheCallWasNotArmed()
    {
        var recovery = Armed();
        recovery.MarkLaunch();
        Assert.AreEqual(RecoveryDecision.None, recovery.Exit(false, true, false));
        Assert.AreEqual(RecoveryDecision.RestoreAndLog, recovery.Exit(true, true, false));
    }

    [TestMethod]
    public void ShouldLog_FirstSecondAndPowersOfTen()
    {
        foreach (long yes in new long[] { 1, 2, 10, 100, 1000 })
            Assert.IsTrue(TickHandshakeRecovery.ShouldLog(yes), $"occurrence {yes} must log");
        foreach (long no in new long[] { 0, 3, 9, 11, 20, 99, 101 })
            Assert.IsFalse(TickHandshakeRecovery.ShouldLog(no), $"occurrence {no} must not log");
    }

    [TestMethod]
    public void Exit_CountsEveryRestore_AndLogsOnlyTheThrottledOnes()
    {
        var recovery = Armed();
        for (int call = 1; call <= 10; call++)
        {
            bool s = recovery.Enter(true);
            var expected = call <= 2 || call == 10 ? RecoveryDecision.RestoreAndLog : RecoveryDecision.Restore;
            Assert.AreEqual(expected, recovery.Exit(s, true, false), $"call {call}");
        }
        Assert.AreEqual(10, recovery.RestoreCount);
    }
}
