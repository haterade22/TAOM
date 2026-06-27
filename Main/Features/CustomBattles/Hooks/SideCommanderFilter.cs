using System.Collections.Generic;
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
        var resolved = new List<BasicCharacterObject>();
        foreach (var id in ids)
        {
            var character = _objectManager.GetBasicCharacter(id);
            if (character != null)
                resolved.Add(character);
            else
                _logger.LogWarning($"SideCommanderFilter: commander id '{id}' for culture '{cultureId}' did not resolve to a character — skipped");
        }

        return resolved;
    }
}
