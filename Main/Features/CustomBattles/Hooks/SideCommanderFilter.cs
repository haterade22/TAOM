using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.Core;

namespace TAOM.Features.CustomBattles.Hooks;

public class SideCommanderFilter : ISideCommanderFilter
{
    public const int MaxCommandersPerCulture = 3;

    private readonly ICustomBattleService _service;
    private readonly IObjectManagerAdapter _objectManager;
    private readonly IModLogger _logger;

    // TEMP — Phase 2A diagnostic for naked commander preview investigation (issue #105).
    // Logs equipment slot resolution per culture once per Bannerlord process lifetime.
    // To re-capture after editing equipment XML, relaunch Bannerlord (singleton state persists
    // across Custom Battle screen open/close). Remove this whole diagnostic block when Phase 2C ships.
    private readonly HashSet<string> _diagnosedCultures = new HashSet<string>();
    private bool _resetHintLogged;

    public SideCommanderFilter(
        ICustomBattleService service,
        IObjectManagerAdapter objectManager,
        IModLogger logger)
    {
        _service = service;
        _objectManager = objectManager;
        _logger = logger;
    }

    public IReadOnlyList<BasicCharacterObject> ResolveCommandersForCulture(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId))
            return new List<BasicCharacterObject>();

        var ids = _service.GetCommanderIdsForFaction(cultureId, MaxCommandersPerCulture);
        var resolved = ids
            .Select(id => _objectManager.GetBasicCharacter(id))
            .Where(c => c != null)
            .ToList();

        LogEquipmentDiagnosticOnce(cultureId, resolved);
        return resolved;
    }

    private void LogEquipmentDiagnosticOnce(string cultureId, IReadOnlyList<BasicCharacterObject> resolved)
    {
        if (_diagnosedCultures.Contains(cultureId))
        {
            if (!_resetHintLogged)
            {
                _resetHintLogged = true;
                _logger.LogInfo(
                    $"[CustomBattles diag] Culture '{cultureId}' already captured this session — " +
                    "diagnostic state persists across Custom Battle screen open/close. " +
                    "Relaunch Bannerlord to re-capture equipment slot resolution after XML edits.");
            }
            return;
        }
        _diagnosedCultures.Add(cultureId);

        foreach (var c in resolved)
        {
            // Wrap per-commander to keep the production filter alive even if a single
            // BasicCharacterObject has unexpected null state. Also lets unit tests run
            // without stubbing the Equipment getter on BasicCharacterObject substitutes.
            try
            {
                var eq = c?.Equipment;
                if (eq == null)
                    continue;

                string body = eq[EquipmentIndex.Body].Item?.StringId ?? "INVALID";
                string head = eq[EquipmentIndex.Head].Item?.StringId ?? "INVALID";
                string leg = eq[EquipmentIndex.Leg].Item?.StringId ?? "INVALID";
                string gloves = eq[EquipmentIndex.Gloves].Item?.StringId ?? "INVALID";
                string cape = eq[EquipmentIndex.Cape].Item?.StringId ?? "INVALID";
                string item0 = eq[EquipmentIndex.Weapon0].Item?.StringId ?? "INVALID";
                string item1 = eq[EquipmentIndex.Weapon1].Item?.StringId ?? "INVALID";
                string horse = eq[EquipmentIndex.Horse].Item?.StringId ?? "INVALID";
                _logger.LogInfo(
                    $"[CustomBattles diag] {cultureId}/{c.StringId}: " +
                    $"Body={body} Head={head} Leg={leg} Gloves={gloves} Cape={cape} " +
                    $"Item0={item0} Item1={item1} Horse={horse}");
            }
            catch (System.Exception ex)
            {
                // Log the full ex.ToString() (type + message + stack) — this diagnostic exists
                // specifically to identify equipment-resolution failures, so the exception type
                // and stack frame are as valuable as the slot-by-slot output.
                _logger.LogWarning($"[CustomBattles diag] Equipment slot read threw for {c?.StringId ?? "<null>"} in {cultureId}: {ex}");
            }
        }
    }
}
