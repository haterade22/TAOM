using System;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The commander's camera for Fords of War (build-spec 6.1): a top-down RTS camera in place of the free spectator camera a mission
/// with no main agent would fall back to. <c>MissionScreen.CheckForUpdateCamera</c> skips its own update when any view's
/// <see cref="UpdateOverridenCamera"/> answers true, so this view owns the frame and finishes it as the tail of the vanilla
/// <c>UpdateCamera</c> does: the camera frame, the vertical field of view, <c>SceneView.SetCamera</c> and <c>Mission.SetCameraFrame</c>
/// (audio and level of detail). WASD or the arrows pan relative to the yaw, Q and E turn, the wheel zooms, the right button drags to
/// turn and tilt, Shift doubles the pan. The arithmetic is <see cref="FordsCameraMath"/>. Built by the mission's <c>[ViewMethod]</c> list
/// (<see cref="FordsOfWarMissionViews"/>), not a <c>[DefaultView]</c>. A fault latches the view off and hands the camera back.
/// </summary>
public sealed class FordsCommanderCameraView : MissionView
{
    private const float NearPlane = 0.1f, FarPlane = 12500f;
    private const float ScrollNotch = 120f;
    private const float GroundFollowPerSecond = 6f;
    private const float DegreesToRadians = (float)(Math.PI / 180.0);

    private FordsCameraProbe? _probe;
    private FordsSession? _session;
    private FordsCameraState _state;
    private Vec2 _lastPointer;
    private float _groundZ;
    private bool _started, _dragging, _failed;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        MissionScreen?.SuppressSpectatorCyclingThisFrame();
    }

    // The escape menu offers Photo Mode and no view here turns it off again; every view is asked, and one "no" disables the item.
    public override bool IsPhotoModeAllowed() => Probe().PhotoVetoed();

    public override bool UpdateOverridenCamera(float dt)
    {
        if (_failed)
            return false;

        try
        {
            _session ??= IoC.Resolve<FordsSession>();
            if (_session == null || !_session.IsActive || MissionScreen == null)
                return false;

            MissionScreen.SuppressSpectatorCyclingThisFrame();
            if (!_started)
                Begin(_session);

            var before = _state;
            var input = ReadInput();
            var boundary = _session.BoundaryPolygon;
            _state = FordsCameraMath.Step(before, input, dt, boundary);
            Apply(dt);
            Probe().Frame(before, input, _state, dt, boundary);
            return true;
        }
        catch (Exception ex)
        {
            _failed = true;
            Probe().Failed();
            IoC.Resolve<IModLogger>()?.LogError($"[FordsOfWar] commander camera failed, vanilla camera restored: {ex}");
            return false;
        }
    }

    private FordsCameraProbe Probe() =>
        _probe ??= new(IoC.Resolve<FordsRunReport>(), (x, y) => Mission.IsPositionInsideBoundaries(new Vec2(x, y)));

    private void Begin(FordsSession session)
    {
        var start = session.CameraStart;
        _groundZ = Mission.Scene != null ? FordsEngine.TerrainAt(Mission.Scene, start.X, start.Y) : start.Z;
        _state = FordsCameraMath.Start(start, session.CameraLookAt, _groundZ);
        _started = true;
    }

    private FordsCameraInput ReadInput()
    {
        var pan = Axis(InputKey.W, InputKey.Up, InputKey.S, InputKey.Down);
        var right = Axis(InputKey.D, InputKey.Right, InputKey.A, InputKey.Left);
        var rotate = Axis(InputKey.Q, InputKey.E);
        var zoom = Input.GetDeltaMouseScroll() / ScrollNotch;

        // The pointer is visible all mission, so the drag is read from its position, not from the raw mouse delta. The button
        // reads down only while the cursor is on the scene (MouseOnMe), so a click on the panel never starts a drag.
        float dragX = 0f, dragY = 0f;
        if (Input.IsKeyDown(InputKey.RightMouseButton))
        {
            var pointer = Input.GetMousePositionPixel();
            if (_dragging)
            {
                dragX = pointer.x - _lastPointer.x;
                dragY = pointer.y - _lastPointer.y;
            }

            _lastPointer = pointer;
            _dragging = true;
        }
        else
        {
            _dragging = false;
        }

        return new FordsCameraInput(pan, right, rotate, zoom, dragX, dragY, Input.IsShiftDown());
    }

    private float Axis(InputKey plus, InputKey minus) => (Input.IsKeyDown(plus) ? 1f : 0f) - (Input.IsKeyDown(minus) ? 1f : 0f);

    private float Axis(InputKey plus, InputKey plusAlt, InputKey minus, InputKey minusAlt) =>
        (Input.IsKeyDown(plus) || Input.IsKeyDown(plusAlt) ? 1f : 0f) - (Input.IsKeyDown(minus) || Input.IsKeyDown(minusAlt) ? 1f : 0f);

    private void Apply(float dt)
    {
        var scene = Mission.Scene;
        if (scene != null)
        {
            var target = FordsEngine.TerrainAt(scene, _state.X, _state.Y);
            _groundZ += (target - _groundZ) * Math.Min(1f, GroundFollowPerSecond * dt);
        }

        var (side, up, back) = FordsCameraMath.Basis(_state.Yaw, FordsCameraMath.Pitch(_state));
        var origin = new Vec3(_state.X, _state.Y, _groundZ + _state.Height, -1f);
        var rotation = new Mat3(FordsEngine.ToVec3(side), FordsEngine.ToVec3(up), FordsEngine.ToVec3(back));
        var frame = new MatrixFrame(rotation, origin);

        MissionScreen.UpdateFreeCamera(frame);
        var camera = MissionScreen.CombatCamera;
        camera.SetFovVertical(MissionScreen.CameraViewAngle * DegreesToRadians, Screen.AspectRatio, NearPlane, FarPlane);
        MissionScreen.SceneView.SetCamera(camera);
        var listener = origin;
        Mission.SetCameraFrame(ref frame, 65f / MissionScreen.CameraViewAngle, ref listener);
    }
}
