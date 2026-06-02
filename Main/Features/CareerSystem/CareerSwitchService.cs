using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem;

public class CareerSwitchService : ICareerSwitchService
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly IModLogger _logger;

    public CareerSwitchService(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerPassiveService passiveService,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _passiveService = passiveService;
        _logger = logger;
    }

    public bool CanSwitch(ICareerHeroAdapter hero, string newCareerStringId)
    {
        if (hero == null || string.IsNullOrEmpty(newCareerStringId))
        {
            _logger.LogDebug($"CareerSystem: CanSwitch — null hero or empty careerId '{newCareerStringId}'");
            return false;
        }

        // Reject switching to the hero's current career. The picker filters this out via
        // GetEligibleSwitchTargets, but this is the boundary defense -- a future caller invoking
        // SwitchCareer directly must not silently "succeed" into a no-op cache refresh.
        var heroId = hero.StringId;
        if (!string.IsNullOrEmpty(heroId))
        {
            var currentCareerId = _dataService.GetCareerStringId(heroId);
            if (!string.IsNullOrEmpty(currentCareerId) &&
                string.Equals(currentCareerId, newCareerStringId, System.StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug($"CareerSystem: CanSwitch — rejecting same-career switch '{newCareerStringId}' for hero '{heroId}'");
                return false;
            }
        }

        var eligible = _registry.IsEligible(newCareerStringId, hero);
        _logger.LogDebug($"CareerSystem: CanSwitch — hero culture='{hero.CultureStringId}' career='{newCareerStringId}' result={eligible}");
        return eligible;
    }

    public bool SwitchCareer(string heroStringId, ICareerHeroAdapter hero, string newCareerStringId)
    {
        if (!CanSwitch(hero, newCareerStringId))
        {
            _logger.LogWarning($"CareerSwitch: hero '{heroStringId}' not eligible for '{newCareerStringId}'");
            return false;
        }

        var oldCareer = _dataService.GetCareerStringId(heroStringId);

        _logger.LogInfo($"CareerSystem: Clearing old career '{oldCareer}' for hero '{heroStringId}'");
        _dataService.ClearCareer(heroStringId);
        _dataService.SetCareer(heroStringId, newCareerStringId);

        var career = _registry.GetCareer(newCareerStringId);
        if (career != null && !string.IsNullOrEmpty(career.RootChoiceId))
        {
            var maxChoices = _registry.GetMaxChoicesForHero(hero.Level);
            _dataService.TryAddChoice(heroStringId, career.RootChoiceId, maxChoices);
        }

        _passiveService.RefreshCache(_dataService, _registry);
        _logger.LogInfo($"CareerSwitch: hero '{heroStringId}' switched from '{oldCareer}' to '{newCareerStringId}'");
        return true;
    }
}
