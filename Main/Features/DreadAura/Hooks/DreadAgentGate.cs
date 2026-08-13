using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// The single chokepoint for "may dread touch this agent". Its own type, next to
/// <see cref="DreadMissionGate"/>: one answers which missions, this one answers which agents.
/// </summary>
public static class DreadAgentGate
{
    /// <summary>
    /// True only for a live AI-controlled human that actually has morale to drain.
    ///
    /// <c>CommonAIComponent</c> is attached by <c>AgentCommonAILogic.OnAgentCreated</c> ONLY when
    /// the agent is AI-controlled, so the player and every non-AI agent has none. Writing to one
    /// of those does not crash: <c>AgentComponentExtensions.ChangeMorale</c> null-checks the
    /// component and silently no-ops, and <c>GetMorale</c> returns a <c>-1f</c> sentinel. The gate
    /// exists to keep that sentinel out of the arithmetic, where it must read as "no morale", never
    /// as "already drained".
    ///
    /// The player being immune is a property of the engine, not a decision of ours. It is the
    /// right outcome: do not "fix" it. Mounts are a separate, genuinely load-bearing clause —
    /// they ARE AI-controlled and DO carry the component, so without it a wraith would drain the
    /// morale of the enemy's horses.
    ///
    /// The null check is load-bearing too: <c>Mission.GetNearbyAgentsAux</c> adds its
    /// <c>DotNetObject.GetManagedObjectWithId(id) as Agent</c> result unconditionally, so a
    /// reclaimed id arrives in the caller's buffer as a null entry.
    /// </summary>
    public static bool CanAffect(Agent agent)
        => agent != null
        && agent.IsActive()
        && agent.IsHuman
        && !agent.IsMount
        && agent.IsAIControlled
        && agent.CommonAIComponent != null
        && agent.GetMorale() >= 0f;
}
