using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What the run asks the director to do with a mounted lord at a wave's edge: nothing, go out to the sally point (the wave's start), or come home (a clear).</summary>
internal enum SallyMove
{
    None,
    Out,
    Home,
}

/// <summary>
/// A place the lord is put: where, and the ground direction he faces there. A zero direction means "keep the facing he has" (the keep point, which is recorded without one). The sally and the
/// home point are places; <see cref="MusterFrame"/> is the recorded shape they come from.
/// </summary>
internal sealed record LdPlace(float X, float Y, float Z, float Fx, float Fy)
{
    public bool HasFacing => Fx != 0f || Fy != 0f;

    public LdPoint Point => new(X, Y, Z);

    public static LdPlace From(MusterFrame frame) => new(frame.X, frame.Y, frame.Z, frame.Fx, frame.Fy);

    public static LdPlace At(LdPoint point) => new(point.X, point.Y, point.Z, 0f, 0f);

    /// <summary>The distance on the ground to a point, in metres. Height is left out: a rider settles onto the ground a moment after a teleport.</summary>
    public float FlatDistanceTo(LdPoint other) => LdSpace.Flat(Point, other);
}

internal static class LdSpace
{
    /// <summary>The distance on the ground between two points; NaN when either has a coordinate that is not a number.</summary>
    public static float Flat(LdPoint a, LdPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
    }
}

/// <summary>What a point has under it: a navmesh, and the inside of the mission boundary. A point the lord is put at needs both.</summary>
internal readonly record struct LdPointFacts(bool Navmesh, bool Boundary)
{
    public bool Usable => Navmesh && Boundary;
}

/// <summary>
/// The lord as the field reads him now. <c>Present</c>: the main agent stands. <c>Own</c>: that body is the one the field spawned for him, not a soldier he took over. <c>Inside</c>: the main agent is
/// inside the mission boundary (the test <c>MissionBoundaryCrossingHandler</c> runs on him). The rest is read only for a detailed look: his formation and where it is ordered to stand, how far he is from it,
/// the nearest attacker, and the gate (<c>open</c>, <c>closed</c> or <c>broken</c>).
/// </summary>
internal sealed record LdLordFacts(bool Present, bool Own, string Id, bool HasMount, LdPoint At, bool Inside, string Formation, LdPoint? OrderAt, float ToFormation, float NearestAttacker, string Gate)
{
    public static LdLordFacts None { get; } = new(false, false, "", false, default, true, "", null, -1f, -1f, "");
}

/// <summary>What a remount did: whether the new body stands, what state the old body read right after the fade, whether that was a loss (killed or knocked out), and the share of health carried over.</summary>
internal sealed record LdRemount(bool Done, string OldState, bool OldLost, float HealthShare, string Problem)
{
    /// <summary>The share of health a remount carries: 0 to 1, and 1 when the limit is not positive or the ratio is not a number (Math.Min and Math.Max on .NET Framework pass a NaN through).</summary>
    public static float ShareOf(float health, float limit)
    {
        if (!(limit > 0f))
            return 1f;

        var ratio = health / limit;
        return float.IsNaN(ratio) ? 1f : System.Math.Max(0f, System.Math.Min(1f, ratio));
    }
}

/// <summary>One callback that reached a body of the lord's (the old one a remount faded, his current one or the main agent): which callback, with what state, and whether the state is a loss.</summary>
internal sealed record LdBodyEvent(string Callback, string State, bool Planned, bool Loss);

internal enum LdContactKind
{
    None,
    Given,
    Taken,
    MountTaken,
}

/// <summary>A hit that touched the lord: his blow (or his horse's) on an attacker, an attacker's blow on him, or on his horse. A kill is the lord's blow that left its victim under 1 health.</summary>
internal readonly record struct LdContact(LdContactKind Kind, bool Kill);
