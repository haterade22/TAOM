namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One blow on a tower as the engine reports it, before the damage table is applied. <paramref name="AttackerSide"/> is the side
/// of whoever struck it; null when the director could not tell, which leaves the blow to the table as before.
/// <paramref name="ThrustDamage"/> is the thrust damage of the weapon's current usage (0 for none); the table reads it only to floor a
/// melee blow whose raw damage the engine truncated to 0.
/// </summary>
internal sealed record TowerHit(string TowerId, bool Ranged, UnitRole AttackerRole, int RawDamage, MatchSide? AttackerSide = null,
    int ThrustDamage = 0);
