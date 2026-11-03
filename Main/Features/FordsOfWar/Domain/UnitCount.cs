namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Role is for a troop no offer of the faction names, so it still counts as what it is; null reads the role from its offer.</summary>
internal sealed record UnitCount(string TroopId, int Count, UnitRole? Role = null);
