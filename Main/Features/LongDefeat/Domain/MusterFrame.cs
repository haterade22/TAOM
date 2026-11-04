using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A recorded muster place: where new defenders appear, and the ground direction the formation faces there. <c>Fx</c>, <c>Fy</c> is a unit vector.</summary>
internal sealed record MusterFrame(float X, float Y, float Z, float Fx, float Fy)
{
    private const float MinLookLength = 0.5f;
    private const float MaxLookLength = 1.5f;

    /// <summary>
    /// Builds a frame from recorded numbers, normalizing the look. False for a non-finite number or a look whose length lies outside
    /// [0.5, 1.5], since the recorder writes a unit vector and anything far from one is a typo, not a direction.
    /// </summary>
    public static bool TryCreate(float x, float y, float z, float fx, float fy, out MusterFrame frame)
    {
        frame = new MusterFrame(x, y, z, 0f, 1f);
        var length = Math.Sqrt((double)fx * fx + (double)fy * fy);
        if (!(length >= MinLookLength && length <= MaxLookLength))
            return false;

        frame = new MusterFrame(x, y, z, (float)(fx / length), (float)(fy / length));
        return true;
    }
}

/// <summary>The recorded muster places of one fortress, one per defender class. A null point leaves that class on the scene's own frame.</summary>
internal sealed record MusterPoints(MusterFrame? Foot, MusterFrame? Archers)
{
    public MusterFrame? For(FormationSlot slot) => slot == FormationSlot.Ranged ? Archers : Foot;
}
