using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TAOM.Adapters;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Adapters;

/// <summary>
/// The Seat's every read and write on <c>MissionScreen</c>, the mission's camera and <c>MissionMainAgentController</c> (build-spec
/// 5.4 and 5.5), built once per mission by <c>SeatView</c>. The frame is finished the way the tail of <c>MissionScreen.UpdateCamera</c>
/// finishes it. <c>UpdateFreeCamera</c> is never called: it writes <c>CameraElevation</c> with the opposite sign of the one
/// <c>UpdateCamera</c> uses, and the return glide reads that value.
/// </summary>
internal sealed class SeatScreenAdapter : ISeatScreenAdapter
{
    private const float NearPlane = 0.1f, FarPlane = 12500f;
    private const float ScrollNotch = 120f;
    private const float DegreesToRadians = (float)(Math.PI / 180.0);

    // The tag whose entity's Z the engine's free camera stops at (MissionScreen.cs:57, 4601-4605, v1.5.6).
    private const string HeightLimiterTag = "camera_height_limiter";

    // MissionScreen.CameraElevation has a private setter (v1.5.6 MissionScreen.cs:197). Cached once, never per call.
    private static readonly MethodInfo? CameraElevationSetter =
        AccessTools.PropertySetter(typeof(MissionScreen), nameof(MissionScreen.CameraElevation));

    private readonly MissionScreen _screen;
    private readonly Mission _mission;
    private MissionMainAgentController? _controller;
    private IAutonomousMovementController? _rage;

    internal SeatScreenAdapter(MissionScreen screen, Mission mission)
    {
        _screen = screen;
        _mission = mission;
    }

    public SeatEngineFacts ReadFacts()
    {
        var mode = _mission.Mode;
        var interlude = mode == MissionMode.Conversation || mode == MissionMode.Barter || mode == MissionMode.CutScene;
        return new SeatEngineFacts(mode == MissionMode.Deployment, interlude, mode == MissionMode.Battle,
            _screen.IsPhotoModeEnabled, _screen.CustomCamera != null, _mission.MissionEnded, RageHoldsHero());
    }

    public bool TryReadCamera(out SeatPose pose, out float viewAngle)
    {
        pose = default;
        viewAngle = 0f;
        var camera = _screen.CombatCamera;
        if (camera == null)
            return false;

        var frame = camera.Frame;
        var look = -frame.rotation.u;
        pose = SeatCameraMath.PoseFromLook(frame.origin.x, frame.origin.y, frame.origin.z, look.x, look.y, look.z);
        viewAngle = _screen.CameraViewAngle;
        return true;
    }

    public float GroundHeight(float x, float y, float fromZ)
    {
        var scene = _mission.Scene;
        if (scene == null)
            return float.NaN;

        // The mask is the engine's own for the free camera (BodyFlags.cs:47, the 544321929 at MissionScreen.cs:1885). The native probe
        // answers 0 when nothing is there, so vanilla's "below 9999" guard never fails and the Seat has none.
        var terrain = scene.GetGroundHeightAtPosition(new Vec3(x, y, fromZ, -1f), BodyFlags.CommonCollisionExcludeFlags);
        return Math.Max(terrain, scene.GetWaterLevel());
    }

    public (float X, float Y) ClampToBoundary(float x, float y)
    {
        var point = new Vec2(x, y);
        if (_mission.IsPositionInsideBoundaries(point))
            return (x, y);

        var closest = _mission.GetClosestBoundaryPosition(point);
        return (closest.x, closest.y);
    }

    public SeatInput ReadInput(float dt)
    {
        var input = _screen.SceneLayer.Input;
        var lift = (input.IsGameKeyDown((int)GameKeyDefinition.Jump) ? 1f : 0f) - (input.IsGameKeyDown((int)GameKeyDefinition.Crouch) ? 1f : 0f);

        var looksWithMouse = !_screen.MouseVisible || input.IsKeyDown(InputKey.MiddleMouseButton);
        var (yaw, pitch) = looksWithMouse
            ? SeatCameraMath.Look(input.GetMouseMoveX(), input.GetMouseMoveY(), input.GetGameKeyAxis("CameraAxisX"),
                input.GetGameKeyAxis("CameraAxisY"), dt, input.GetMouseSensitivity(), NativeConfig.InvertMouse, input.GetIsMouseActive())
            : (0f, 0f);

        return new SeatInput(
            SeatCameraMath.DeadZone(input.GetGameKeyAxis("MovementAxisX")),
            SeatCameraMath.DeadZone(input.GetGameKeyAxis("MovementAxisY")),
            lift, input.GetDeltaMouseScroll() / ScrollNotch, yaw, pitch, input.IsGameKeyDown((int)GameKeyDefinition.Zoom));
    }

