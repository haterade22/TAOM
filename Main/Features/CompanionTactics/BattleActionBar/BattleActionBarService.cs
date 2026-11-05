using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// Resolves the contextual battle-action buttons for a selected formation. Composition
/// flags drive the categories shown; all actions on the bar set per-formation stance state
/// (display-only) — they don't change formation behavior because v1.3.15 doesn't expose
/// the firing-order / tighten-spacing APIs the original mod referenced.
/// </summary>
public sealed class BattleActionBarService : IBattleActionBarService
{
    private readonly ICompanionTacticsSettingsProvider _settings;
    private readonly IFormationCompositionAnalyzer _analyzer;
    private readonly ITroopStanceManager _stances;
    private readonly TAOM.Core.Logging.IModLogger _logger;

    public BattleActionBarService(
        ICompanionTacticsSettingsProvider settings,
        IFormationCompositionAnalyzer analyzer,
        ITroopStanceManager stances,
        TAOM.Core.Logging.IModLogger logger)
    {
        _settings = settings;
        _analyzer = analyzer;
        _stances = stances;
        _logger = logger;
    }

    public IReadOnlyList<BattleAction> GetActionsForFormation(IFormationAdapter formation)
    {
        if (formation == null) return Array.Empty<BattleAction>();
        if (!_settings.ShowBattleActionBar) return Array.Empty<BattleAction>();

        var composition = _analyzer.Analyze(formation);
        var formationIndex = formation.FormationIndex;
        var actions = new List<BattleAction>(8);

        // BattleActionBarDebug — gated diagnostic for composition resolution. Off by default.
        if (_settings.BattleActionBarDebug && _logger != null)
        {
            _logger.LogDebug($"[BattleActionBar] formation={formationIndex} ranged={composition.HasRanged} polearm={composition.HasPolearm} shields={composition.HasShields} cavalry={composition.HasCavalry}");
        }

        if (composition.HasRanged)
        {
            actions.Add(new BattleAction(
                "action_hold_fire", "Hold Fire", ActionCategory.Ranged,
                () => { /* Display-only; ranged firing-order API not exposed in v1.3.15. */ }));
            actions.Add(new BattleAction(
                "action_free_fire", "Free Fire", ActionCategory.Ranged,
                () => { /* Display-only. */ }));
            if (_settings.EnableVolleyFire)
            {
                actions.Add(new BattleAction(
                    "action_volley", "Volley Fire", ActionCategory.Ranged,
                    () => { /* Display-only. */ }));
            }
        }

        if (composition.HasPolearm)
        {
            actions.Add(new BattleAction(
                "action_brace", "Brace for Cavalry", ActionCategory.Polearm,
                () => Guarded(() => _stances.SetStance(formationIndex, TroopStance.BracedForCavalry))));
            actions.Add(new BattleAction(
                "action_pike_wall", "Pike Wall", ActionCategory.Polearm,
                () => Guarded(() => _stances.SetStance(formationIndex, TroopStance.PikeWall))));
        }

        if (composition.HasShields)
        {
            actions.Add(new BattleAction(
                "action_shield_wall", "Shield Wall", ActionCategory.Shield,
                () => { /* Display-only. */ }));
            actions.Add(new BattleAction(
                "action_testudo", "Testudo", ActionCategory.Shield,
                () => Guarded(() => _stances.SetStance(formationIndex, TroopStance.Testudo))));
        }

        if (composition.HasCavalry)
        {
            actions.Add(new BattleAction(
                "action_charge", "Line Charge", ActionCategory.Cavalry,
                () => Guarded(() => _stances.SetStance(formationIndex, TroopStance.LineCharge))));
            actions.Add(new BattleAction(
                "action_skirmish", "Skirmish", ActionCategory.Cavalry,
                () => Guarded(() => _stances.SetStance(formationIndex, TroopStance.Skirmish))));
        }

        return actions;
    }

    // A click can arrive after the setting turned off mid-battle; record nothing then.
    private void Guarded(Action record)
    {
        if (_settings.ShowBattleActionBar) record();
    }
}
