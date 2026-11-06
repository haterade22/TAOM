using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A gate's plane on the ground: <c>Nx, Ny</c> the unit normal pointing to the inside of the walls, <c>Lx, Ly</c> the unit axis along the door.</summary>
internal readonly record struct GatePlane(float Nx, float Ny, float Lx, float Ly);

/// <summary>
/// Who has come through a repaired gate (<c>gate-attacked</c>, approved plan S4 row): an attacker within a few metres behind the gate's plane, within the door's width and at the door's height. The engine keeps no
/// "inside" flag on a gate, so the plane comes from the gate's own frame: its forward axis, which the prefab lays the passage along (the door at the origin, <c>middle_pos</c> and <c>wait_pos</c> at (0, -1.4, 0) and the
/// ram's rest point at (0, 6.25, 0), props_siege_gates_outer.xml), turned to point at the keep. The side axis is never the normal: at a keep far off the door's bearing it would put the box beside the door. Pure.
/// </summary>
internal static class GateGeometry
{
    private const float MinLength = 1e-4f;

    /// <summary>
    /// The plane of a gate at <paramref name="gate"/> whose frame has the horizontal part (<paramref name="fx"/>, <paramref name="fy"/>) of its forward axis, facing <paramref name="inside"/>. Null for a frame with
    /// no horizontal forward axis, a number that is not one, or an inside point on the gate's side line (no direction along the forward axis to turn it by).
    /// </summary>
    public static GatePlane? PlaneOf(LdPoint gate, float fx, float fy, LdPoint inside)
    {
        var dx = inside.X - gate.X;
        var dy = inside.Y - gate.Y;
        if (!Finite(fx, fy, dx, dy) || Unit(fx, fy) is not { } n)
            return null;

        var along = dx * n.X + dy * n.Y;
        if (Math.Abs(along) < MinLength)
            return null;

        var sign = along < 0f ? -1f : 1f;
        return new GatePlane(n.X * sign, n.Y * sign, -n.Y * sign, n.X * sign);
    }

    /// <summary>
    /// Whether <paramref name="at"/> is behind the plane (on the inside, past it) by at most <paramref name="depth"/>, within <paramref name="halfWidth"/> of the door's centre and within
    /// <paramref name="heightBand"/> of the gate's height: a soldier on the wall walk above the door is not in the doorway.
    /// </summary>
    public static bool IsBehind(LdPoint gate, GatePlane plane, LdPoint at, float depth, float halfWidth, float heightBand)
    {
        var dx = at.X - gate.X;
        var dy = at.Y - gate.Y;
        var behind = dx * plane.Nx + dy * plane.Ny;
        var across = Math.Abs(dx * plane.Lx + dy * plane.Ly);
        return behind > 0f && behind <= depth && across <= halfWidth && Math.Abs(at.Z - gate.Z) <= heightBand;
    }

    private static (float X, float Y)? Unit(float x, float y)
    {
        var length = (float)Math.Sqrt((double)x * x + (double)y * y);
        return length < MinLength ? null : (x / length, y / length);
    }

    private static bool Finite(params float[] values)
    {
        foreach (var v in values)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                return false;
        }

        return true;
    }
}
