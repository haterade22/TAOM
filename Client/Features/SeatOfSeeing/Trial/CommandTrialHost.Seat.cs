using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal sealed partial class CommandTrialHost
{
    // The focus handler's reference point (v1.5.6 MissionFormationTargetSelectionHandler.cs:36), which the field trial reads to see that the
    // pointer reached it. Cached once.
    private static readonly FieldInfo? FocusCentre = AccessTools.Field(typeof(MissionFormationTargetSelectionHandler), "_centerOfScreen");

    private MissionFormationTargetSelectionHandler? _focusHandler;
    private Formation? _focused;
    private bool _watching;
    private Agent? _chosenAgent;
    private Formation? _chosenFormation;
    private float _wantedX = float.NaN;
    private float _wantedY = float.NaN;

    public void PressSeatKey() => _view.PressSeatKey();

    public bool PressPending => _view.PressPending;

    public void SetInput(SeatInput? input) => _view.TrialInput = input;

    public SeatState State => _session.State;

    public int Frames => _session.Frames;

    public bool GlideBegan => _session.GlideBegan;

    public bool Faulted => _session.Faulted;

    public void PressHide() => _session.PressHide();

    public bool PanelOpen => _adapter.IsPanelOpen();

    public bool MouseVisible => _adapter.MouseVisible;

    public bool UiHidden => _adapter.ReadUiHidden();

    public bool OpenPanel() => _adapter.OpenPanel();

    public void ClosePanel(bool asPlayer)
    {
        _adapter.TrialPlayerClose = asPlayer;
        _adapter.ClosePanel();
    }

    public bool WindowFocused => ScreenManager.IsWindowFocused;

    public void MoveCursorTo(float fractionX, float fractionY)
    {
        WatchFocus();
        _wantedX = (int)(fractionX * Screen.RealScreenResolutionWidth);
        _wantedY = (int)(fractionY * Screen.RealScreenResolutionHeight);
        Input.SetMousePosition((int)_wantedX, (int)_wantedY);
    }

    public SeatFocusReading ProbeFocus()
    {
        WatchFocus();
        var pixel = Input.MousePositionPixel;
        var ranged = Input.MousePositionRanged;
        var usable = ScreenManager.UsableArea;
        var expectedX = float.NaN;
        var expectedY = float.NaN;
        if (SeatPointer.TryInUsableArea(ranged.x, ranged.y, Screen.RealScreenResolutionWidth, Screen.RealScreenResolutionHeight, usable.X, usable.Y,
                out var x, out var y))
        {
            expectedX = x;
            expectedY = y;
        }

        var field = FocusCentre != null && _focusHandler != null && FocusCentre.GetValue(_focusHandler) is Vec2 centre ? centre : new Vec2(float.NaN, float.NaN);
        return new SeatFocusReading(pixel.x, pixel.y, _wantedX, _wantedY, expectedX, expectedY, field.x, field.y, _focused?.RepresentativeClass.ToString() ?? "none");
    }

    public int Said => _adapter.Said;

    public SeatMessage? LastSaid => _adapter.LastSaid;

    public void PressKeep() => _session.PressKeep();

    public SeatKeepKind Keeping => _session.Keep;

    public bool KeepsChosen => (_chosenAgent != null && _adapter.KeepsAgent(_chosenAgent))
        || (_chosenFormation != null && _adapter.KeepsFormation(_chosenFormation));

    public float DistanceToKept()
    {
        var read = _adapter.ReadKept();
        var camera = _screen.CombatCamera;
        if (read.Gone || camera == null)
            return float.NaN;

        return camera.Position.Distance(new Vec3(read.X, read.Y, read.Z));
    }

    public bool ChooseSoldier()
    {
        _chosenAgent = null;
        _chosenFormation = null;
        var camera = _screen.CombatCamera;
        var team = _mission.PlayerTeam;
        if (camera == null || team == null)
            return false;

        var best = float.MaxValue;
        foreach (var agent in team.ActiveAgents)
        {
            if (!agent.IsHuman || agent.IsMount || !agent.IsActive() || agent == _mission.MainAgent)
                continue;
            var distance = (agent.Position - camera.Position).LengthSquared;
            if (distance < best)
            {
                best = distance;
                _chosenAgent = agent;
            }
        }

        return _chosenAgent != null;
    }

    public Vec3f? ViewForward() => _adapter.TryReadCamera(out var camera, out _) ? FordsCameraMath.Forward(camera.Yaw) : null;

    public SeatAimStep AimAtChosen()
    {
        var agent = _chosenAgent;
        if (agent == null || !AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsActive())
            return new SeatAimStep(default, false, true);
        if (!_adapter.TryReadCamera(out var camera, out _))
            return new SeatAimStep(default, false, false);

        var chest = agent.GetChestGlobalPosition();
        var (lookYaw, lookPitch, error) = SeatTrialAim.Step(camera.X, camera.Y, camera.Z, camera.Yaw, camera.Pitch, chest.x, chest.y, chest.z);
        return new SeatAimStep(new SeatInput(0f, 0f, 0f, 0f, lookYaw, lookPitch, false), SeatTrialAim.OnTarget(error), false);
    }

    public int ChooseCompany(bool largest)
    {
        _chosenAgent = null;
        _chosenFormation = null;
        var team = _mission.PlayerTeam;
        if (team == null)
            return 0;

        var formations = new List<Formation>();
        var rows = new List<(int Units, bool HoldsHero)>();
        var hero = _mission.MainAgent;
        foreach (var formation in team.FormationsIncludingEmpty)
        {
            if (formation.CountOfUnits <= 0)
                continue;
            formations.Add(formation);
            rows.Add((formation.CountOfUnits, hero != null && hero.Formation == formation));
        }

        var index = largest ? SeatTrialRules.Largest(rows) : SeatTrialRules.SmallestKillable(rows);
        if (index < 0)
            return 0;
        _chosenFormation = formations[index];
        return rows[index].Units;
    }

    public void SelectChosenCompany()
    {
        NoteWrite("CommandTrialHost.SelectChosenCompany");
        var controller = _mission.PlayerTeam?.PlayerOrderController;
        if (controller == null || _chosenFormation == null)
            return;

        controller.ClearSelectedFormations();
        controller.SelectFormation(_chosenFormation);
    }

    // The same gate as AimAtChosen: the handle is held for seconds, and a deleted agent's slot answers for its next tenant.
    public void KillChosenSoldier()
    {
        var agent = _chosenAgent;
        if (agent != null && AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive())
            Kill(agent);
    }

    public void KillChosenCompany()
    {
        if (_chosenFormation == null)
            return;

        var units = new List<Agent>();
        _chosenFormation.ApplyActionOnEachUnit(units.Add);
        foreach (var unit in units)
            Kill(unit);
    }

    private void Kill(Agent agent)
    {
        NoteWrite("CommandTrialHost.Kill");
        _mission.KillAgentCheat(agent);
    }

    private void NoteWrite(string site) => MissionThreadGuard.NoteCall(site, _logger.LogError);

    private void WatchFocus()
    {
        _focusHandler ??= _mission.GetMissionBehavior<MissionFormationTargetSelectionHandler>();
        if (_focusHandler == null || _watching)
            return;

        _focusHandler.OnFormationFocused += formation => _focused = formation;
        _watching = true;
    }
}
