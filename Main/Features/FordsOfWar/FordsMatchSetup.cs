namespace TAOM.Features.FordsOfWar;

/// <summary>
/// What the player chose in the main-menu flow: the scene, the two factions, the AI difficulty and the lane count. WestAi puts an AI
/// on the West side as well; only an unattended autorun sets it.
/// </summary>
internal sealed record FordsMatchSetup(string SceneName, string WestFaction, string EastFaction, string AiProfile, int LaneCount,
    bool WestAi = false);
