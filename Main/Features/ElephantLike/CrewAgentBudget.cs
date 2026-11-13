namespace TAOM.Features.ElephantLike;

/// <summary>
/// The capacity check for a direct <c>Mission.SpawnAgent</c> call (#1107), which has no check of its own. At a full pool
/// the engine's slot search returns -1 and the native spawn writes through that index over the mission's own memory.
/// Vanilla's reinforcements read the live count before they spawn; its initial spawn relies on the battle size cap.
/// The check answers for one slot, which fits a crew member, because it spawns with <c>NoHorses(true)</c>.
/// </summary>
public static class CrewAgentBudget
{
    /// <summary>True when one more agent fits under the engine's limit.</summary>
    public static bool HasRoom(int agentsNow, int maxAgents) => agentsNow < maxAgents;
}
