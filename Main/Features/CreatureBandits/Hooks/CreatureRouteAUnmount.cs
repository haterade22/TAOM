using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// Route A's last wiring step (#692): makes a built creature an ordinary enemy to soldiers' native target selection.
/// Native IsEnemy reads a Mountable agent's side from its rider, so a riderless creature is nobody's enemy and no soldier
/// ever picks it; with Mountable cleared it reads the creature's own team, which SetTeam has written. Every precondition
/// is checked by <see cref="CreatureBanditRules.RouteA"/> first, and any miss leaves today's untargetable creature.
///
/// The creature leaves Mission.MountsWithoutRiders in the same step: once it is not a mount, OnAgentRemoved no longer
/// unlists it (<c>Mission.cs:3041-3044</c>), and HumanAIComponent's mount search would walk a cleared agent. Unlisting
/// comes first (a search-and-remove, safe when absent), then the flag change; if the flag change throws, the still
/// Mountable creature is listed again. Main thread only: the list is the one HumanAIComponent iterates.
/// </summary>
internal static class CreatureRouteAUnmount
{
    private static readonly FieldInfo? WieldIndexPointer = AccessTools.Field(typeof(Agent), "_primaryWieldedItemIndexPointer");

    /// <summary>Whether the native weapon state exists, read without a dereference (a missing field reads as absent).</summary>
    internal static bool HasWeaponState(Agent agent)
    {
        if (WieldIndexPointer?.GetValue(agent) is not UIntPtr pointer) return false;
        return CreatureBanditRules.IsWeaponStateAllocated(pointer.ToUInt64());
    }

    internal static CreatureRouteA Apply(Mission mission, Agent agent, bool teamSet, Action beforeUnmount)
    {
        var decision = CreatureBanditRules.RouteA(HasWeaponState(agent),
            (agent.GetAgentFlags() & AgentFlag.CanWieldWeapon) != 0, teamSet, agent.Monster?.PelvisBoneIndex >= 0);
        if (decision != CreatureRouteA.On) return decision;

        beforeUnmount();
        mission.RemoveMountWithoutRider(agent);
        try
        {
            agent.SetAgentFlags(CreatureBanditRules.CombatantFlags(agent.GetAgentFlags()));
        }
        catch
        {
            if (agent.IsMount) mission.AddMountWithoutRider(agent);
            throw;
        }
        return decision;
    }
}
