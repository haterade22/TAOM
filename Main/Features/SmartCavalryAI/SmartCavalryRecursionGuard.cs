using System;

namespace TAOM.Features.SmartCavalryAI;

/// <summary>
/// Thread-local depth counter the <c>CavalryCommandAdapter</c> raises around any
/// <c>Formation.SetMovementOrder</c> call that the SmartCavalryAI feature itself issues.
/// The Patch31 postfix checks <see cref="IsSuppressed"/> and short-circuits when set,
/// preventing infinite recursion when the service responds to a charge order by issuing
/// further movement orders (Stop, Move-to-waypoint, ChargeToTarget against original target).
///
/// <para>Counter (not bool) so nested <see cref="Enter"/> scopes are safe — the inner
/// dispose decrements rather than clearing. <see cref="Reset"/> is a defensive escape
/// hatch called from <c>OnEndMission</c> so an abnormal mission termination during a
/// suppressed callstack doesn't permanently disable the feature on the next mission.</para>
/// </summary>
public static class SmartCavalryRecursionGuard
{
    [ThreadStatic]
    private static int _depth;

    public static bool IsSuppressed => _depth > 0;

    public static IDisposable Enter() => new Scope();

    /// <summary>Force-clears the depth counter. Called from <c>SmartCavalryAIMissionBehavior.OnEndMission</c>
    /// to recover from any abnormal termination that bypassed the <c>using</c> dispose.</summary>
    public static void Reset() => _depth = 0;

    private sealed class Scope : IDisposable
    {
        public Scope() { _depth++; }
        public void Dispose() { if (_depth > 0) _depth--; }
    }
}
