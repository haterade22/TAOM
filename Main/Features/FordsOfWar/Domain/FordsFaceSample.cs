namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One live Fords unit's footing as the battlefield adapter measured it: the navigation face group it stands on (-1 when it
/// stands on none), whether it rides, how far it stands above the terrain and how far under the navmesh surface (NaN when the
/// engine has no navmesh height there). Info only: no rule reads it.
/// </summary>
internal readonly record struct FordsFaceSample(MatchSide Side, int Lane, int FaceId, bool Mounted, float AboveTerrain, float BelowNav);
