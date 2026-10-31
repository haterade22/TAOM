namespace TAOM.Features.FordsOfWar;

/// <summary>What the player chose in the main-menu flow: the scene, the two factions, the AI difficulty and the lane count.</summary>
internal sealed record FordsMatchSetup(string SceneName, string WestFaction, string EastFaction, string AiProfile, int LaneCount);
