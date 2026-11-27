using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.SeatOfSeeing.Hooks;

namespace TAOM.Client.Adapters;

/// <summary>
/// <see cref="ISeatCommandAdapter"/> over the live mission (Seat build-spec 6.4). A pick is the screen-space nearest soldier, projected the way
/// the commander's take projects one (<c>FordsPointerPick</c>), with one terrain ray on the winner. Selection goes through the player's
/// <c>OrderController</c> (<c>SelectFormation</c>, <c>DeselectFormation</c>, <c>ClearSelectedFormations</c>), the same call the order panel and
/// the number keys make, so all three share one selection. Orders are the ones the order placer gives (v1.5.7 OrderTroopPlacer.cs:403-424):
/// <c>SetOrderWithTwoPositions</c> with the line orders, a line previewed by <c>SimulateNewOrderWithPositionAndDirection</c>, a charge by
/// <c>SetOrderWithFormation</c>. Nothing here runs outside the first mission tick of a frame.
/// </summary>
internal sealed class SeatCommandAdapter : ISeatCommandAdapter
{
    private readonly MissionScreen _screen;
    private readonly Mission _mission;
    private readonly IModLogger _logger;
    private readonly OrderFlagPool _flags;
    private readonly List<Agent> _agents = new();
    private readonly List<FordsControlCandidate> _units = new();
    private readonly List<Vec3> _flagPoints = new();
    private MissionFormationTargetSelectionHandler? _focus;
    private bool _focusSearched;
    private Formation? _focused;
    private Formation? _pickedOwn;
    private Formation? _enemy;
    private IOrderable? _orderable;
    private WorldPosition _ground;
    private WorldPosition _lineBegin;
    private bool _lineHasBegin;

    internal SeatCommandAdapter(MissionScreen screen, Mission mission, IModLogger logger)
    {
        _screen = screen;
        _mission = mission;
        _logger = logger;
        _flags = new OrderFlagPool(mission);
    }

    private OrderController? Controller => _mission.PlayerTeam?.PlayerOrderController;

    public int SelectedCount => Controller?.SelectedFormations.Count ?? 0;

    public SeatCommandFacts ReadFacts()
    {
        SubscribeFocus();
        var main = _mission.MainAgent;
        var team = _mission.PlayerTeam;
        if (team == null)
            return default;

        var ending = _mission.IsMissionEnding;
        return new SeatCommandFacts(main != null, _mission.Mode == MissionMode.Deployment, main != null && main.IsPlayerControlled,
            SeatPresence.HeroFightsOn, team.HasBots, main != null && team.FormationsIncludingEmpty.Any(f => f.PlayerOwner == main && f.CountOfUnits > 0),
            team.IsPlayerGeneral || team.IsPlayerSergeant, ending, ending && _mission.CheckIfBattleInRetreat(), _mission.IsOrderMenuOpen);
    }

    public SeatPicked PickOwn(float pixelX, float pixelY)
    {
        _pickedOwn = null;
        var team = _mission.PlayerTeam;
        var controller = Controller;
        if (team == null || controller == null)
            return default;

        var soldier = Nearest(team.ActiveAgents, pixelX, pixelY, "own");
        var formation = soldier?.Formation;
        if (formation == null || !controller.IsFormationSelectable(formation))
            return default;

        _pickedOwn = formation;
        return new SeatPicked(true, controller.SelectedFormations.Contains(formation));
    }

    public void Select(SeatSelectAction action)
    {
        MissionThreadGuard.NoteCall("SeatCommandAdapter.Select", _logger.LogError);
        var controller = Controller;
        if (controller == null)
            return;

        var formation = _pickedOwn;
        switch (action)
        {
            case SeatSelectAction.ClearAll:
                controller.ClearSelectedFormations();
                break;
            case SeatSelectAction.ReplaceWith when formation != null:
                controller.ClearSelectedFormations();
                if (controller.IsFormationSelectable(formation))
                    controller.SelectFormation(formation);
                break;
            case SeatSelectAction.Add when formation != null:
                if (!controller.SelectedFormations.Contains(formation) && controller.IsFormationSelectable(formation))
                    controller.SelectFormation(formation);
                break;
            case SeatSelectAction.Remove when formation != null:
                if (controller.SelectedFormations.Contains(formation))
                    controller.DeselectFormation(formation);
                break;
        }
    }

