using System;
using System.Threading;

namespace TAOM.Features.BattleLoadDiagnostics;

// Static latch marking the "initial battle load" window — open from Mission.Initialize
// (phase 4) until the first OnMissionTick (phase 6). The per-agent equip logging
// (phase 5) is gated on this, so reinforcement spawns AFTER the battle is playable are
// not logged (the hang symptom is the initial load only).
//
// Read from a Harmony prefix on the hot agent-spawn path AND from the background stall
// watchdog thread — hence volatile/Interlocked and static (no per-call IoC.Resolve).
// Both transitions are explicitly driven (open <-> closed), so there is no sentinel/
// terminal collision of the kind the harmony-patches observation-matrix rule warns about.
public static class BattleLoadLoadingWindow
{
    private static volatile bool _isOpen;
    private static long _openedAtTicksUtc;

    public static bool IsOpen => _isOpen;

    // UTC time the window opened, or null when closed. Used by the stall watchdog to
    // compute how long the load has been stuck.
    public static DateTime? OpenedAtUtc
    {
        get
        {
            if (!_isOpen) return null;
            return new DateTime(Interlocked.Read(ref _openedAtTicksUtc), DateTimeKind.Utc);
        }
    }

    public static void Enter()
    {
        // Clear the previous load's shader reading here, so the probe's reset has exactly one call
        // site and it is the same opener as the window it measures inside. Carrying a stale
        // "still compiling" forward would let it defer the watchdog on a genuinely wedged load.
        BattleLoadRenderWaitProbe.Reset();
        // Store the timestamp BEFORE flipping _isOpen; the volatile write publishes it.
        Interlocked.Exchange(ref _openedAtTicksUtc, DateTime.UtcNow.Ticks);
        _isOpen = true;
    }

    public static void Close() => _isOpen = false;
}
