using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>One 1 Hz look at the field, taken by the engine layer and merged into the run evidence.</summary>
internal sealed record FordsFieldSample(IReadOnlyList<FordsLaneSample> Lanes, IReadOnlyList<FordsTowerSample> Towers,
    IReadOnlyList<FordsPointSample> Points, int LiveWest, int LiveEast, int Backlog);

/// <summary>
/// One stream in a 1 Hz look. <see cref="AttackTarget"/> is the standing tower the stream's applied command is an AttackTower order
/// at (null when it has none); then <see cref="Flagged"/> of its live units carry AttackEntity and <see cref="Contact"/> melee units
/// stand within 4 m of that tower. <see cref="MarchTower"/> is the next standing enemy tower of a stream whose applied command is a
/// MoveTo (null when it has none, or none is left), and <see cref="MarchPast"/> how many of its live units stand more than 10 m past
/// that tower along the lane.
/// </summary>
internal sealed record FordsLaneSample(MatchSide Side, int Lane, int Live, float Front, bool HoldSettled, int InFire,
    float HoldFront, string? FireTower = null, int Alarmed = 0, string? AttackTarget = null, int Flagged = 0, int Contact = 0,
    string? MarchTower = null, int MarchPast = 0);
