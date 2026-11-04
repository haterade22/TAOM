using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Adapters;

/// <summary>
/// Every read and write the Seat makes on the mission screen and the mission's camera (build-spec 5.4), so the state machine runs
/// in a test. A client type: <c>MissionScreen</c> and <c>MissionMainAgentController</c> sit in the client-only
/// <c>TaleWorlds.MountAndBlade.View</c> assembly (ADR-013), and no core code calls it. All members run on the main thread, from the
/// mission screen tick and from <c>UpdateOverridenCamera</c> inside <c>Mission.OnTick</c>.
/// </summary>
internal interface ISeatScreenAdapter
{
    /// <summary>The mission's mode, photo mode, custom camera and ended state right now, and whether a warg rage holds the hero.</summary>
    SeatEngineFacts ReadFacts();

    /// <summary>The camera the player has now, and the view angle in degrees it renders with; false when it cannot be read.</summary>
    bool TryReadCamera(out SeatPose pose, out float viewAngle);

    /// <summary>The ground height under a point: the larger of the terrain and the water level, measured from <paramref name="fromZ"/> down.</summary>
    float GroundHeight(float x, float y, float fromZ);

    /// <summary>The point itself while inside the mission boundaries, else the closest boundary position.</summary>
    (float X, float Y) ClampToBoundary(float x, float y);

    /// <summary>This frame's flight input from the engine's own game keys, the mouse and the wheel.</summary>
    SeatInput ReadInput(float dt);

    /// <summary>
    /// The Z of the scene's <c>camera_height_limiter</c> entity, which the engine's free camera stops at (MissionScreen.cs:1894-1897). Read
    /// once at entry. No tag reads as 0, and a Z at or below 0 means no limit.
    /// </summary>
    float ReadHeightLimit();

    /// <summary>Hands the engine the frame: camera frame, vertical field of view, scene camera and audio listener.</summary>
    void ApplyFrame(SeatPose pose, float viewAngle, float listenerZ);

    /// <summary>Shuts the hero's controller gate (<c>false</c>) or opens it again (<c>true</c>).</summary>
    void SetHeroController(bool enabled);

    /// <summary>
    /// Writes the look the Seat ended on, so the engine's camera continues from it: the bearing, and the elevation (the vanilla sign of the
    /// pitch) through a private setter. False when that setter is missing, which leaves a visible jump in the glide's vertical angle.
    /// </summary>
    bool WriteLook(float yaw, float pitch);

    /// <summary>
    /// Arms the engine's own return glide to the hero. False when the camera has no agent to follow, and then the engine's listener is not
    /// called, because its branch would zero the bearing and elevation that <see cref="WriteLook"/> just wrote.
    /// </summary>
    bool BeginGlide();

    /// <summary>Shows one of the Seat's messages to the player.</summary>
    void Say(SeatMessage message);
}