    public float ReadHeightLimit()
    {
        var tag = _mission.Scene?.FindEntityWithTag(HeightLimiterTag);
        return tag != null ? tag.GetGlobalFrame().origin.z : 0f;
    }

    public void ApplyFrame(SeatPose pose, float viewAngle, float listenerZ)
    {
        var (side, up, back) = FordsCameraMath.Basis(pose.Yaw, pose.Pitch);
        var origin = new Vec3(pose.X, pose.Y, pose.Z, -1f);
        var frame = new MatrixFrame(new Mat3(FordsEngine.ToVec3(side), FordsEngine.ToVec3(up), FordsEngine.ToVec3(back)), origin);

        var camera = _screen.CombatCamera;
        camera.Frame = frame;
        _screen.CameraBearing = pose.Yaw;
        camera.SetFovVertical(viewAngle * DegreesToRadians, Screen.AspectRatio, NearPlane, FarPlane);
        _screen.SceneView.SetCamera(camera);
        var listener = new Vec3(pose.X, pose.Y, listenerZ, -1f);
        _mission.SetCameraFrame(ref frame, SeatCameraMath.ZoomFactor(viewAngle), ref listener);

        // The tail of UpdateCamera (MissionScreen.cs:2354-2355, v1.5.6), which a Seat frame skips. Without these two calls the view
        // stays as blurred as the last frame that set depth of field. This is the view staying sharp, not the depth of field
        // effect the contract drops.
        var scene = _mission.Scene;
        scene.SetDepthOfFieldParameters(0f, 0f, false);
        scene.SetDepthOfFieldFocus(0f);
    }

    // The flag is the controller's own HoldsHero, set by the swap, never its activation flag. A rider who fell or dismounted during a
    // rage is not held: the controller's own line 193 returns without a mount, and nothing else clears the flag then.
    private bool RageHoldsHero()
    {
        _rage ??= _mission.GetMissionBehavior<IAutonomousMovementController>();
        return _rage?.HoldsHero == true && _mission.MainAgent?.MountAgent != null;
    }

    public void SetHeroController(bool enabled)
    {
        if (enabled && RageHoldsHero()) return;
        _controller ??= _mission.GetMissionBehavior<MissionMainAgentController>();
        if (enabled)
            _controller?.Enable();
        else
            _controller?.Disable();
    }

    /// <summary>
    /// Writes the look the Seat ended on: the bearing, and the elevation at the vanilla sign of the current look (minus the pitch).
    /// <c>UpdateCamera</c> keeps what <c>HandleUserInput</c> integrated from the mouse during the stay unless this runs. False when the
    /// private setter is missing.
    /// </summary>
    public bool WriteLook(float yaw, float pitch)
    {
        _screen.CameraBearing = yaw;
        if (CameraElevationSetter == null)
            return false;
        CameraElevationSetter.Invoke(_screen, new object[] { -pitch });
        return true;
    }

    /// <summary>
    /// The engine's own glide, used when Deployment ends: with the mode Battle and the old mode Deployment,
    /// <c>OnMissionModeChange</c> seeds the position, bearing and elevation gaps from the camera to the followed agent and lets
    /// <c>UpdateCamera</c> ease them away (MissionScreen.cs:4559-4583). With no agent to follow its branch zeroes the bearing and the
    /// elevation, so the listener is not called and the look just written stays.
    /// </summary>
    public bool BeginGlide()
    {
        if (_screen.GetSpectatingData(_screen.CombatCamera.Position).AgentToFollow == null)
            return false;

        ((IMissionListener)_screen).OnMissionModeChange(MissionMode.Deployment, false);
        return true;
    }

    public void Say(SeatMessage message) => InformationManager.DisplayMessage(new InformationMessage(TextFor(message).ToString()));

    private static TextObject TextFor(SeatMessage message) => message switch
    {
        SeatMessage.FirstEntry => new TextObject("{=taom_seat_first_entry}Press the Seat key to return to your hero."),
        SeatMessage.OtherView => new TextObject("{=taom_seat_other_view}You cannot take the Seat from this view."),
        SeatMessage.RageHolds => new TextObject("{=taom_seat_rage}You cannot take the Seat while the rage holds you."),
        _ => new TextObject("{=taom_seat_fault}The Seat of Seeing failed and is off until this scene ends. Your own view is back."),
    };
}
