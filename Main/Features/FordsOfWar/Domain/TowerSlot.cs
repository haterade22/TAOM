namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Where a tower or keep stands. Order 0 is the outermost tower (nearest the middle); keeps use Lane -1 and Order 99.
/// Yaw is radians in the ground plane, atan2(y, x), pointing the way the owner's troops march. <see cref="Authored"/> is a tower the
/// scene already built (its marker entity has a body): the fitter keeps it where it stands and the adapter attaches the damage
/// component to that entity instead of building a barricade.
/// </summary>
internal sealed record TowerSlot(string Id, MatchSide Owner, int Lane, int Order, TowerKind Kind, Vec3f Position, float Yaw, bool Authored = false);
