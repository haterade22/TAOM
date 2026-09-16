using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// Which missions may carry signature strikes. Its own type so the MissionLogic stays a thin
/// entry point (ADR-002) and the rule is readable in one place.
///
/// Narrower than DreadMissionGate in one way and wider in another: it does NOT require a campaign
/// (Custom Battle is the cheap smoke route, and the ring needs no campaign context), but it does
/// exclude arenas and tournaments (a slam in a tournament ring would flatten the other entrants,
/// the failure family <c>rca-tournament-exit-hang-2026-07-06.md</c> carries a patch for) and any
/// multiplayer session.
/// </summary>
public static class SignatureMissionGate
{
    public static bool IsEligible(Mission? mission)
    {
        if (mission == null)
            return false;

        if (GameNetwork.IsSessionActive)
            return false;

        // ArenaCombat covers arenas and tournaments; NoCombat covers conversations and walkarounds.
        return mission.CombatType == Mission.MissionCombatType.Combat;
    }
}
