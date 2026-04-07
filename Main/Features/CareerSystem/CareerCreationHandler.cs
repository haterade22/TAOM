using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem;

public class CareerCreationHandler : ICareerCreationHandler
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly IModLogger _logger;

    public CareerCreationHandler(
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

    public void OnCareerSelected(string heroStringId, string careerStringId)
    {
        if (string.IsNullOrEmpty(heroStringId) || string.IsNullOrEmpty(careerStringId))
        {
            _logger.LogWarning("CareerCreation: null heroId or careerId");
            return;
        }

        var career = _registry.GetCareer(careerStringId);
        if (career == null)
        {
            _logger.LogError($"CareerCreation: career '{careerStringId}' not found in registry");
            return;
        }

        _dataService.SetCareer(heroStringId, careerStringId);

        // Auto-select root choice
        if (!string.IsNullOrEmpty(career.RootChoiceId))
        {
            var maxChoices = _registry.GetMaxChoicesForHero(1); // Level 1 at creation
            _dataService.TryAddChoice(heroStringId, career.RootChoiceId, maxChoices);
        }

        _passiveService.RefreshCache(_dataService, _registry);
        _logger.LogInfo($"CareerCreation: assigned career '{careerStringId}' to hero '{heroStringId}' with root choice '{career.RootChoiceId}'");
    }
}
