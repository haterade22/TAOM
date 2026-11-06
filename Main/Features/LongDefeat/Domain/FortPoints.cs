using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

// A volley origin or a tower volley spot is an LdPoint (MusterPlan.cs): a place with no facing. The arrow leaves a configured height above it.

/// <summary>
/// One recorded high tower of a fortress: <paramref name="Posts"/> are the spots its posted archers hold (the muster point's shape, a facing included),
/// <paramref name="VolleyOrigins"/> the spots its level 3 volley looses from. Either list may be empty, but a tower is kept only with at least one point.
/// </summary>
internal sealed record LdTower(string Id, IReadOnlyList<MusterFrame> Posts, IReadOnlyList<LdPoint> VolleyOrigins);

/// <summary>The recorded origins of the Elven Volley along the walls of a fortress; never empty.</summary>
internal sealed record LdVolleyOrigins(IReadOnlyList<LdPoint> Origins);
