namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Which missions run the stamina rules (#1497). An allowlist: a field battle, a siege, a sally-out, and the Fords of
/// War match (built with no team AI, so the three battle tests are false for it; it is the unattended test battle).
/// Arenas and tournaments (not a Combat mission), hideouts, towns and conversations (no team AI) are out by
/// construction. Its own type so the MissionLogic stays a thin entry point (ADR-002) and the rule has unit tests. It
/// takes plain values, so it needs no Mission.
/// </summary>
public static class StaminaMissionGate
{
    public static bool IsArmable(bool enabled, bool fordsMatch, bool combat, bool fieldBattle, bool siege, bool sallyOut) =>
        enabled && (fordsMatch || (combat && (fieldBattle || siege || sallyOut)));
}
