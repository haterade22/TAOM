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

    // TEMP — Phase 2A diagnostic for naked commander preview investigation.
    // Logs equipment slot resolution per culture once. Remove when Phase 2C ships.
    private readonly HashSet<string> _diagnosedCultures = new HashSet<string>();

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
            return;
        _diagnosedCultures.Add(cultureId);

        foreach (var c in resolved)
        {
            string body = c.Equipment[EquipmentIndex.Body].Item?.StringId ?? "INVALID";
            string head = c.Equipment[EquipmentIndex.Head].Item?.StringId ?? "INVALID";
            string leg = c.Equipment[EquipmentIndex.Leg].Item?.StringId ?? "INVALID";
            string gloves = c.Equipment[EquipmentIndex.Gloves].Item?.StringId ?? "INVALID";
            string cape = c.Equipment[EquipmentIndex.Cape].Item?.StringId ?? "INVALID";
            string item0 = c.Equipment[EquipmentIndex.Weapon0].Item?.StringId ?? "INVALID";
            string item1 = c.Equipment[EquipmentIndex.Weapon1].Item?.StringId ?? "INVALID";
            string horse = c.Equipment[EquipmentIndex.Horse].Item?.StringId ?? "INVALID";
            _logger.LogInfo(
                $"[CustomBattles diag] {cultureId}/{c.StringId}: " +
                $"Body={body} Head={head} Leg={leg} Gloves={gloves} Cape={cape} " +
                $"Item0={item0} Item1={item1} Horse={horse}");
        }
    }
}
