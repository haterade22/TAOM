using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerPersistenceBehavior : CampaignBehaviorBase
{
    private readonly ICareerDataService _dataService;
    private readonly IModLogger _logger;

    public CareerPersistenceBehavior(ICareerDataService dataService, IModLogger logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
    }

    public override void SyncData(IDataStore dataStore)
    {
        _logger.LogInfo("CareerSystem: SyncData called — serializing career data");
        // Serialize as flat primitive dictionaries to avoid SaveableTypeDefiner requirement
        var careerIds = new Dictionary<string, string>();
        var choiceLists = new Dictionary<string, string>();
        var tierLists = new Dictionary<string, string>();

        // Save path: flatten HeroCareerData into primitive dicts
        var allData = _dataService.GetAllData();
        foreach (var kvp in allData)
        {
            var heroId = kvp.Key;
            var data = kvp.Value;
            if (!string.IsNullOrEmpty(data.CareerStringId))
                careerIds[heroId] = data.CareerStringId;
            if (data.ChoiceIds.Count > 0)
                choiceLists[heroId] = string.Join(",", data.ChoiceIds);
            if (data.TierUnlocks.Count > 0)
                tierLists[heroId] = string.Join(",", data.TierUnlocks);
        }

        dataStore.SyncData("_taom_careerIds", ref careerIds);
        dataStore.SyncData("_taom_careerChoices", ref choiceLists);
        dataStore.SyncData("_taom_careerTiers", ref tierLists);

        // Load path: reconstruct HeroCareerData from primitive dicts
        var restored = new Dictionary<string, HeroCareerData>();
        if (careerIds != null)
        {
            foreach (var kvp in careerIds)
            {
                var heroData = new HeroCareerData(kvp.Key) { CareerStringId = kvp.Value };
                restored[kvp.Key] = heroData;
            }
        }
        if (choiceLists != null)
        {
            foreach (var kvp in choiceLists)
            {
                if (!restored.TryGetValue(kvp.Key, out var heroData))
                {
                    heroData = new HeroCareerData(kvp.Key);
                    restored[kvp.Key] = heroData;
                }
                foreach (var choiceId in kvp.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    heroData.AddChoice(choiceId);
            }
        }
        if (tierLists != null)
        {
            foreach (var kvp in tierLists)
            {
                if (!restored.TryGetValue(kvp.Key, out var heroData))
                {
                    heroData = new HeroCareerData(kvp.Key);
                    restored[kvp.Key] = heroData;
                }
                foreach (var tierStr in kvp.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(tierStr, out var tier))
                        heroData.TierUnlocks.Add(tier);
                }
            }
        }

        _dataService.RestoreData(restored);
        _logger.LogInfo($"CareerSystem: SyncData complete — restored {restored.Count} heroes' career data (careerIds={careerIds?.Count ?? 0}, choiceLists={choiceLists?.Count ?? 0}, tierLists={tierLists?.Count ?? 0})");
    }
}
