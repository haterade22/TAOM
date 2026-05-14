using System;
using System.Collections.Generic;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MixedFormations.Hooks;

public sealed class MixedFormationsMissionBehavior : MissionBehavior
{
    private readonly IFormationLayoutService _service;
    private readonly IMixedFormationsSettingsProvider _settings;
    private readonly IModLogger _logger;

    private float _autoApplyAccumulator;
    private bool _wasCycleKeyDown;

    // BehaviorType=Other: this class inherits MissionBehavior (not MissionLogic) and does not
    // override MissionEnded/OnMissionResultReady, so it has no business in Mission.MissionLogics.
    // Returning Logic here caused vanilla AddMissionBehavior to do `MissionLogics.Add(this as MissionLogic)`
    // which evaluates to null and NREs the next CheckMissionEnded tick.
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public MixedFormationsMissionBehavior()
    {
        _service = IoC.Resolve<IFormationLayoutService>();
        _settings = IoC.Resolve<IMixedFormationsSettingsProvider>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        if (!_settings.IsEnabled)
        {
            _logger.LogInfo("[MixedFormations] disabled via MCM — patches inert");
            return;
        }

        _logger.LogInfo($"[MixedFormations] mission init — defaultLayout={_settings.DefaultLayout}, cycleKey={_settings.CycleHotkey}");
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (!_settings.IsEnabled) return;

        _autoApplyAccumulator += dt;
        if (_autoApplyAccumulator >= 1f)
        {
            _autoApplyAccumulator = 0f;
            ApplyDefaultsToPlayerTeam();
        }

        HandleCycleHotkey();
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _service.OnMissionEnd();
    }

    public bool TryGetTeamAdapters(out IReadOnlyList<IFormationAdapter> all, out IReadOnlyList<IFormationAdapter> selected)
    {
        all = Array.Empty<IFormationAdapter>();
        selected = Array.Empty<IFormationAdapter>();

        var team = Mission.Current?.PlayerTeam;
        if (team == null) return false;

        var allAdapters = new List<IFormationAdapter>();
        foreach (var formation in team.FormationsIncludingEmpty)
            if (formation != null) allAdapters.Add(new FormationAdapter(formation));
        all = allAdapters;

        var selectedFormations = team.PlayerOrderController?.SelectedFormations;
        if (selectedFormations is { Count: > 0 })
        {
            var selectedAdapters = new List<IFormationAdapter>(selectedFormations.Count);
            foreach (var formation in selectedFormations)
                if (formation != null) selectedAdapters.Add(new FormationAdapter(formation));
            selected = selectedAdapters;
        }

        return true;
    }

    private void ApplyDefaultsToPlayerTeam()
    {
        if (!TryGetTeamAdapters(out var allFormations, out _)) return;
        _service.ApplyDefaultsToFormations(allFormations);
    }

    private void HandleCycleHotkey()
    {
        if (!TryResolveCycleKey(out var key))
        {
            _wasCycleKeyDown = false;
            return;
        }

        var isDown = Input.IsKeyDown(key);
        var firstFrameDown = isDown && !_wasCycleKeyDown;
        _wasCycleKeyDown = isDown;

        if (firstFrameDown) ExecuteCycle();
    }

    private bool TryResolveCycleKey(out InputKey key)
    {
        var raw = _settings.CycleHotkey?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            key = default;
            return false;
        }
        return Enum.TryParse(raw, ignoreCase: true, out key);
    }

    private void ExecuteCycle()
    {
        if (!TryGetTeamAdapters(out var allFormations, out var selectedFormations)) return;

        var target = selectedFormations.Count > 0 ? selectedFormations : allFormations;
        var scope = selectedFormations.Count > 0 ? "selected" : "all";

        var (newLayout, affected) = _service.CycleLayouts(target);
        if (affected > 0)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"[MixedFormations] Layout ({scope}) → {LayoutLabel(newLayout)}",
                Colors.Yellow));
        }
    }

    private static string LayoutLabel(Models.FormationLayoutType layout) => layout switch
    {
        Models.FormationLayoutType.InfantryFrontRangedBack => "Infantry front, Ranged back",
        Models.FormationLayoutType.RangedFrontInfantryBack => "Ranged front, Infantry back",
        Models.FormationLayoutType.RangedWingsInfantryCenter => "Ranged wings, Infantry center",
        Models.FormationLayoutType.Checkerboard => "Checkerboard",
        _ => layout.ToString(),
    };
}
