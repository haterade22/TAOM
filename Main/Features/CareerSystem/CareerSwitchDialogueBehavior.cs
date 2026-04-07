using System.Linq;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem;

public class CareerSwitchDialogueBehavior : CampaignBehaviorBase
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerSwitchService _switchService;
    private readonly ICareerHeroAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;

    private string _pendingNewCareerId;

    public CareerSwitchDialogueBehavior(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerSwitchService switchService,
        ICareerHeroAdapterFactory adapterFactory,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _switchService = switchService;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter campaignStarter)
    {
        campaignStarter.AddPlayerLine(
            "career_switch_start",
            "hero_main_options",
            "career_switch_response",
            "{=taom_career_switch}I wish to discuss my career path.",
            CareerSwitchCondition,
            null,
            200);

        campaignStarter.AddDialogLine(
            "career_switch_response",
            "career_switch_response",
            "close_window",
            "{=taom_career_switched}Your path has been altered. May it serve you well.",
            null,
            CareerSwitchConsequence,
            200);
    }

    private bool CareerSwitchCondition()
    {
        var hero = Hero.MainHero;
        if (hero == null) return false;

        if (!_dataService.HasCareer(hero.StringId)) return false;

        var adapter = _adapterFactory.Create(hero);
        if (adapter == null) return false;

        var currentCareerId = _dataService.GetCareerStringId(hero.StringId);
        var allCareers = _registry.GetAllCareers();

        foreach (var career in allCareers)
        {
            if (career.Id == currentCareerId) continue;
            if (_switchService.CanSwitch(adapter, career.Id))
            {
                _pendingNewCareerId = career.Id;
                return true;
            }
        }

        return false;
    }

    private void CareerSwitchConsequence()
    {
        var hero = Hero.MainHero;
        if (hero == null || string.IsNullOrEmpty(_pendingNewCareerId)) return;

        var adapter = _adapterFactory.Create(hero);
        if (adapter == null) return;

        var oldCareerId = _dataService.GetCareerStringId(hero.StringId);
        var success = _switchService.SwitchCareer(hero.StringId, adapter, _pendingNewCareerId);

        if (success)
            _logger.LogInfo($"CareerSystem: Dialogue career switch — '{oldCareerId}' -> '{_pendingNewCareerId}' for {hero.Name}");
        else
            _logger.LogWarning($"CareerSystem: Dialogue career switch failed — '{_pendingNewCareerId}' for {hero.Name}");

        _pendingNewCareerId = null;
    }
}
