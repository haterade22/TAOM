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
    private readonly TAOM.Core.Logging.IOutcomeLog _outcomes;

    public BattleActionBarService(
        ICompanionTacticsSettingsProvider settings,
        IFormationCompositionAnalyzer analyzer,
        ITroopStanceManager stances,
        TAOM.Core.Logging.IModLogger logger,
        TAOM.Core.Logging.IOutcomeLog outcomes)
    {
        _settings = settings;
        _analyzer = analyzer;
        _stances = stances;
        _logger = logger;
        _outcomes = outcomes;
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
                () => Guarded(formationIndex, TroopStance.BracedForCavalry)));
            actions.Add(new BattleAction(
                "action_pike_wall", "Pike Wall", ActionCategory.Polearm,
                () => Guarded(formationIndex, TroopStance.PikeWall)));
        }

        if (composition.HasShields)
        {
            actions.Add(new BattleAction(
                "action_shield_wall", "Shield Wall", ActionCategory.Shield,
                () => { /* Display-only. */ }));
            actions.Add(new BattleAction(
                "action_testudo", "Testudo", ActionCategory.Shield,
                () => Guarded(formationIndex, TroopStance.Testudo)));
        }

        if (composition.HasCavalry)
        {
            actions.Add(new BattleAction(
                "action_charge", "Line Charge", ActionCategory.Cavalry,
                () => Guarded(formationIndex, TroopStance.LineCharge)));
            actions.Add(new BattleAction(
                "action_skirmish", "Skirmish", ActionCategory.Cavalry,
                () => Guarded(formationIndex, TroopStance.Skirmish)));
        }

        return actions;
    }

    // A click can arrive after the setting turned off mid-battle; record nothing then. The [Check] line (#1072) says a
    // click reached the stance, once per stance for the process.
    private void Guarded(int formationIndex, TroopStance stance)
    {
        if (!_settings.ShowBattleActionBar) return;
        _stances.SetStance(formationIndex, stance);
        _outcomes.Pass(BattleActionBarChecks.StanceId, stance.ToString(),
            "stance=" + stance + " formation=" + formationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