    public SeatRightProbe ProbeRight(float pixelX, float pixelY, float rangedX, float rangedY)
    {
        _enemy = null;
        _orderable = null;
        _ground = WorldPosition.Invalid;
        var team = _mission.PlayerTeam;
        if (team == null)
            return default;

        _enemy = EnemyAt(team, pixelX, pixelY);

        var entity = SeatOrderRay.CollideAt(_screen, _mission, new Vec2(rangedX, rangedY), out var point, out var hit);
        var ground = false;
        if (hit)
        {
            _ground = new WorldPosition(_mission.Scene, UIntPtr.Zero, point, hasValidZ: false);
            var probe = _ground;
            ground = _mission.IsFormationUnitPositionAvailable(ref probe, team);
        }

        // The walk of SeatOrderRay stops at the first entity with a script the side may order, or at nothing.
        if (entity.IsValid)
        {
            var side = team.Side;
            _orderable = entity.GetScriptComponents().OfType<IOrderable>().FirstOrDefault(orderable => orderable.GetOrder(side) != OrderType.None);
        }

        return new SeatRightProbe(_enemy != null, _orderable != null, ground);
    }

    public void Charge()
    {
        MissionThreadGuard.NoteCall("SeatCommandAdapter.Charge", _logger.LogError);
        if (_enemy != null)
            Controller?.SetOrderWithFormation(OrderType.Charge, _enemy);
    }

    public void UseOrderable()
    {
        MissionThreadGuard.NoteCall("SeatCommandAdapter.UseOrderable", _logger.LogError);
        var controller = Controller;
        if (_orderable != null && controller is { SelectedFormations.Count: > 0 })
            controller.SetOrderWithOrderableObject(_orderable);
    }

    public void MoveToProbed(bool horizontal)
    {
        MissionThreadGuard.NoteCall("SeatCommandAdapter.MoveToProbed", _logger.LogError);
        if (_ground.IsValid)
            Controller?.SetOrderWithTwoPositions(LineOrder(horizontal), _ground, _ground);
    }

    public bool LineBegin(float rangedX, float rangedY)
    {
        _lineHasBegin = false;
        var team = _mission.PlayerTeam;
        if (team == null || !TryGround(rangedX, rangedY, out var ground))
            return false;

        var probe = ground;
        if (!_mission.IsFormationUnitPositionAvailable(ref probe, team))
            return false;

        _lineBegin = ground;
        _lineHasBegin = true;
        return true;
    }

    public void LinePreview(float rangedX, float rangedY, bool horizontal)
    {
        var controller = Controller;
        if (!_lineHasBegin || controller == null || !TryGround(rangedX, rangedY, out var end))
            return;

        controller.SimulateNewOrderWithPositionAndDirection(_lineBegin, end, out var frames, !horizontal);
        _flagPoints.Clear();
        foreach (var frame in frames)
            _flagPoints.Add(frame.GetGroundVec3());
        _flags.Show(_flagPoints);
    }

    public void LineCommit(float rangedX, float rangedY, bool horizontal)
    {
        MissionThreadGuard.NoteCall("SeatCommandAdapter.LineCommit", _logger.LogError);
        var controller = Controller;
        var begin = _lineBegin;
        var had = _lineHasBegin;
        LineCancel();
        if (!had || controller == null || !TryGround(rangedX, rangedY, out var end))
            return;

        controller.SetOrderWithTwoPositions(LineOrder(horizontal), begin, end);
    }

    public void LineCancel()
    {
        _lineHasBegin = false;
        if (_flags.Shown > 0)
            _flags.Hide();
    }

    public void Drop()
    {
        _lineHasBegin = false;
        _flags.Drop();
        if (_focus != null)
            _focus.OnFormationFocused -= OnFocused;
        _focus = null;
        _focused = null;
        _pickedOwn = null;
        _enemy = null;
        _orderable = null;
    }

    private static OrderType LineOrder(bool horizontal) =>
        horizontal ? OrderType.MoveToLineSegmentWithHorizontalLayout : OrderType.MoveToLineSegment;

