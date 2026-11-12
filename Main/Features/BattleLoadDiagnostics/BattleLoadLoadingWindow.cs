namespace TAOM.Features.BattleLoadDiagnostics;

// Static latch marking the "initial battle load" window — open from Mission.Initialize
// (phase 4) until the first OnMissionTick (phase 6). The per-agent equip logging
// (phase 5) is gated on this, so reinforcement spawns AFTER the battle is playable are
// not logged (the hang symptom is the initial load only).
//
// Read from a Harmony prefix on the hot agent-spawn path — hence volatile and static
// (no per-call IoC.Resolve).
// Both transitions are explicitly driven (open <-> closed), so there is no sentinel/
// terminal collision of the kind the harmony-patches observation-matrix rule warns about.
public static class BattleLoadLoadingWindow
{
    private static volatile bool _isOpen;

    public static bool IsOpen => _isOpen;

    public static void Enter() => _isOpen = true;

    public static void Close() => _isOpen = false;
}
