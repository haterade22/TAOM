namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>
/// A standing spot for one creature and the way it faces. <see cref="IsValid"/> is false for every input that could not
/// produce one (a non-finite number, a gate with no usable axis, a negative index): <see cref="None"/> carries NaN, so a
/// caller that forgot to check cannot send an agent anywhere real.
/// </summary>
public readonly record struct SiegeSlot(float X, float Y, float FaceX, float FaceY, bool IsValid)
{
    /// <summary>The slot that does not exist.</summary>
    public static readonly SiegeSlot None = new(float.NaN, float.NaN, float.NaN, float.NaN, false);
}

/// <summary>Where a hold anchor came from, for the log and for the order of preference.</summary>
public enum AnchorSource
{
    OuterMiddle,
    InnerMiddle,
    InsideOuterGate,
}

/// <summary>
/// One place a creature may hold. <see cref="HeightValid"/> says the ground at the point is within a few metres of its
/// gate's base, which keeps a hold off a wall top; whether the creature can walk there is asked per creature.
/// </summary>
public readonly record struct AnchorCandidate(AnchorSource Source, SiegePoint Point, bool HeightValid);
