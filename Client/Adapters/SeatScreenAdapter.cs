using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Options;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Elephant;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.Mumakil;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.SeatOfSeeing.Hooks;

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

    // GauntletOrderUIHandler._dataSource is protected (v1.5.6 GauntletOrderUIHandler.cs:41). Cached once, never per call.
    private static readonly FieldInfo? OrderDataSource = AccessTools.Field(typeof(GauntletOrderUIHandler), "_dataSource");

    private const int CursorLayerOrder = 30;

    private readonly MissionScreen _screen;
    private readonly Mission _mission;
    private readonly IModLogger _logger;
    private MissionMainAgentController? _controller;
    private IAutonomousMovementController? _rage;
    private GauntletOrderUIHandler? _orderHandler;
    private GauntletLayer? _cursorLayer;
    private Agent? _keptAgent;
    private Formation? _keptFormation;
    private readonly FordsMissionSpeed _clock = new(null);
    private readonly FordsGameSpeed _clockLevel = new();
    private readonly Action<string> _report;

    internal SeatScreenAdapter(MissionScreen screen, Mission mission, IModLogger logger)
    {
        _screen = screen;
        _mission = mission;
        _logger = logger;
        _report = logger.LogError;
    }

    public SeatEngineFacts ReadFacts()
    {
        var mode = _mission.Mode;
        var interlude = mode == MissionMode.Conversation || mode == MissionMode.Barter || mode == MissionMode.CutScene;
        return new SeatEngineFacts(mode == MissionMode.Deployment, interlude, mode == MissionMode.Battle,
            _screen.IsPhotoModeEnabled, _screen.CustomCamera != null, _mission.MissionEnded, RageHoldsHero(), _screen.IsRadialMenuActive);
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

        var looksWithMouse = !_screen.MouseVisible || LookHeld();
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

    public void Say(SeatMessage message, string? argument = null)
    {
        Said++;
        LastSaid = message;
        InformationManager.DisplayMessage(new InformationMessage(TextFor(message, argument).ToString()));
    }

    /// <summary>How many messages the Seat has shown in this mission, for the field trial's facts.</summary>
    internal int Said { get; private set; }

    internal SeatMessage? LastSaid { get; private set; }

    public bool IsPanelOpen() => _mission.IsOrderMenuOpen;

    public bool LookHeld() => _screen.SceneLayer.Input.IsKeyDown(InputKey.MiddleMouseButton);

    // One layer for the mission, built on the first show and then only toggled, the way vanilla keeps a cursor layer (the banner editor and
    // the face generator, in TaleWorlds.MountAndBlade.GauntletUI.dll). The layer takes no input (its mask stays Invalid) and only asks for
    // a visible cursor, which ScreenManager.UpdateMouseVisibility grants while any active layer asks (ScreenManager.cs:763-774). It is never
    // removed: RemoveLayer finalizes a layer and AddLayer refuses a finalized one (ScreenBase.cs:299-301, 327), and the screen finalizes
    // its layers itself at teardown (ScreenBase.cs:60-63). SetMouseVisibility is a plain setter (InputRestrictions.cs:23-26), so hiding is
    // safe on any teardown path, with the layer live or already finalized.
    public void SetCursor(bool show)
    {
        if (_cursorLayer == null)
        {
            if (!show)
                return;
            var layer = new GauntletLayer("SeatCursor", CursorLayerOrder);
            layer.InputRestrictions.SetMouseVisibility(true);
            _screen.AddLayer(layer);
            _cursorLayer = layer;
            return;
        }

        _cursorLayer.InputRestrictions.SetMouseVisibility(show);
    }

    public void HoldCursorPosition() => MBWindowManager.DontChangeCursorPos();

    public SeatCloseSignals ReadCloseSignals()
    {
        var input = _screen.SceneLayer.Input;
        var byTrial = TrialPlayerClose;
        TrialPlayerClose = false;
        // Game key 77 (SelectOrderReturn) closes the panel at the root of a layout that has a Return order (GauntletOrderUIHandler.cs:449-452,
        // 484-492), so it shares the view-orders bit. A signal counts only on a closing edge, so a Return press in a sub-menu suppresses nothing.
        return new SeatCloseSignals(input.IsHotKeyReleased("ToggleEscapeMenu"), input.IsKeyReleased(InputKey.RightMouseButton),
            input.IsGameKeyPressed((int)GameKeyDefinition.ViewOrders) || input.IsGameKeyPressed((int)GameKeyDefinition.SelectOrderReturn) || byTrial,
            input.IsGameKeyReleased((int)GameKeyDefinition.HoldOrder));
    }

    public bool TransferMenuOpen() => _mission.IsTransferMenuOpen;

    /// <summary>The field trial closed the panel as the player does, so the session must not reopen it.</summary>
    internal bool TrialPlayerClose { get; set; }

    public bool OpenPanel()
    {
        var panel = OrderPanel();
        panel?.OpenToggleOrder(false, false);
        return panel != null;
    }

    internal void ClosePanel() => OrderPanel()?.TryCloseToggleOrder();

    private MissionOrderVM? OrderPanel()
    {
        if (OrderDataSource == null)
            return null;
        _orderHandler ??= _mission.GetMissionBehavior<GauntletOrderUIHandler>();
        return _orderHandler == null ? null : OrderDataSource.GetValue(_orderHandler) as MissionOrderVM;
    }

    public bool ReadUiHidden() => MBDebug.DisableAllUI;

    public void WriteUiHidden(bool hidden) => MBDebug.DisableAllUI = hidden;

    public bool DialogShown() => ((IMissionScreen)_screen).GetDisplayDialog();

    public bool OtherLayerFocused()
    {
        var focused = ScreenManager.FocusedLayer;
        return focused != null && focused != _screen.SceneLayer;
    }

    public SeatKeepKind TryKeep()
    {
        _keptAgent = null;
        _keptFormation = null;
        var selected = _mission.PlayerTeam?.PlayerOrderController?.SelectedFormations;
        var count = selected?.Count ?? 0;
        var first = count > 0 ? selected![0] : null;
        if (SeatKeepRule.KeepsCompany(_mission.IsOrderMenuOpen, count, first?.CountOfUnits ?? 0))
        {
            _keptFormation = first;
            return SeatKeepKind.Company;
        }

        _keptAgent = SoldierAtPointer();
        return _keptAgent != null ? SeatKeepKind.Soldier : SeatKeepKind.None;
    }

    // RayCastForClosestAgent can answer a mount, so the soldier is its rider and a riderless mount is a miss. The soldier counts unless
    // terrain or an entity is nearer along the same ray, with the order placer's thickness and flags (OrderTroopPlacer.cs:205).
    private Agent? SoldierAtPointer()
    {
        SeatOrderRay.PointerRay(_screen, out var begin, out var end);
        var hit = _mission.RayCastForClosestAgent(begin, end, -1, SeatOrderRay.RayThickness, out var agentDistance);
        var soldier = hit != null && hit.IsMount ? hit.RiderAgent : hit;
        if (soldier == null || !soldier.IsHuman || !AgentSlotIdentity.IsCurrentOccupant(soldier) || !soldier.IsActive())
            return null;

        var terrainHit = _mission.Scene.RayCastForClosestEntityOrTerrain(begin, end, out var terrainDistance, out _, out _, SeatOrderRay.RayThickness,
            SeatOrderRay.RayFlags);
        return SeatKeepRule.AgentCounts(terrainHit, terrainDistance, agentDistance) ? soldier : null;
    }

    public SeatTargetRead ReadKept()
    {
        var gone = new SeatTargetRead(true, 0f, 0f, 0f);
        if (_keptAgent != null)
        {
            if (!AgentSlotIdentity.IsCurrentOccupant(_keptAgent) || !_keptAgent.IsActive())
                return gone;
            var chest = _keptAgent.GetChestGlobalPosition();
            return new SeatTargetRead(false, chest.x, chest.y, chest.z);
        }

        // An empty Formation stays in Team.FormationsIncludingEmpty (Team.cs:280-287), and no read of its position says that it emptied, so the
        // count decides.
        if (_keptFormation == null || _keptFormation.CountOfUnits <= 0)
            return gone;
        var ground = _keptFormation.CachedMedianPosition.GetGroundVec3();
        return new SeatTargetRead(false, ground.x, ground.y, ground.z);
    }

    public void Unkeep()
    {
        _keptAgent = null;
        _keptFormation = null;
    }

    public float RealSeconds() => Time.ApplicationTime;

    public SeatTakeResult TryTake()
    {
        // The main agent must be null. A living hero does not leave the body, so two player units never exist in one formation
        // (ColumnFormation.SwitchUnitLocationsWithBackMostUnit loops on that state through Formation.TryRelocatePlayerUnit).
        if (_mission.MainAgent != null)
            return SeatTakeResult.HeroAlive();

        var refusal = SeatTakeRefusal.None;
        var soldier = _keptFormation != null ? NearestOfKeptCompany() : SoldierAtPointerToTake(out refusal);
        if (soldier == null)
            return refusal != SeatTakeRefusal.None ? SeatTakeResult.Refused(refusal) : SeatTakeResult.Nobody();

        MissionThreadGuard.NoteCall("SeatScreenAdapter.TryTake", _logger.LogError);
        var kind = soldier.Character != null && soldier.Character.IsHero ? SeatTakeKind.Hero : SeatTakeKind.Soldier;
        _mission.TakeControlOfAgent(soldier);
        return SeatTakeResult.Taken(kind, HandOverTheCommand(soldier));
    }

    private Agent? SoldierAtPointerToTake(out SeatTakeRefusal refusal)
    {
        refusal = SeatTakeRefusal.None;
        var soldier = SoldierAtPointer();
        if (soldier == null)
            return null;

        refusal = SeatTakeRule.Check(TakeFacts(soldier));
        return refusal == SeatTakeRefusal.None ? soldier : null;
    }

    // The nearest eligible unit of the kept company to the point the view orbits, by the pure rule.
    private Agent? NearestOfKeptCompany()
    {
        var kept = ReadKept();
        if (kept.Gone || _keptFormation == null)
            return null;

        var units = new List<Agent>();
        _keptFormation.ApplyActionOnEachUnit(units.Add);
        var candidates = new List<SeatTakeCandidate>(units.Count);
        foreach (var unit in units)
        {
            var distance = (unit.Position - new Vec3(kept.X, kept.Y, kept.Z)).LengthSquared;
            candidates.Add(new SeatTakeCandidate(distance, SeatTakeRule.Check(TakeFacts(unit))));
        }

        var index = SeatTakeRule.Nearest(candidates);
        return index >= 0 ? units[index] : null;
    }

    private SeatSoldierFacts TakeFacts(Agent agent)
    {
        var occupant = AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive();
        var hero = agent.Character != null && agent.Character.IsHero;

        // A hero is playable only in a campaign, when his origin is the player's own party; with no campaign every hero is refused,
        // as the engine's own take refuses them (Mission.cs:6395). Never PartyBase.MainParty: it throws without a stored main party.
        var party = Campaign.Current?.MainParty?.Party;
        var ownParty = hero && party != null && ReferenceEquals(agent.Origin?.BattleCombatant, party);
        return new SeatSoldierFacts(agent.IsHuman, occupant, agent.Team != null && agent.Team == _mission.PlayerTeam, agent.IsUsingGameObject,
            agent.IsRetreating(), agent.IsFadingOut(), agent.Health / agent.HealthLimit, hero, ownParty,
            agent.Origin is HowdahCrewAgentOrigin or MumakilCrewAgentOrigin, agent.Formation != null);
    }

    // Vanilla sets the owner only at the start (AssignPlayerRoleInTeamMissionController.cs:54-62), and the setter takes the formation off the AI
    // (Formation.cs:283-294), which undoes Team.DelegateCommandToAI. PlayerOwner survives the fall, so a formation whose owner is gone is found
    // by asking the owner, with the slot check that a deleted agent's handle needs.
    private int HandOverTheCommand(Agent owner)
    {
        var team = _mission.PlayerTeam;
        if (team == null)
            return 0;

        var handed = 0;
        foreach (var formation in team.FormationsIncludingEmpty)
        {
            var previous = formation.PlayerOwner;
            if (previous == null || (AgentSlotIdentity.IsCurrentOccupant(previous) && previous.IsActive()))
                continue;
            formation.PlayerOwner = owner;
            handed++;
        }

        var controller = team.PlayerOrderController;
        if (controller != null)
            controller.Owner = owner;
        return handed;
    }

    public SeatTimeState ReadTime()
    {
        var state = MissionState.Current;
        return new SeatTimeState(_mission.IsFastForward, _mission.FixedDeltaTimeMode, state != null && state.Paused,
            _mission.GetRequestedTimeSpeed(SeatTimeLadder.RequestId, out _));
    }

    // FordsMissionSpeed sets the flag only when it differs (Mission.cs:1268-1279 writes one global byte through native, IMBMission_OnFastForwardStateChanged
    // at 0x6F9E90, which ignores its mission argument), and stops once the mission has ended, as the engine itself does (Mission.cs:3728-3731).
    public void ApplyFastTime(int level, float realDt)
    {
        MissionThreadGuard.NoteCall("SeatScreenAdapter.ApplyFastTime", _report);
        _clockLevel.Set(level);
        _clock.Apply(_mission, _clockLevel, realDt);
    }

    public void StopFastTime()
    {
        MissionThreadGuard.NoteCall("SeatScreenAdapter.StopFastTime", _report);
        FordsMissionSpeed.Stop(_mission);
    }

    public void AddSlowRequest(float factor)
    {
        MissionThreadGuard.NoteCall("SeatScreenAdapter.AddSlowRequest", _report);
        _mission.AddTimeSpeedRequest(new Mission.TimeSpeedRequest(factor, SeatTimeLadder.RequestId));
    }

    public void RemoveSlowRequest()
    {
        MissionThreadGuard.NoteCall("SeatScreenAdapter.RemoveSlowRequest", _report);
        _mission.RemoveTimeSpeedRequest(SeatTimeLadder.RequestId);
    }

    public void SetPaused(bool paused)
    {
        var state = MissionState.Current;
        if (state != null)
            state.Paused = paused;
    }

    public bool ClockOwnedElsewhere() => _mission.GetMissionBehavior<LongDefeatMissionLogic>() != null;

    public SeatRiseRead ReadRise() => new(_mission.IsOrderMenuOpen, _mission.CameraIsFirstPerson, _mission.Mode == MissionMode.Battle);

    public float LiftHeight() => SeatPanelLift.HeightFor(_mission.IsFieldBattle, _mission.IsSiegeBattle || _mission.IsSallyOutBattle);

    // Mission.SetCustomCameraTargetLocalOffset (Mission.cs:2025) is added to the view's target in third person only (MissionScreen.cs:1483). The target
    // lock tests the offset for exact zero (MissionMainAgentController.cs:868-876, Vec3.cs:199-207), so a zero is Vec3.Zero and never a small leftover.
    public void SetPanelLift(float metres)
    {
        MissionThreadGuard.NoteCall("SeatScreenAdapter.SetPanelLift", _report);
        _mission.SetCustomCameraTargetLocalOffset(metres == 0f ? Vec3.Zero : new Vec3(0f, 0f, metres, -1f));
    }

    /// <summary>The field trial's check that the kept soldier is the one it aimed at: a reference, never an agent index.</summary>
    internal bool KeepsAgent(Agent agent) => ReferenceEquals(_keptAgent, agent);

    internal bool KeepsFormation(Formation formation) => ReferenceEquals(_keptFormation, formation);

    internal bool MouseVisible => _screen.MouseVisible;

    private static TextObject TextFor(SeatMessage message, string? argument) => message switch
    {
        SeatMessage.FirstEntry => new TextObject("{=taom_seat_first_entry}Press the Seat key to leave the Seat."),
        SeatMessage.OtherView => new TextObject("{=taom_seat_other_view}You cannot take the Seat from this view."),
        SeatMessage.RageHolds => new TextObject("{=taom_seat_rage}You cannot take the Seat while the rage holds you."),
        SeatMessage.KeepNobody => new TextObject("{=taom_seat_keep_nobody}There is nobody there to keep to."),
        SeatMessage.KeepGone => new TextObject("{=taom_seat_keep_gone}The one you were keeping to is gone."),
        SeatMessage.HeroFell => new TextObject("{=taom_seat_hero_fell}Your hero has fallen. Press Take this soldier (Delete unless rebound) to take a soldier."),
        SeatMessage.TakeNobody => new TextObject("{=taom_seat_take_nobody}There is nobody there to take."),
        SeatMessage.TakeRefused => new TextObject("{=taom_seat_take_refused}You cannot take that soldier."),
        SeatMessage.TakeHeroAlive => new TextObject("{=taom_seat_take_hero_alive}You can take a soldier only after the one you fight as has fallen."),
        SeatMessage.TimeLevel => new TextObject("{=taom_seat_time_level}Mission time: {LEVEL}x").SetTextVariable("LEVEL", argument ?? ""),
        SeatMessage.TimeStayed => new TextObject("{=taom_seat_time_stayed}The battle is paused. Press Stay time (Home unless rebound) again to go on."),
        SeatMessage.TimeResumed => new TextObject("{=taom_seat_time_resumed}The battle goes on."),
        SeatMessage.TimeOtherClock => new TextObject("{=taom_seat_time_other_clock}This battle has its own speed control."),
        _ => new TextObject("{=taom_seat_fault}The Seat of Seeing failed and is off until this scene ends. Your own view is back."),
    };
}
