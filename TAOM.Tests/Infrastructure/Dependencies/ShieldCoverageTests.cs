using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins PatchShield's coverage bookkeeping: which methods a pass has examined and which of them
/// carry its finalizer. One set used to hold both, so the pass line's total and the session
/// summary's "shielded N" counted every skipped method (TAOM's own, the excluded hot layers,
/// SaveShield's targets) as shielded.
/// </summary>
[TestClass]
public class ShieldCoverageTests
{
    private static readonly MethodBase Skipped1 = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!;
    private static readonly MethodBase Skipped2 = typeof(string).GetMethod(nameof(string.ToUpperInvariant), Type.EmptyTypes)!;
    private static readonly MethodBase Attached = typeof(string).GetMethod(nameof(string.ToLowerInvariant), Type.EmptyTypes)!;
    private static readonly MethodBase Unknown = typeof(string).GetMethod(nameof(string.Normalize), Type.EmptyTypes)!;

    [TestMethod]
    public void SeenCountAndAttachedCount_TwoSkippedOneAttached_ReturnThreeAndOne()
    {
        var coverage = new ShieldCoverage();
        coverage.RecordSkipped(Skipped1);
        coverage.RecordSkipped(Skipped2);
        coverage.RecordAttached(Attached);

        Assert.AreEqual(3, coverage.SeenCount, "seen counts every method a pass decided on");
        Assert.AreEqual(1, coverage.AttachedCount, "attached counts only methods carrying the finalizer");
    }

    [TestMethod]
    public void HasSeen_SkippedOrAttachedMethod_ReturnsTrue()
    {
        var coverage = new ShieldCoverage();
        coverage.RecordSkipped(Skipped1);
        coverage.RecordAttached(Attached);

        Assert.IsTrue(coverage.HasSeen(Skipped1), "a skipped method is seen, so a later pass does not revisit it");
        Assert.IsTrue(coverage.HasSeen(Attached), "an attached method is seen, so a later pass does not attach twice");
    }

    [TestMethod]
    public void HasSeen_UnrecordedMethod_ReturnsFalse()
    {
        var coverage = new ShieldCoverage();
        coverage.RecordSkipped(Skipped1);
        coverage.RecordAttached(Attached);

        Assert.IsFalse(coverage.HasSeen(Unknown), "an unrecorded method has not been seen");
    }

    [TestMethod]
    public void RecordAttachedAndRecordSkipped_SameMethodTwice_CountItOnce()
    {
        var coverage = new ShieldCoverage();
        coverage.RecordAttached(Attached);
        coverage.RecordAttached(Attached);
        coverage.RecordSkipped(Skipped1);
        coverage.RecordSkipped(Skipped1);

        Assert.AreEqual(2, coverage.SeenCount);
        Assert.AreEqual(1, coverage.AttachedCount);
    }
}
