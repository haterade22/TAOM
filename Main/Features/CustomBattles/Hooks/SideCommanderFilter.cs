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
        return ids
            .Select(id => _objectManager.GetBasicCharacter(id))
            .Where(c => c != null)
            .ToList();
    }
}
