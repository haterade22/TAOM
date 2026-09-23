using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Keeps an exception's stack trace intact when a Harmony finalizer hands it back to be rethrown.
///
/// A finalizer that returns its exception makes Harmony's generated wrapper execute
/// <c>throw &lt;result&gt;</c>, and throwing an existing exception object replaces its stack trace
/// with the frames from that point outward. Every frame between the real throw site and the
/// patched method is lost. <see cref="PatchShield"/> wraps every patched method in the process, so
/// every crash that crossed one arrived at the crash reporter that way: player bundle 2d446100
/// reported a childbirth failure several calls deep as five frames ending at
/// <c>MapState.OnTick_Patch2</c>, and three different throws through the same frame would have
/// shared one crash signature.
///
/// The repair is the one .NET Framework itself uses for remoting and
/// <c>Exception.InternalPreserveStackTrace</c>: move the current trace into
/// <c>_remoteStackTraceString</c>, which the runtime keeps across a throw and prints ahead of the
/// new frames. A marker line in the style of .NET's own "End of stack trace from previous location"
/// names the method where the rethrow happened. The first frames of the original throw also go into
/// <see cref="Exception.Data"/> under <see cref="ThrowSiteDataKey"/>, because frame-based consumers
/// (<c>new StackTrace(ex)</c>, the crash signature) only ever see the segment after the last throw.
///
/// Call it on a finalizer's RETHROW path only, just before returning the exception. It is
/// idempotent per rethrow (several finalizers on one method may all call it; Harmony throws once)
/// and fails open: on a runtime without these fields the trace is simply truncated, as before.
/// Cost is paid per exception on the rethrow path, never per call of the patched method.
///
/// Between this call and the next throw the exception has no live frames: <c>TargetSite</c> is
/// null (unless something read it earlier, which caches it) and <c>new StackTrace(ex)</c> is empty,
/// though the text is intact. Harmony runs a method's finalizers highest priority first, then in
/// the order they were added, so anything that reads frames from a finalizer on the same method must
/// run before PatchShield's. The crash reporter's Patch37 finalizers outrank it at 800 (pinned by
/// <c>CrashReporterFinalizers_OutrankPatchShield</c>); its Native2Managed bridge finalizers are at
/// the default priority and run first only because they are added before PatchShield's pass 2.
/// </summary>
public static class RethrowStackPreserver
{
    /// <summary>
    /// <see cref="Exception.Data"/> key holding the first frames of the original throw, innermost
    /// first, joined by " &lt;- ". Recorded once, by the innermost rethrow.
    /// </summary>
    public const string ThrowSiteDataKey = "TAOM.ThrowSite";

    /// <summary>How many frames of the original throw the recorded site keeps.</summary>
    internal const int ThrowSiteFrameCount = 5;

    private static readonly FieldInfo? RemoteStackTraceField = ExceptionField("_remoteStackTraceString");
    private static readonly FieldInfo? StackTraceField = ExceptionField("_stackTrace");
    private static readonly FieldInfo? StackTraceStringField = ExceptionField("_stackTraceString");

    /// <param name="exception">The exception the finalizer is about to return.</param>
    /// <param name="rethrowSite">The patched (original) method whose wrapper will rethrow it.</param>
    /// <returns>
    /// <paramref name="exception"/> itself, so a finalizer can write
    /// <c>return RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);</c>.
    /// Never a wrapper: the type a caller above the shield catches must not change.
    /// </returns>
    public static Exception? PreserveForRethrow(Exception? exception, MethodBase? rethrowSite)
    {
        if (exception == null || RemoteStackTraceField == null || StackTraceField == null) return exception;

        try
        {
            // Null until the exception is first thrown, and null again after an earlier call here
            // until the next throw repopulates it. Either way there is nothing new to keep.
            if (StackTraceField.GetValue(exception) == null) return exception;

            RecordThrowSite(exception);

            // The getter returns the already-preserved part followed by the current frames.
            var trace = exception.StackTrace;
            if (string.IsNullOrEmpty(trace)) return exception;

            RemoteStackTraceField.SetValue(
                exception,
                trace + Environment.NewLine + Marker(rethrowSite) + Environment.NewLine);

            // Cleared exactly as InternalPreserveStackTrace clears them: the next throw writes fresh
            // frames, and if no throw follows (a later finalizer swallows) the object still reads
            // without the current frames printed twice.
            StackTraceField.SetValue(exception, null);
            StackTraceStringField?.SetValue(exception, null);
        }
        catch
        {
            // Fail open: the worst case is the truncated trace this class exists to prevent.
        }
        return exception;
    }

    private static void RecordThrowSite(Exception exception)
    {
        try
        {
            var data = exception.Data;
            if (data == null || data.IsReadOnly || data.Contains(ThrowSiteDataKey)) return;

            var frames = new StackTrace(exception, fNeedFileInfo: false).GetFrames();
            if (frames == null || frames.Length == 0) return;

            var site = new StringBuilder();
            for (int i = 0; i < frames.Length && i < ThrowSiteFrameCount; i++)
            {
                if (i > 0) site.Append(" <- ");
                site.Append(Describe(frames[i].GetMethod()));
            }
            data[ThrowSiteDataKey] = site.ToString();
        }
        catch
        {
            // Data can be read-only or throw on exotic exception types; the trace still gets kept.
        }
    }

    private static string Marker(MethodBase? rethrowSite)
        => "   --- End of stack trace from previous location (rethrown by a Harmony finalizer on "
           + (rethrowSite == null ? "an unknown method" : Describe(rethrowSite)) + ") ---";

    /// <summary>
    /// Harmony's replacement methods are dynamic and have no declaring type, but their name already
    /// carries the original's full type name (<c>TaleWorlds...MapState.OnTick_Patch2</c>).
    /// </summary>
    private static string Describe(MethodBase? method)
    {
        if (method == null) return "?";
        var type = method.DeclaringType?.FullName;
        return type == null ? method.Name : type + "." + method.Name;
    }

    private static FieldInfo? ExceptionField(string name)
    {
        try { return typeof(Exception).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); }
        catch { return null; }
    }
}
