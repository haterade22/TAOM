using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>
/// The command mouse steps' window onto the engine. A click is a <see cref="SeatPointerFrame"/> queued in the Seat's own adapter, which reads it
/// in place of the mouse, so the whole path from the screen tick to the order runs; no operating system input is sent. The pixel of a point is the
/// engine's own projection (<c>MBWindowManager.WorldToScreen</c>), and its fraction of the window is the pixel over the resolution; the step's line
/// prints both, which is the evidence if they ever disagree at another UI scale.
/// </summary>
internal sealed partial class CommandTrialHost
{
    private Formation? _mouseFirst;
    private Formation? _mouseSecond;
    private Formation? _mouseEnemy;

    private OrderController? Orders => _mission.PlayerTeam?.PlayerOrderController;

    public void SetCommandMouse(bool on)
    {
        var settings = CommandTrialRun.TrialSettings;
        if (settings != null)
            settings.CommandMouse = on;
    }

    public bool CommandLive => _session.CommandLive;

    public bool MouseChooseSoldier(bool otherCompany)
    {
        var camera = _screen.CombatCamera;
        var team = _mission.PlayerTeam;
        if (camera == null || team == null)
            return false;

        Agent? best = null;
        var nearest = float.MaxValue;
        foreach (var agent in team.ActiveAgents)
        {
            if (!agent.IsHuman || !agent.IsActive() || agent == _mission.MainAgent || agent.Formation == null || agent.Formation.CountOfUnits <= 0)
                continue;
            if (otherCompany && ReferenceEquals(agent.Formation, _mouseFirst))
                continue;
            if (Orders is { } controller && !controller.IsFormationSelectable(agent.Formation))
                continue;

            var distance = (agent.Position - camera.Position).LengthSquared;
            if (distance < nearest)
            {
                nearest = distance;
                best = agent;
            }
        }

        if (best == null)
            return false;

        SetChosen(best);
        if (otherCompany)
            _mouseSecond = best.Formation;
        else
            _mouseFirst = best.Formation;
        return true;
    }

    public bool MouseChooseEnemy()
    {
        var camera = _screen.CombatCamera;
        var team = _mission.PlayerTeam;
        if (camera == null || team == null)
            return false;

        Agent? best = null;
        var nearest = float.MaxValue;
        foreach (var other in _mission.Teams)
        {
            if (other == null || !other.IsEnemyOf(team))
                continue;
            foreach (var agent in other.ActiveAgents)
            {
                if (!agent.IsHuman || !agent.IsActive() || agent.Formation == null || agent.Formation.CountOfUnits <= 0)
                    continue;
                var distance = (agent.Position - camera.Position).LengthSquared;
                if (distance < nearest)
                {
                    nearest = distance;
                    best = agent;
                }
            }
        }

        if (best == null)
            return false;

        SetChosen(best);
        _mouseEnemy = best.Formation;
        return true;
    }

    private void SetChosen(Agent agent)
    {
        _chosenFormation = null;
        _chosenAgent = agent;
    }

    public void MouseSelectFirstCompany()
    {
        NoteWrite("CommandTrialHost.MouseSelectFirstCompany");
        var controller = Orders;
        if (controller == null || _mouseFirst == null)
            return;

        controller.ClearSelectedFormations();
        if (controller.IsFormationSelectable(_mouseFirst))
            controller.SelectFormation(_mouseFirst);
    }

    public SeatMousePoint MouseProjectChosen()
    {
        var agent = _chosenAgent;
        if (agent == null || !AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsActive())
            return default;

        var chest = agent.GetChestGlobalPosition();
        return Project(chest.x, chest.y, chest.z);
    }

