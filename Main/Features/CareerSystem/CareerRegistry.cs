using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerRegistry : ICareerRegistry
{
    private readonly ICareerConfigProvider _configProvider;
    private readonly IModLogger _logger;

    private Dictionary<string, CareerDefinition> _careers;
    private Dictionary<string, CareerChoiceDefinition> _choices;
    private Dictionary<string, CareerChoiceGroupDefinition> _groups;
    private List<CareerDefinition> _allCareers;
    private Dictionary<string, string> _careerIdByGroupId;
    private Dictionary<string, string> _careerIdByRootChoiceId;
    private int _maxPerkPoints;

    private static readonly IReadOnlyList<CareerChoiceDefinition> EmptyChoices = new List<CareerChoiceDefinition>();

    public CareerRegistry(ICareerConfigProvider configProvider, IModLogger logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public CareerDefinition GetCareer(string careerStringId)
    {
        EnsureLoaded();
        return _careers.TryGetValue(careerStringId, out var career) ? career : null;
    }

    public IReadOnlyList<CareerDefinition> GetAllCareers()
    {
        EnsureLoaded();
        return _allCareers;
    }

    public CareerChoiceDefinition GetChoice(string choiceStringId)
    {
        EnsureLoaded();
        return _choices.TryGetValue(choiceStringId, out var choice) ? choice : null;
    }

    public string GetOwningCareerId(string choiceStringId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(choiceStringId)) return null;

        // Root first: it resolves from taom_careers.xml alone, so a ghost root is still
        // identifiable when taom_career_choices.xml failed to load.
        if (_careerIdByRootChoiceId.TryGetValue(choiceStringId, out var rootOwner)) return rootOwner;

        if (!_choices.TryGetValue(choiceStringId, out var choice)) return null;
        if (string.IsNullOrEmpty(choice.GroupId)) return null;
        return _careerIdByGroupId.TryGetValue(choice.GroupId, out var careerId) ? careerId : null;
    }

    public CareerChoiceGroupDefinition GetGroup(string groupStringId)
    {
        EnsureLoaded();
        return _groups.TryGetValue(groupStringId, out var group) ? group : null;
    }

    public IReadOnlyList<CareerChoiceDefinition> GetChoicesForGroup(string groupStringId)
    {
        EnsureLoaded();
        if (!_groups.TryGetValue(groupStringId, out var group)) return EmptyChoices;

        var result = new List<CareerChoiceDefinition>(group.ChoiceIds.Count);
        foreach (var choiceId in group.ChoiceIds)
        {
            if (_choices.TryGetValue(choiceId, out var choice))
                result.Add(choice);
        }
        return result;
    }

    public bool IsEligible(string careerStringId, ICareerHeroAdapter hero)
    {
        EnsureLoaded();
        if (hero == null)
        {
            _logger.LogDebug($"CareerSystem: IsEligible — hero is null for career '{careerStringId}'");
            return false;
        }
        if (!_careers.TryGetValue(careerStringId, out var career))
        {
            _logger.LogWarning($"CareerSystem: IsEligible — career '{careerStringId}' not found in registry");
            return false;
        }

        if (hero.ClanTier < career.MinClanTier)
        {
            _logger.LogDebug($"CareerSystem: IsEligible — hero culture='{hero.CultureStringId}' clanTier={hero.ClanTier} < minClanTier={career.MinClanTier} for career '{careerStringId}'");
            return false;
        }

        if (career.EligibleCultureIds.Count > 0)
        {
            var heroCulture = hero.CultureStringId;
            var found = false;
            foreach (var cultureId in career.EligibleCultureIds)
            {
                if (string.Equals(cultureId, heroCulture, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                _logger.LogDebug($"CareerSystem: IsEligible — hero culture '{heroCulture}' not in [{string.Join(", ", career.EligibleCultureIds)}] for career '{careerStringId}'");
                return false;
            }
        }

        _logger.LogDebug($"CareerSystem: IsEligible — hero culture='{hero.CultureStringId}' IS eligible for career '{careerStringId}'");
        return true;
    }

    public int GetMaxChoicesForHero(int heroLevel)
    {
        EnsureLoaded();
        // Budget: 1 root (auto-added at CC) + N free points for level N
        // Hero starts at level 1 with 1 free point, gains 1 per level.
        var effectiveLevel = Math.Max(1, heroLevel);
        return Math.Min(effectiveLevel + 1, _maxPerkPoints + 1);
    }

    public int GetUnspentPoints(int heroLevel, int takenChoiceCount)
        => Math.Max(0, GetMaxChoicesForHero(heroLevel) - takenChoiceCount);

    public bool IsTierAvailable(int heroLevel, int tier)
    {
        if (tier < 1 || tier > 3) return false;
        return heroLevel >= GetTierUnlockLevel(tier);
    }

    public int GetTierUnlockLevel(int tier)
    {
        switch (tier)
        {
            case 1: return 1;
            case 2: return 10;
            case 3: return 20;
            default: return int.MaxValue;
        }
    }

    public IReadOnlyList<CareerDefinition> GetEligibleSwitchTargets(string currentCareerId, ICareerHeroAdapter hero)
    {
        EnsureLoaded();
        if (hero == null) return new List<CareerDefinition>();

        var targets = new List<CareerDefinition>();
        foreach (var career in _allCareers)
        {
            if (string.Equals(career.Id, currentCareerId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsEligible(career.Id, hero)) continue;
            targets.Add(career);
        }
        return targets;
    }

    private void EnsureLoaded()
    {
        if (_careers != null) return;

        _careers = new Dictionary<string, CareerDefinition>();
        _choices = new Dictionary<string, CareerChoiceDefinition>();
        _groups = new Dictionary<string, CareerChoiceGroupDefinition>();
        _careerIdByGroupId = new Dictionary<string, string>();
        _careerIdByRootChoiceId = new Dictionary<string, string>();

        _maxPerkPoints = _configProvider.GetMaxPerkPoints();

        var careers = _configProvider.LoadCareers();
        _allCareers = new List<CareerDefinition>(careers);
        foreach (var career in careers)
        {
            _careers[career.Id] = career;
            // Root choices carry group_id="" in the data, so they are unreachable through the
            // group index and need their own. Without this a ghost root from another career
            // resolves to no owner and survives the repair, which is the entire bug.
            if (!string.IsNullOrEmpty(career.RootChoiceId) && !_careerIdByRootChoiceId.ContainsKey(career.RootChoiceId))
                _careerIdByRootChoiceId[career.RootChoiceId] = career.Id;

            // Reverse index for GetOwningCareerId. First writer wins: a group listed by two
            // careers is a data error, and silently reassigning ownership to the later career
            // would make a legitimately-held choice look foreign to the earlier one.
            foreach (var groupId in career.ChoiceGroupIds)
            {
                if (!_careerIdByGroupId.ContainsKey(groupId))
                    _careerIdByGroupId[groupId] = career.Id;
            }
        }

        foreach (var group in _configProvider.LoadChoiceGroups())
            _groups[group.Id] = group;

        foreach (var choice in _configProvider.LoadChoices())
            _choices[choice.Id] = choice;

        _logger.LogInfo($"CareerSystem: Registry initialized: {_careers.Count} careers, {_groups.Count} groups, {_choices.Count} choices, maxPerkPoints={_maxPerkPoints}");
    }
}
