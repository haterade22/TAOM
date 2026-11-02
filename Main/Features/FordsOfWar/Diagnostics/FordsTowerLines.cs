using System;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The text of the real-tower log lines (after the <c>[FordsOfWar][Tower]</c> tag): the component attached to a scene's tower, the
/// first attack order at it from a stream, and the read-back after it fell. Pure, so the adapters only gather the numbers.
/// </summary>
internal static class FordsTowerLines
{
    /// <summary>One bowman placed on a tower top: where the marker says and where the engine put him.</summary>
    public static string GarrisonSpawned(string id, int n, Vec3f spot, float agentZ, int faceGroup) =>
        FormattableString.Invariant($"spawned tower={id} n={n} at=({spot.X:F1},{spot.Y:F1},{spot.Z:F1}) z={agentZ:F2} face={faceGroup}");

    /// <summary>A look at every bowman: how many still stand within 1.5 m of the spot and 2 m of its height, the worst offsets, and on which navmesh ids.</summary>
    public static string GarrisonLook(int held, int total, float maxDrift, float maxDz, string faces, string worst) =>
        FormattableString.Invariant($"look held={held}/{total} maxDrift={maxDrift:F2} maxDz={maxDz:F2} faces={faces} worst={worst}");

    public static string Attached(string id, string entity, int children, int hitPoints) =>
        "attached id=" + id + " entity=" + entity + " children=" + children + " hp=" + hitPoints;

    /// <summary><paramref name="faceGroup"/> is the navmesh face group id under the tower's origin, or a negative number when there is no face.</summary>
    public static string Order(string id, MatchSide attacker, int lane, Vec3f position, int faceGroup, bool pathExists) =>
        "order id=" + id + " side=" + attacker + " lane=" + lane + " pos=(" + FordsLogLine.F1(position.X) + "," + FordsLogLine.F1(position.Y)
        + "," + FordsLogLine.F1(position.Z) + ") face=" + (faceGroup < 0 ? "none" : faceGroup.ToString(System.Globalization.CultureInfo.InvariantCulture))
        + " pathExists=" + pathExists;

    public static string Fell(string id, bool rootVisible, bool childVisible, bool rootPhysics, bool childPhysics) =>
        "fell id=" + id + " rootVisible=" + rootVisible + " childVisible=" + childVisible + " rootPhysics=" + rootPhysics + " childPhysics=" + childPhysics;
}
