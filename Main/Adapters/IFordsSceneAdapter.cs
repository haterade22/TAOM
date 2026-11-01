using System.Collections.Generic;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>The scene under a Fords of War mission: its boundary, its ground and navmesh, and the markers of an authored layout.</summary>
internal interface IFordsSceneAdapter
{
    /// <summary>
    /// The two base points from the mission boundary: along the longest axis of the playable polygon, or its shorter one when
    /// <paramref name="shortSide"/> is set (for a near-square boundary), <paramref name="insetFraction"/> of the length in from each
    /// end, snapped to the ground and moved onto the navmesh when needed. A base off the navmesh is moved along the base-to-base
    /// axis, and the raw and settled positions are logged. False without a usable boundary.
    /// </summary>
    bool TryGetBasePoints(float insetFraction, bool shortSide, out Vec3f westBase, out Vec3f eastBase);

    /// <summary>The playable polygon, for the commander camera. Empty when the scene has none.</summary>
    IReadOnlyList<Vec3f> BoundaryPolygon();

    /// <summary>
    /// Snaps every layout point to the ground and checks it against the navmesh, and every lane segment against the navmesh's
    /// straight-line and path tests (see <see cref="FordsLayoutFitter"/>).
    /// </summary>
    FordsLayoutFit Fit(MapLayout layout);

    /// <summary>Reads the layout from the scene's <c>fow_</c> tags. False, with the first problem, when the markers are incomplete.</summary>
    bool TryReadTaggedLayout(string sceneName, CampDefaults camp, out MapLayout layout, out string problem);
}
