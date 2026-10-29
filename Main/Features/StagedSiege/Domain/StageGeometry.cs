using System;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>A scene position in plain floats (constructing an engine vector runs engine code, which a test host cannot do).</summary>
public readonly struct StagePoint
{
    public StagePoint(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public bool IsFinite => !(float.IsNaN(X) || float.IsInfinity(X) || float.IsNaN(Y) || float.IsInfinity(Y) || float.IsNaN(Z) || float.IsInfinity(Z));

    /// <summary>Ground distance: height is ignored.</summary>
    public float DistanceXY(StagePoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>A unit direction on the ground.</summary>
public readonly struct StageFacing
{
    private const float MinLength = 0.5f;
    private const float MaxLength = 1.5f;
    private const float CoincidentMetres = 0.01f;

    public StageFacing(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }
    public float Y { get; }

    /// <summary>
    /// Normalizes a recorded direction. Fails for a non-finite pair or a length outside [0.5, 1.5], because the recorder writes a
    /// unit vector and anything far from one is a typo, not a direction.
    /// </summary>
    public static bool TryNormalize(float x, float y, out StageFacing facing)
    {
        facing = new StageFacing(0f, 1f);
        if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
            return false;
        var length = (float)Math.Sqrt(x * x + y * y);
        if (!(length >= MinLength && length <= MaxLength))
            return false;
        facing = new StageFacing(x / length, y / length);
        return true;
    }

    /// <summary>The direction from one point to another; (0, 1) when they are under 0.01 m apart.</summary>
    public static StageFacing Toward(StagePoint from, StagePoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        return length < CoincidentMetres ? new StageFacing(0f, 1f) : new StageFacing(dx / length, dy / length);
    }
}

/// <summary>A position and the way it faces.</summary>
public readonly struct StageFrame
{
    public StageFrame(StagePoint position, StageFacing facing)
    {
        Position = position;
        Facing = facing;
    }

    public StagePoint Position { get; }
    public StageFacing Facing { get; }
}

/// <summary>A capture point: its letter, its centre and how far from the centre a unit counts as standing on it.</summary>
public readonly struct StageZone
{
    public StageZone(StageId id, StagePoint center, float radius)
    {
        Id = id;
        Center = center;
        Radius = radius;
    }

    public StageId Id { get; }
    public StagePoint Center { get; }
    public float Radius { get; }
}
