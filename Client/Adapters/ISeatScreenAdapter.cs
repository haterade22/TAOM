using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Adapters;

/// <summary>
/// Every read and write the Seat makes on the mission screen and the mission's camera (build-spec 5.4), so the state machine runs
/// in a test. A client type: <c>MissionScreen</c> and <c>MissionMainAgentController</c> sit in the client-only
/// <c>TaleWorlds.MountAndBlade.View</c> assembly (ADR-013), and no core code calls it. All members run on the main thread, from the
/// mission screen tick, from <c>UpdateOverridenCamera</c> and from <c>OnMissionTick</c>.
/// </summary>
internal interface ISeatScreenAdapter
{
    /// <summary>The mission's mode, photo mode, custom camera and ended state right now, whether a warg rage holds the hero, and whether the radial menu is active.</summary>
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

    /// <summary>The order panel is open: the engine's plain <c>Mission.IsOrderMenuOpen</c> (build-spec 6.1).</summary>
    bool IsPanelOpen();

    /// <summary>The middle mouse button is down on the scene layer, which looks with the cursor hidden.</summary>
    bool LookHeld();

    /// <summary>
    /// Shows or hides the cursor. The adapter builds one small layer on the first show (it asks the screen manager for a visible cursor and
    /// takes no input) and after that only toggles that layer's mouse visibility, as the banner editor and the face generator do; the
    /// screen finalizes the layer at teardown. Hiding with no layer built is a no-op, and it is safe at every teardown path.
    /// </summary>
    void SetCursor(bool show);

    /// <summary>Asks the engine not to move the cursor when it hides, so it comes back where it was (<c>MBWindowManager.DontChangeCursorPos</c>).</summary>
    void HoldCursorPosition();

    /// <summary>The player's own ways to close the panel that this frame shows (build-spec 6.2).</summary>
    SeatCloseSignals ReadCloseSignals();

    /// <summary>
    /// Opens the order panel again; false when the view model cannot be reached. True means the site was reached: the engine can still refuse
    /// without a word (<c>MissionOrderVM.OpenToggleOrder</c> returns when the hero or the last troop is gone), so the caller reads
    /// <see cref="IsPanelOpen"/> to know that the panel opened.
    /// </summary>
    bool OpenPanel();

    /// <summary>The troop transfer menu is open (<c>Mission.IsTransferMenuOpen</c>). The Transfer order closes the panel on purpose.</summary>
    bool TransferMenuOpen();

    /// <summary>The engine's flag that stops every Gauntlet draw (<c>MBDebug.DisableAllUI</c>), which no other TAOM code writes.</summary>
    bool ReadUiHidden();

    void WriteUiHidden(bool hidden);

    /// <summary>A dialog shows over the mission.</summary>
    bool DialogShown();

    /// <summary>A layer other than the mission's scene layer holds the focus.</summary>
    bool OtherLayerFocused();

    /// <summary>
    /// Keeps to something (build-spec section 7): with the panel open and one company selected, that company; else the soldier under the
    /// pointer (the cursor when shown, else the screen centre). The adapter holds the target. None when nothing is there.
    /// </summary>
    SeatKeepKind TryKeep();

    /// <summary>The kept target's position, or gone: the soldier is not in the mission or the company has no units.</summary>
    SeatTargetRead ReadKept();

    /// <summary>Drops the kept target.</summary>
    void Unkeep();

    /// <summary>Real seconds since the application started (<c>Time.ApplicationTime</c>): it runs under a pause and does not stretch with mission time.</summary>
    float RealSeconds();

    /// <summary>Takes the nearest eligible soldier of the kept company, else the one at the pointer, for the player (build-spec section 9), from the mission tick only.</summary>
    SeatTakeResult TryTake();
}
