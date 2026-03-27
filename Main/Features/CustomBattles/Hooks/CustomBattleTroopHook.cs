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

    public void OnGetDefaultTroopOfFormation(string cultureId, int formationIndex, ref BasicCharacterObject result)
    {
        if (result != null)
            return;

        if (string.IsNullOrEmpty(cultureId))
            return;

        var troopId = _service.GetDefaultTroopIdForFormation(cultureId, formationIndex);
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
