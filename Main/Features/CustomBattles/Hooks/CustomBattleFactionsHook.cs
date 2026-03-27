using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.Core;

namespace TAOM.Features.CustomBattles.Hooks;

public class CustomBattleFactionsHook : IOnGetCustomBattleFactions
{
    private readonly ICustomBattleService _service;
    private readonly IObjectManagerAdapter _objectManager;
    private readonly IModLogger _logger;

    public CustomBattleFactionsHook(
        ICustomBattleService service,
        IObjectManagerAdapter objectManager,
        IModLogger logger)
    {
        _service = service;
        _objectManager = objectManager;
        _logger = logger;
    }

    public void OnGetCustomBattleFactions(ref IEnumerable<BasicCultureObject> factions)
    {
        var factionIds = _service.GetFactionIds();
        var resolved = factionIds
            .Select(id => _objectManager.GetBasicCulture(id))
            .Where(c => c != null)
            .ToList();

        _logger.LogInfo($"CustomBattleFactionsHook: Loaded {resolved.Count} TAOM factions");
        factions = resolved;
    }
}
