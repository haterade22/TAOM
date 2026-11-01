using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>Who struck a tower or a tower's gunner: its side, and its lane when it is a lane unit (-1 for any other agent, such as a gunner).</summary>
internal readonly record struct FordsAttacker(MatchSide Side, int Lane = -1);
