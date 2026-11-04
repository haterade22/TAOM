namespace TAOM.Client.Adapters;

/// <summary>One flag on the ground: where a queued order goes.</summary>
internal readonly struct MarshalFlagPoint
{
    internal MarshalFlagPoint(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    internal float X { get; }

    internal float Y { get; }

    internal float Z { get; }
}
