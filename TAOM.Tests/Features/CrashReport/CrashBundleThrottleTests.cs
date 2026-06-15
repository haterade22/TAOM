using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport;

namespace TAOM.Tests.Features.CrashReport;

// Pure throttle that decides whether a crash signature gets a bundle this session.
// Drives the dedup + session-cap + cooldown policy at the HandleException chokepoint.
[TestClass]
public class CrashBundleThrottleTests
{
    private static readonly DateTime Base = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static CrashBundleThrottle Make(Func<DateTime> clock, int cap = 100, double cooldownSeconds = 30)
        => new CrashBundleThrottle(cap, TimeSpan.FromSeconds(cooldownSeconds), clock);

    [TestMethod]
    public void Admit_FirstOccurrence_ReturnsWriteBundle()
    {
        var now = Base;
        var sut = Make(() => now);

        var a = sut.Admit("sigA");

        Assert.AreEqual(CrashBundleDecision.WriteBundle, a.Decision);
        Assert.AreEqual(1, a.Occurrence);
    }

    [TestMethod]
    public void Admit_SecondOccurrenceSameSignature_ReturnsSuppressDuplicate()
    {
        var now = Base;
        var sut = Make(() => now);
        sut.Admit("sigA");

        var a = sut.Admit("sigA");

        Assert.AreEqual(CrashBundleDecision.SuppressDuplicate, a.Decision);
        Assert.AreEqual(2, a.Occurrence);
    }

    [TestMethod]
    public void Admit_AlreadyBundledSignature_StaysSuppressedAfterCooldownElapses()
    {
        var now = Base;
        var sut = Make(() => now);
        sut.Admit("sigA"); // WriteBundle

        now = now.AddSeconds(60); // well past the 30s cooldown
        var a = sut.Admit("sigA");

        Assert.AreEqual(CrashBundleDecision.SuppressDuplicate, a.Decision,
            "a signature that already got a bundle must never be re-bundled");
    }

    [TestMethod]
    public void Admit_DistinctSignatureWithinCooldown_ReturnsSuppressCooldown()
    {
        var now = Base;
        var sut = Make(() => now);
        sut.Admit("sigA"); // WriteBundle at t0

        now = now.AddSeconds(10); // inside the 30s window
        var a = sut.Admit("sigB");

        Assert.AreEqual(CrashBundleDecision.SuppressCooldown, a.Decision);
    }

    [TestMethod]
    public void Admit_DistinctSignatureAfterCooldown_ReturnsWriteBundle()
    {
        var now = Base;
        var sut = Make(() => now);
        sut.Admit("sigA"); // WriteBundle at t0

        now = now.AddSeconds(31); // past the 30s window
        var a = sut.Admit("sigB");

        Assert.AreEqual(CrashBundleDecision.WriteBundle, a.Decision);
    }

    [TestMethod]
    public void Admit_FirstBundle_NotCooldownGated()
    {
        // Clock pinned to DateTime.MinValue == the throttle's initial _lastBundleUtc.
        // Without the "first bundle is never cooldown-gated" guard, now - _lastBundleUtc
        // would be Zero < cooldown and the very first crash would be wrongly suppressed.
        var sut = Make(() => DateTime.MinValue);

        var a = sut.Admit("sigA");

        Assert.AreEqual(CrashBundleDecision.WriteBundle, a.Decision);
    }

    [TestMethod]
    public void Admit_CooldownSuppressedSignature_WritesBundleOnceCooldownElapses()
    {
        var now = Base;
        var sut = Make(() => now);
        sut.Admit("sigA"); // WriteBundle at t0

        now = now.AddSeconds(5);
        var first = sut.Admit("sigB");
        Assert.AreEqual(CrashBundleDecision.SuppressCooldown, first.Decision);

        now = now.AddSeconds(30); // 35s after sigA's bundle
        var second = sut.Admit("sigB");
        Assert.AreEqual(CrashBundleDecision.WriteBundle, second.Decision,
            "a cooldown-suppressed signature is not marked bundled, so it can still get its one bundle later");
    }

    [TestMethod]
    public void Admit_SessionCapReached_ReturnsSuppressCap()
    {
        var now = Base;
        // cooldown Zero so only the cap can block.
        var sut = Make(() => now, cap: 2, cooldownSeconds: 0);
        sut.Admit("s1"); // WriteBundle (1)
        sut.Admit("s2"); // WriteBundle (2)

        var a = sut.Admit("s3");

        Assert.AreEqual(CrashBundleDecision.SuppressCap, a.Decision);
    }

    [TestMethod]
    public void Admit_RepeatedOccurrences_IncrementOccurrenceCount()
    {
        var now = Base;
        var sut = Make(() => now);
        sut.Admit("sigA");
        sut.Admit("sigA");

        var a = sut.Admit("sigA");

        Assert.AreEqual(3, a.Occurrence);
    }

    [TestMethod]
    public void Admit_ConcurrentDistinctSignatures_NeverExceedsCap()
    {
        var now = Base;
        var sut = Make(() => now, cap: 10, cooldownSeconds: 0);
        int written = 0;

        Parallel.For(0, 200, i =>
        {
            if (sut.Admit("sig" + i).Decision == CrashBundleDecision.WriteBundle)
                Interlocked.Increment(ref written);
        });

        Assert.AreEqual(10, written, "the lock must keep concurrent admits from exceeding the session cap");
    }
}
