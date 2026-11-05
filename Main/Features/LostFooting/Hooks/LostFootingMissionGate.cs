using TaleWorlds.MountAndBlade;
using TAOM.Features.CoopInterop;
using TAOM.Features.MountDespawn.Hooks;

namespace TAOM.Features.LostFooting.Hooks;

/// <summary>
/// Which missions the Lost Footing census reads: the field battles, sieges and sally-outs of
/// <see cref="MountDespawnMissionGate"/>'s allowlist, never in a co-op session or where a co-op host owns the
/// battle, and never once the mission has ended. On the 1.5.x line the census shares Fell Foes' gate, which is
/// this same expression; Fell Foes is not on the 1.4.x line, so the census owns it here (plan 117).
/// </summary>
public static class LostFootingMissionGate
{
    public static bool IsEligible(Mission mission, ICoopSessionProvider coop) =>
        !(coop.IsSessionActive || coop.ShouldDeferToHost) && MountDespawnMissionGate.IsEligible(mission) && !mission.MissionEnded;
}
