using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HeroAbilities;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.StoryMissions;

/// <summary>
/// Reads the story roster check's facts (#1366): the troop ids of every story mission's plan (<see cref="RunPlanTroops.AllTroopIds"/>, the list the launcher refuses a
/// missing troop by) and the extra arrow item of every hero kit, each looked up through <see cref="IObjectManagerAdapter"/>. One <see cref="StoryRosterCheck.Id"/> line,
/// once per process. Never throws.
/// </summary>
internal sealed class StoryRosterService
{
    private readonly StoryMissionCatalog _catalog;
    private readonly IHeroAbilitiesConfigProvider _heroConfig;
    private readonly IObjectManagerAdapter _objects;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;

    public StoryRosterService(StoryMissionCatalog catalog, IHeroAbilitiesConfigProvider heroConfig, IObjectManagerAdapter objects, IOutcomeLog outcomes, IModLogger logger)
    {
        _catalog = catalog;
        _heroConfig = heroConfig;
        _objects = objects;
        _outcomes = outcomes;
        _logger = logger;
    }

    public void Report()
    {
        try
        {
            var troops = _catalog.Missions
                .SelectMany(m => RunPlanTroops.AllTroopIds(StoryPlanBuilder.Build(m, RunOptions.Default)))
                .Distinct()
                .ToList();
            var items = _heroConfig.GetConfig().Heroes
                .Select(k => k.Ranged?.ExtraArrowItem ?? "")
                .Where(id => id.Length > 0)
                .Distinct()
                .ToList();
            LongDefeatChecks.Record(_outcomes, StoryRosterCheck.Id, "",
                StoryRosterCheck.Judge(_catalog.Missions.Count, troops.Count, troops.Where(id => _objects.GetBasicCharacter(id) == null).ToList(),
                    items.Count, items.Where(id => !_objects.ItemExists(id)).ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[StoryMissions] the roster check failed: {ex}");
        }
    }
}
