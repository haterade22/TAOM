using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Collectors;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class StackFrameSnapshotBuilderTests
{
    [TestMethod]
    public void FromException_NullException_ReturnsEmpty()
    {
        var frames = StackFrameSnapshotBuilder.FromException(null);
        Assert.AreEqual(0, frames.Count);
    }

    [TestMethod]
    public void FromException_ThrownException_CapturesFrames()
    {
        Exception caught;
        try { ThrowingMethod(); throw new Exception("unreachable"); }
        catch (Exception ex) { caught = ex; }

        var frames = StackFrameSnapshotBuilder.FromException(caught);

        Assert.IsTrue(frames.Count > 0, "expected at least one frame");
        Assert.AreEqual(0, frames[0].Index, "first frame index is 0");
        Assert.IsNotNull(frames[0].MethodFullName);
    }

    private static void ThrowingMethod() => throw new InvalidOperationException("test");
}
