using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerQuestService : ICareerQuestService
{
    private readonly ICareerQuestConfigProvider _configProvider;
    private readonly ICareerRegistry _registry;
    private readonly ICareerDataService _dataService;
    private readonly IModLogger _logger;

    private Dictionary<string, CareerQuestDefinition> _byCareerTier;
    private Dictionary<string, CareerQuestDefinition> _byId;

    public CareerQuestService(
        ICareerQuestConfigProvider configProvider,
        ICareerRegistry registry,
        ICareerDataService dataService,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _registry = registry;
        _dataService = dataService;
        _logger = logger;
    }

    public CareerQuestDefinition GetQuestFor(string careerId, int tier)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(careerId)) return null;
        return _byCareerTier.TryGetValue(Key(careerId, tier), out var q) ? q : null;
    }

    public CareerQuestDefinition GetQuestById(string questId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(questId)) return null;
        return _byId.TryGetValue(questId, out var q) ? q : null;
    }

    public IReadOnlyList<CareerQuestDefinition> GetAllQuests() => _configProvider.LoadQuests();

    public bool IsTierUnlocked(int heroLevel, int tier, string heroId)
    {
        if (_registry.IsTierAvailable(heroLevel, tier)) return true;
        return !string.IsNullOrEmpty(heroId) && _dataService.IsTierUnlocked(heroId, tier);
    }

    public int ComputeProgress(CareerQuestObjectiveType type, int currentProgress, int observedValue)
    {
        if (currentProgress < 0) currentProgress = 0;
        if (observedValue < 0) observedValue = 0;
        return IsThresholdObjective(type)
            ? Math.Max(currentProgress, observedValue)   // bank the highest seen
            : currentProgress + observedValue;            // accumulate
    }

    public bool IsObjectiveSatisfied(CareerQuestObjectiveDefinition objective, int progress)
    {
        return objective != null && progress >= objective.Target;
    }

    public bool AreAllObjectivesSatisfied(CareerQuestDefinition quest, IReadOnlyList<int> progress)
    {
        if (quest == null || progress == null) return false;
        if (quest.Objectives.Count == 0) return false;
        if (progress.Count < quest.Objectives.Count) return false;

        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            if (!IsObjectiveSatisfied(quest.Objectives[i], progress[i]))
                return false;
        }
        return true;
    }

    public void ApplyRewards(string heroId, CareerQuestDefinition quest, IQuestHeroAdapter hero)
    {
        if (quest == null) return;

        foreach (var reward in quest.Rewards)
        {
            switch (reward.Type)
            {
                case CareerQuestRewardType.UnlockTier:
                    _dataService.UnlockTier(heroId, reward.Amount);
                    _logger.LogInfo($"CareerQuest '{quest.Id}': unlocked tier {reward.Amount} for hero '{heroId}'");
                    break;

                case CareerQuestRewardType.GrantAttributeFlag:
                    _dataService.SetFlag(heroId, reward.Param);
                    _logger.LogInfo($"CareerQuest '{quest.Id}': set flag '{reward.Param}' for hero '{heroId}'");
                    break;

                case CareerQuestRewardType.GrantItem:
                    if (HeroOk(hero, quest, reward)) hero.AddItemToInventory(reward.Param, reward.Amount);
                    break;

                case CareerQuestRewardType.GrantRenown:
                    if (HeroOk(hero, quest, reward)) hero.AddRenown(reward.Amount);
                    break;

                case CareerQuestRewardType.GrantInfluence:
                    if (HeroOk(hero, quest, reward)) hero.AddInfluence(reward.Amount);
                    break;
            }
        }
    }

    private bool HeroOk(IQuestHeroAdapter hero, CareerQuestDefinition quest, CareerQuestRewardDefinition reward)
    {
        if (hero != null && hero.IsValid) return true;
        _logger.LogWarning($"CareerQuest '{quest.Id}': cannot apply {reward.Type} — hero adapter invalid");
        return false;
    }

    private static bool IsThresholdObjective(CareerQuestObjectiveType type)
    {
        switch (type)
        {
            case CareerQuestObjectiveType.SkillThreshold:
            case CareerQuestObjectiveType.RenownThreshold:
            case CareerQuestObjectiveType.GoldAccumulated:
                return true;
            default:
                return false; // count objectives accumulate
        }
    }

    private void EnsureLoaded()
    {
        if (_byCareerTier != null) return;
        _byCareerTier = new Dictionary<string, CareerQuestDefinition>(StringComparer.OrdinalIgnoreCase);
        _byId = new Dictionary<string, CareerQuestDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in _configProvider.LoadQuests())
        {
            _byId[quest.Id] = quest;   // ids are already de-duplicated by the config provider
            var key = Key(quest.CareerId, quest.Tier);
            if (_byCareerTier.ContainsKey(key))
            {
                _logger.LogWarning($"CareerQuest: duplicate quest for career '{quest.CareerId}' tier {quest.Tier} — keeping the first, ignoring '{quest.Id}'");
                continue;
            }
            _byCareerTier[key] = quest;
        }
    }

    private static string Key(string careerId, int tier) => careerId + "|" + tier;
}
