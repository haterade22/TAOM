namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// A spawned agent as the match tracks it. Key is the engine layer's opaque id (never Agent.Index), Value feeds the kill
/// bounty, HeroId is the NPCCharacter id for a hero and null for a troop.
/// </summary>
internal sealed record UnitRecord(AgentKey Key, MatchSide Side, int Lane, int OwnerSlot, int Value, UnitRole Role, string? HeroId);
