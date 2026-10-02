using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.Core;

namespace TAOM.Features.CustomBattles.Hooks;

public class CustomBattleTroopHook : IOnGetDefaultTroopOfFormation
{
    private readonly ICustomBattleService _service;
    private readonly IObjectManagerAdapter _objectManager;
    private readonly IModLogger _logger;

    public CustomBattleTroopHook(
        ICustomBattleService service,
        IObjectManagerAdapter objectManager,
        IModLogger logger)
    {
        _service = service;
        _objectManager = objectManager;
        _logger = logger;
    }

    // TAOM's culture troop replaces vanilla's pick: for the six re-skinned cultures vanilla's switch returns a
    // Calradian troop (vlandia -> vlandian_swordsman), which SandBoxCore still loads for Custom Battle. The service
    // replaces a vanilla pick only with a troop vanilla's slot list can show (a soldier of the slot's culture and
    // class), so that pick stays wherever TAOM has no such troop, or the one it names does not resolve. With no vanilla
    // pick it may return a troop that does not fit, which Start spawns for a slot left empty.
    public void OnGetDefaultTroopOfFormation(string cultureId, int formationIndex, ref BasicCharacterObject result)
    {
        if (string.IsNullOrEmpty(cultureId))
            return;

        var troopId = _service.GetDefaultTroopIdForFormation(cultureId, formationIndex, vanillaHasPick: result != null);
        if (string.IsNullOrEmpty(troopId))
            return;

        var troop = _objectManager.GetBasicCharacter(troopId);
        if (troop != null)
        {
            result = troop;
            _logger.LogDebug($"CustomBattleTroopHook: Resolved {cultureId} formation {formationIndex} -> {troopId}");
        }
    }
}
