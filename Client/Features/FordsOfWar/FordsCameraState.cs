namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// Where the commander camera is: a ground position, a yaw (radians, 0 looks along +Y, a positive turn looks left), a height above
/// the ground and a tilt offset (radians) on top of the pitch the height gives. A plain struct: TAOM.Client declares no records.
/// </summary>
internal readonly struct FordsCameraState
{
    public FordsCameraState(float x, float y, float yaw, float height, float tilt)
    {
        X = x;
        Y = y;
        Yaw = yaw;
        Height = height;
        Tilt = tilt;
    }

    public float X { get; }
    public float Y { get; }
    public float Yaw { get; }
    public float Height { get; }
    public float Tilt { get; }
}
