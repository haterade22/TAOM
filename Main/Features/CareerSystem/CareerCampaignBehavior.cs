using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem;

public class CareerCampaignBehavior : CampaignBehaviorBase
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly IModLogger _logger;

    public CareerCampaignBehavior(
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

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, OnHeroLeveledUp);
        CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        // Auto-assign career for eligible heroes without one
        if (!_dataService.HasCareer(hero.StringId))
        {
            var cultureId = hero.Culture?.StringId;
            if (!string.IsNullOrEmpty(cultureId))
            {
                foreach (var career in _registry.GetAllCareers())
                {
                    var eligible = false;
                    foreach (var id in career.EligibleCultureIds)
                    {
                        if (string.Equals(id, cultureId, System.StringComparison.OrdinalIgnoreCase))
                        { eligible = true; break; }
                    }
                    if (eligible)
                    {
                        var handler = IoC.Resolve<ICareerCreationHandler>();
                        handler?.OnCareerSelected(hero.StringId, career.Id);
                        _logger.LogInfo($"CareerSystem: Auto-assigned career '{career.Id}' to {hero.Name} (culture: {cultureId})");
                        break;
                    }
                }
            }
        }

        _passiveService.RefreshCache(_dataService, _registry);

        var careerId = _dataService.GetCareerStringId(hero.StringId);
        if (careerId != null)
            _logger.LogInfo($"CareerSystem: {hero.Name} has career '{careerId}' with {_dataService.GetChoiceCount(hero.StringId)} choices");
        else
            _logger.LogInfo("CareerSystem: Main hero has no career assigned");
    }

    private void OnHeroLeveledUp(Hero hero, bool shouldNotify)
    {
        if (hero != Hero.MainHero) return;
        if (!_dataService.HasCareer(hero.StringId)) return;

        var maxChoices = _registry.GetMaxChoicesForHero(hero.Level);
        var currentChoices = _dataService.GetChoiceCount(hero.StringId);
        if (maxChoices > currentChoices)
            _logger.LogInfo($"CareerSystem: {hero.Name} leveled up — {maxChoices - currentChoices} career choice(s) available");
    }

    private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        if (victim == null) return;
        if (victim == Hero.MainHero) return;

        if (_dataService.HasCareer(victim.StringId))
        {
            _dataService.ClearCareer(victim.StringId);
            _passiveService.RefreshCache(_dataService, _registry);
        }
    }
}
