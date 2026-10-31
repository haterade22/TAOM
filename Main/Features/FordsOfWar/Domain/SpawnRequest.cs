namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Ask the engine layer to spawn <paramref name="Count"/> units of a troop (or one hero) into a lane.</summary>
internal sealed record SpawnRequest(int RequestId, MatchSide Side, int Lane, string TroopId, int Count,
    int OwnerSlot, UnitRole Role, bool IsHero);
