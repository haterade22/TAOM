using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// The capture both stack samplers share (#331, #634). The suspend-and-walk itself only runs against a
/// frozen game, so these pin its guards and the line format the triage reader greps for.
/// </summary>
[TestClass]
public class ThreadStackCaptureTests
{
    [TestMethod]
    public void Capture_NullThread_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ThreadStackCapture.Capture(null!));
    }

    [TestMethod]
    public void Capture_AThreadThatHasEnded_ThrowsWithoutSuspendingAnything()
    {
        var finished = new Thread(() => { });
        finished.Start();
        finished.Join();

        Assert.ThrowsException<InvalidOperationException>(() => ThreadStackCapture.Capture(finished));
    }

    [TestMethod]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void FormatFrames_OneAtLinePerFrame_NamingTypeAndMethod()
    {
        var stack = new StackTrace(false);

        string text = ThreadStackCapture.FormatFrames(stack);

        string[] lines = text.Split('\n');
        Assert.AreEqual(stack.FrameCount, lines.Length);
        StringAssert.StartsWith(lines[0], "    at ");
        StringAssert.Contains(lines[0], $"{typeof(ThreadStackCaptureTests).FullName}.{nameof(FormatFrames_OneAtLinePerFrame_NamingTypeAndMethod)}");
    }
}
