using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Is this managed <see cref="Agent"/> still the occupant of its engine slot? The engine recycles a
/// deleted agent's index within a mission, and the deleted managed object keeps reading the recycled
/// slot through native pointers captured at creation: <c>State</c>, <c>IsActive()</c>,
/// <c>IsFadingOut()</c>, position and velocity all answer for whoever lives there now, while
/// <c>Health</c>, <c>Monster</c>, <c>Name</c> and <c>Team</c> stay the dead agent's (#592). So every
/// liveness guard reading the engine can pass for a stale handle, and a handle held across frames
/// (a bone check's target list, a behavior-tree blackboard) needs this check before it touches
/// native state. <c>Mission.FindAgentWithIndex</c> is a native lookup of the slot's CURRENT managed
/// occupant, which vanilla itself calls with indices from blows and network messages.
/// Boundary code (raw Agent, Mission.Current); game-tested per ADR-008.
/// </summary>
internal static class AgentSlotIdentity
{
    public static bool IsCurrentOccupant(Agent agent)
    {
        if (agent == null || agent.Index < 0) return false;
        Mission mission = Mission.Current;
        // A handle from an earlier mission would hand a destroyed mission pointer to native.
        if (mission == null || !ReferenceEquals(agent.Mission, mission)) return false;
        return ReferenceEquals(mission.FindAgentWithIndex(agent.Index), agent);
    }
}
