using System;
using System.Runtime.CompilerServices;

namespace TAOM.Tests.Features.CrashReport;

// Proves a crash-capture finalizer's handed-back exception keeps its throw site once Harmony
// rethrows it. A value-returning finalizer makes Harmony's wrapper execute `throw <result>`,
// which replaces the trace unless RethrowStackPreserver moved it aside first
// (harmony-patches.md, lessons/harmony-il.md).
internal static class RethrowProbe
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowAtTheSite() => throw new InvalidOperationException("taom-006 throw site");

    // A thrown and caught exception, so it has live frames naming ThrowAtTheSite.
    internal static Exception CaughtFromTheSite()
    {
        try { ThrowAtTheSite(); }
        catch (InvalidOperationException caught) { return caught; }
        throw new InvalidOperationException("ThrowAtTheSite did not throw");
    }

    // What Harmony's wrapper does with a finalizer's non-null result: throw the same object.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string TraceAfterHarmonyRethrow(Exception handedBack)
    {
        try { throw handedBack; }
        catch (Exception rethrown) { return rethrown.StackTrace ?? string.Empty; }
    }
}
