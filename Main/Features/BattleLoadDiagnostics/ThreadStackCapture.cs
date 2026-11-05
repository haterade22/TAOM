using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Photographs another thread's managed stack from a timer thread: the one way a frozen game can still
/// name the method it is stuck in. Shared by <see cref="ExitStallSampler"/> (#331) and
/// <see cref="MissionTickStallWatchdog"/> (#634).
///
/// Thread.Suspend/Resume and StackTrace(Thread, bool) are obsolete-as-WARNING on net472 but present in
/// both the reference assemblies and the runtime. Known residual risk: suspending a thread mid-GC and
/// then allocating can deadlock before Resume, so NOTHING between Suspend and Resume may log or
/// allocate beyond the walk itself (Codex round-2 P3). Callers only sample a thread that has already
/// been stuck for seconds.
/// </summary>
internal static class ThreadStackCapture
{
    /// <summary>
    /// Suspends <paramref name="thread"/>, walks its stack and resumes it. Throws when the thread is
    /// gone or the walk fails; the thread is resumed first either way.
    /// </summary>
    public static StackTrace Capture(Thread thread)
    {
        if (thread == null) throw new ArgumentNullException(nameof(thread));
        if (!thread.IsAlive) throw new InvalidOperationException($"thread {thread.ManagedThreadId} is not alive");

#pragma warning disable CS0618
        thread.Suspend();
        try
        {
            return new StackTrace(thread, needFileInfo: false);
        }
        finally
        {
            try { thread.Resume(); }
            catch { /* resume must never throw out */ }
        }
#pragma warning restore CS0618
    }

    /// <summary>One "    at Type.Method" line per frame. Call after the thread is resumed.</summary>
    public static string FormatFrames(StackTrace stack)
    {
        var sb = new StringBuilder(2048);
        for (int i = 0; i < stack.FrameCount; i++)
        {
            var method = stack.GetFrame(i)?.GetMethod();
            if (i > 0) sb.Append('\n');
            sb.Append("    at ");
            if (method == null) sb.Append("<unknown>");
            else sb.Append(method.DeclaringType?.FullName).Append('.').Append(method.Name);
        }
        return sb.ToString();
    }
}
