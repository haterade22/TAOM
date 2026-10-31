namespace TAOM.Features.FordsOfWar;

/// <summary>What a neutral camp holds and pays when the generated layout places one (the engine spawn is a later phase).</summary>
internal sealed record CampDefaults(string TroopId, int Count, int Reward)
{
    public static CampDefaults Default { get; } = new("hill_troll", 2, 250);
}
