namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Ask the engine layer to spawn <paramref name="Count"/> units of a troop (or one hero) into a lane. IsGuard marks a hero's guard.</summary>
internal sealed record SpawnRequest(int RequestId, MatchSide Side, int Lane, string TroopId, int Count,
    int OwnerSlot, UnitRole Role, bool IsHero, bool IsGuard = false);
