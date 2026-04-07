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
        return Math.Min(heroLevel + 1, _maxPerkPoints + 1);
    }

    public bool IsTierAvailable(int heroLevel, int tier)
    {
        switch (tier)
        {
            case 1: return true;
            case 2: return heroLevel >= 10;
            case 3: return heroLevel >= 20;
            default: return false;
        }
    }

    private void EnsureLoaded()
    {
        if (_careers != null) return;

        _careers = new Dictionary<string, CareerDefinition>();
        _choices = new Dictionary<string, CareerChoiceDefinition>();
        _groups = new Dictionary<string, CareerChoiceGroupDefinition>();

        _maxPerkPoints = _configProvider.GetMaxPerkPoints();

        var careers = _configProvider.LoadCareers();
        _allCareers = new List<CareerDefinition>(careers);
        foreach (var career in careers)
            _careers[career.Id] = career;

        foreach (var group in _configProvider.LoadChoiceGroups())
            _groups[group.Id] = group;

        foreach (var choice in _configProvider.LoadChoices())
            _choices[choice.Id] = choice;

        _logger.LogInfo($"CareerSystem: Registry initialized: {_careers.Count} careers, {_groups.Count} groups, {_choices.Count} choices, maxPerkPoints={_maxPerkPoints}");
    }
}
