namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One blow on a tower as the engine reports it, before the damage table is applied. <paramref name="AttackerSide"/> is the side
/// of whoever struck it; null when the director could not tell, which leaves the blow to the table as before.
/// </summary>
internal sealed record TowerHit(string TowerId, bool Ranged, UnitRole AttackerRole, int RawDamage, MatchSide? AttackerSide = null);
