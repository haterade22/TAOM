using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.CareerSystem;

// Hooks the "I wish to discuss my career path." player line under hero_main_options. The
// consequence opens GauntletCareerScreen in switch mode (the screen renders a picker of
// eligible alternatives); previously this auto-switched to whatever the registry returned
// first in iteration order, giving the player no choice.
public class CareerSwitchDialogueBehavior : CampaignBehaviorBase
{
    // Note: ICareerSwitchService is NOT injected here -- the switch action is invoked from
    // GauntletCareerScreen.OnChooseSwitchTarget instead. This behavior only registers the
    // dialogue lines + visibility gate. SubModule.cs must call the 4-arg ctor.
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerHeroAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;

    public CareerSwitchDialogueBehavior(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerHeroAdapterFactory adapterFactory,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
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
            "{=taom_career_switched_open_screen}Very well -- let us discuss what paths lie before you.",
            null,
            OpenCareerSwitchScreen,
            200);
    }

    // Dialogue option only appears if the player has a career AND at least one eligible
    // alternative -- a culture with a single career path produces no targets and we don't
    // want a dead-end click.
    private bool CareerSwitchCondition()
    {
        var hero = Hero.MainHero;
        if (hero == null) return false;
        if (!_dataService.HasCareer(hero.StringId)) return false;

        var adapter = _adapterFactory.Create(hero);
        if (adapter == null) return false;

        var currentCareerId = _dataService.GetCareerStringId(hero.StringId);
        var targets = _registry.GetEligibleSwitchTargets(currentCareerId, adapter);
        return targets.Count > 0;
    }

    private void OpenCareerSwitchScreen()
    {
        _logger?.LogInfo("CareerSystem: Opening career-switch screen from dialogue");
        GauntletCareerScreen.OpenCareerScreen(switchMode: true);
    }
}
