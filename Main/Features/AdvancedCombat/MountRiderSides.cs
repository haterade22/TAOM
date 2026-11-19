using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// The side a mount keeps once its rider is gone (#594). The engine never gives a mount a team:
/// <c>Mission.SpawnAgent</c> sets the team of the rider only (v1.5.7 Mission.cs:4209) and builds the mount
/// through <c>CreateHorseAgentFromRosterElements</c> (:4602) with no team and no origin. So the rider's team is
/// recorded against his mount when he is built on it and each time he mounts one; the last write is the last
/// rider's. Keyed by the <see cref="Agent"/> object, never its index (the engine recycles indices, #592). A
/// <see cref="ConditionalWeakTable{TKey,TValue}"/> lets a dead mount's entry go with it and is safe from the
/// mount callbacks native may raise off the main thread (#634).
/// Boundary code (raw Agent and Team); the bites read it only through AgentAdapter.
/// </summary>
internal static class MountRiderSides
{
    private sealed class Side
    {
        public volatile Team? Team;
    }

    private static readonly ConditionalWeakTable<Agent, Side> Sides = new();

    /// <summary>Records <paramref name="riderTeam"/> as <paramref name="mount"/>'s side. A null either way changes nothing.</summary>
    public static void Record(Agent? mount, Team? riderTeam)
    {
        if (mount == null || riderTeam == null) return;
        Sides.GetOrCreateValue(mount).Team = riderTeam;
    }

    /// <summary>Records a rider's team against the mount he sits on, if any.</summary>
    public static void RecordRider(Agent? rider)
    {
        if (rider == null) return;
        Record(rider.MountAgent, rider.Team);
    }

    /// <summary>
    /// The team that stands for <paramref name="agent"/>: its own, else the last team recorded for it as a mount,
    /// else null. Only mounts are ever recorded, so a soldier answers with his own team or null.
    /// </summary>
    public static Team? SideOf(Agent? agent)
    {
        if (agent == null) return null;
        if (agent.Team != null) return agent.Team;
        return Sides.TryGetValue(agent, out var side) ? side.Team : null;
    }
}
