namespace TAOM.Features.FactionUI.CharacterCreation;

/// <summary>How far one themed character-creation screen moves the engine's default camera (#704):
/// sideways (<see cref="X"/>), away from the character (<see cref="Distance"/>), both in scene metres,
/// and how much wider the vertical field of view is, in radians (<see cref="Fov"/>).</summary>
public readonly struct CameraOffsets
{
    public static readonly CameraOffsets None = new(0f, 0f, 0f);

    public CameraOffsets(float x, float distance, float fov)
    {
        X = x;
        Distance = distance;
        Fov = fov;
    }

    public float X { get; }

    public float Distance { get; }

    public float Fov { get; }
}