    // The ray of the order placer: the pointer's, thickness 0.3, the focus ray flags with flora (OrderTroopPlacer.cs:202-218, v1.5.7).
    private bool TryGround(float rangedX, float rangedY, out WorldPosition ground)
    {
        _screen.ScreenPointToWorldRay(new Vec2(rangedX, rangedY), out var begin, out var end);
        end = SeatOrderRay.Far(begin, end);
        if (!_mission.Scene.RayCastForClosestEntityOrTerrain(begin, end, out var distance, out Vec3 _, 0.3f, SeatOrderRay.RayFlags))
        {
            ground = WorldPosition.Invalid;
            return false;
        }

        var direction = end - begin;
        direction.Normalize();
        ground = new WorldPosition(_mission.Scene, UIntPtr.Zero, begin + direction * distance, hasValidZ: false);
        return true;
    }

    // The nearest enemy soldier across every enemy team, so the order of the teams never decides which of two companies under the pointer is charged.
    private Formation? EnemyAt(Team playerTeam, float pixelX, float pixelY)
    {
        var enemies = _mission.Teams.Where(team => team != null && team.IsEnemyOf(playerTeam)).SelectMany(team => team.ActiveAgents);
        var formation = Nearest(enemies, pixelX, pixelY, "enemy")?.Formation;
        if (formation != null && formation.CountOfUnits > 0)
            return formation;

        // The focus handler names a company near the pointer by its own reach (70 px at 1080p, the placer's own), which the Seat's patch points at
        // the cursor. So a right click within that reach of an enemy company charges it, even where the soldier pick found nobody.
        var focused = FocusedFormation();
        return focused != null && focused.CountOfUnits > 0 && focused.Team != null && focused.Team.IsEnemyOf(playerTeam) ? focused : null;
    }

    // Once, at the first fact read of the mission, so the handler's event has been heard before the first right click needs it.
    private void SubscribeFocus()
    {
        if (_focusSearched)
            return;
        _focusSearched = true;
        _focus = _mission.GetMissionBehavior<MissionFormationTargetSelectionHandler>();
        if (_focus != null)
            _focus.OnFormationFocused += OnFocused;
    }

    private void OnFocused(Formation formation) => _focused = formation;

    private Formation? FocusedFormation()
    {
        SubscribeFocus();
        return _focused;
    }

    // The soldier whose chest is nearest the pointer, within reach, and not hidden by the ground, over a team's active agents. A mount is never
    // named: the rider is the soldier, and a riderless mount is not one.
    private Agent? Nearest(IEnumerable<Agent> candidates, float pixelX, float pixelY, string side)
    {
        var camera = _screen.CombatCamera;
        if (camera == null)
            return null;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        _agents.Clear();
        _units.Clear();
        foreach (var agent in candidates)
        {
            if (!agent.IsHuman || !agent.IsActive() || agent.Formation == null)
                continue;

            float x = 0f, y = 0f, w = 0f;
            MBWindowManager.WorldToScreen(camera, agent.GetChestGlobalPosition(), ref x, ref y, ref w);
            if (!(w > 0f))
                continue;

            _agents.Add(agent);
            _units.Add(new FordsControlCandidate(x, y, false));
        }

        var winner = SeatPick.Winner(_units, pixelX, pixelY, Screen.RealScreenResolutionHeight);
        Agent? picked = null;
        if (winner >= 0)
        {
            var chest = _agents[winner].GetChestGlobalPosition();
            var origin = camera.Position;
            var distance = (chest - origin).Length;
            var terrainHit = _mission.Scene.RayCastForClosestEntityOrTerrain(origin, chest, out var terrainDistance, out _, out _, SeatOrderRay.RayThickness,
                SeatOrderRay.RayFlags);
            if (SeatKeepRule.AgentCounts(terrainHit, terrainDistance, SeatPick.TestedDistance(distance)))
                picked = _agents[winner];
        }

        _logger.LogInfo($"[SeatOfSeeing][Mouse] pick side={side} pointer=({pixelX:0},{pixelY:0}) seen={_units.Count} picked={(picked == null ? "none" : "yes")} ms={clock.Elapsed.TotalMilliseconds:0.0}");
        return picked;
    }
}
