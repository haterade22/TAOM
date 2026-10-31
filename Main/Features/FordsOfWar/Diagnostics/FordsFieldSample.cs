using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>One 1 Hz look at the field, taken by the engine layer and merged into the run evidence.</summary>
internal sealed record FordsFieldSample(IReadOnlyList<FordsLaneSample> Lanes, IReadOnlyList<FordsTowerSample> Towers,
    IReadOnlyList<FordsPointSample> Points, int LiveWest, int LiveEast, int Backlog);

internal sealed record FordsLaneSample(MatchSide Side, int Lane, int Live, float Front, bool HoldSettled, int InFire,
    float HoldFront, string? FireTower = null);
