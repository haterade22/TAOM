using System.Threading;

namespace TAOM.Features.MissionTickGuard;

/// <summary>What the Mission.OnTick finalizer must do after one call (Patch91b).</summary>
public enum RecoveryDecision { None, Restore, RestoreAndLog }

/// <summary>
/// Patch91b's state machine (port decision D17). Pure: no engine types. v1.4.8 Mission.OnTick clears
/// tickCompleted (Mission.cs:3740), runs every OnMissionTick, then starts the agent tick (:3768-3775);
/// only the agent tick sets the flag again (:3613) and the next frame spins until it does
/// (WaitTickCompletion, :3585-3591). Restoring is safe only when the call began with the flag set,
/// cleared it and launched no agent tick: then no job is in flight. Main thread only.
/// </summary>
public sealed class TickHandshakeRecovery
{
    public static TickHandshakeRecovery Instance { get; } = new TickHandshakeRecovery();

    private bool _launchedThisCall;
    private long _restores;

    internal TickHandshakeRecovery() { }

    public bool Armed { get; private set; }

    public long RestoreCount => Interlocked.Read(ref _restores);

    /// <summary>Arms only when the private flag resolved and both patches are verified in place.</summary>
    public bool Arm(bool fieldResolved, bool launchMarkInstalled, bool recoveryInstalled)
    {
        Armed = fieldResolved && launchMarkInstalled && recoveryInstalled;
        return Armed;
    }

    /// <summary>Mission.OnTick prefix. Returns Harmony's __state: true when this call may be recovered.</summary>
    public bool Enter(bool tickCompletedAtEntry)
    {
        _launchedThisCall = false;
        return Armed && tickCompletedAtEntry;
    }

    /// <summary>TickAgentsAndTeamsAsync finalizer: this OnTick call reached the agent-tick launch.</summary>
    public void MarkLaunch() => _launchedThisCall = true;

    /// <summary>Mission.OnTick finalizer, on every call. Always clears the launch mark.</summary>
    public RecoveryDecision Exit(bool enteredArmed, bool threw, bool tickCompletedNow)
    {
        bool launched = _launchedThisCall;
        _launchedThisCall = false;
        if (!enteredArmed || !threw || launched || tickCompletedNow) return RecoveryDecision.None;

        long count = Interlocked.Increment(ref _restores);
        return ShouldLog(count) ? RecoveryDecision.RestoreAndLog : RecoveryDecision.Restore;
    }

    /// <summary>Occurrences 1 and 2, then every power of ten (10, 100, 1000...).</summary>
    internal static bool ShouldLog(long occurrence)
    {
        if (occurrence <= 2) return occurrence >= 1;
        while (occurrence % 10 == 0) occurrence /= 10;
        return occurrence == 1;
    }
}