    public SeatMousePoint MouseGround(float rightMetres)
    {
        var centre = ViewCentreOnGround();
        var heading = ViewForward();
        if (centre == null || heading == null)
            return default;

        // The view's right is the heading turned a quarter clockwise (the edge_pan step's own convention).
        var x = centre.Value.X + heading.Value.Y * rightMetres;
        var y = centre.Value.Y - heading.Value.X * rightMetres;
        var z = _adapter.GroundHeight(x, y, centre.Value.Z + 50f);
        return float.IsNaN(z) ? default : Project(x, y, z);
    }

    private SeatMousePoint Project(float x, float y, float z)
    {
        var camera = _screen.CombatCamera;
        if (camera == null)
            return default;

        float px = 0f, py = 0f, w = 0f;
        MBWindowManager.WorldToScreen(camera, new Vec3(x, y, z, -1f), ref px, ref py, ref w);
        var width = Screen.RealScreenResolutionWidth;
        var height = Screen.RealScreenResolutionHeight;
        if (!(w > 0f) || px < 0f || py < 0f || px > width || py > height)
            return default;

        return new SeatMousePoint(true, px, py, px / width, py / height, x, y);
    }

    public void MouseClick(SeatMousePoint at, bool right, bool shift, bool ctrl)
    {
        var now = Time.ApplicationTime;
        _adapter.TrialPointer.Enqueue(new SeatPointerFrame(!right, !right, right, right, false, at.PixelX, at.PixelY, at.RangedX, at.RangedY, shift, ctrl, now));
    }

    public void MouseDragRight(SeatMousePoint from, SeatMousePoint to)
    {
        var now = Time.ApplicationTime;
        var held = now + SeatTrialMouse.DragSeconds;
        _adapter.TrialPointer.Enqueue(new SeatPointerFrame(false, false, true, false, true, from.PixelX, from.PixelY, from.RangedX, from.RangedY, false, false, now));
        _adapter.TrialPointer.Enqueue(new SeatPointerFrame(false, false, false, false, true, to.PixelX, to.PixelY, to.RangedX, to.RangedY, false, false, held));
        _adapter.TrialPointer.Enqueue(new SeatPointerFrame(false, false, false, true, false, to.PixelX, to.PixelY, to.RangedX, to.RangedY, false, false, held + 0.1f));
    }

    public int MouseSelectedCount => Orders?.SelectedFormations.Count ?? 0;

    public bool MouseOnlyFirstSelected => OnlySelected(_mouseFirst, null);

    public bool MouseFirstAndSecondSelected => OnlySelected(_mouseFirst, _mouseSecond);

    private bool OnlySelected(Formation? first, Formation? second)
    {
        var selected = Orders?.SelectedFormations;
        if (selected == null || first == null)
            return false;
        var expected = second == null ? 1 : 2;
        return selected.Count == expected && selected.Contains(first) && (second == null || selected.Contains(second));
    }

    private Formation? FirstSelected => Orders is { SelectedFormations.Count: > 0 } controller ? controller.SelectedFormations[0] : null;

    public float MouseOrderDistance(SeatMousePoint to)
    {
        var formation = FirstSelected;
        if (formation == null || !formation.OrderPositionIsValid)
            return float.NaN;
        return formation.OrderPosition.Distance(new Vec2(to.WorldX, to.WorldY));
    }

    public float MouseFacingError(SeatMousePoint from, SeatMousePoint to)
    {
        var formation = FirstSelected;
        if (formation == null)
            return float.NaN;
        var facing = formation.FacingOrder.GetDirection(formation);
        return SeatTrialMouse.NormalError(to.WorldX - from.WorldX, to.WorldY - from.WorldY, facing.x, facing.y);
    }

    public bool MouseChargesChosenEnemy
    {
        get
        {
            var formation = FirstSelected;
            return formation != null && _mouseEnemy != null && formation.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Charge
                && ReferenceEquals(formation.TargetFormation, _mouseEnemy);
        }
    }

    public void ReleaseMouseTrial()
    {
        SetCommandMouse(false);
        _adapter.TrialPointer.Clear();
        MarshalReleaseKeys();
    }
}
