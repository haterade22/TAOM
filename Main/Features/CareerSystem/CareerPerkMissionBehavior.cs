using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerPerkMissionBehavior : MissionBehavior
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerAbilityService _abilityService;
    private readonly IModLogger _logger;

    private float _tickAccumulator;
    private const float TickInterval = 1f;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public CareerPerkMissionBehavior(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerAbilityService abilityService,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _abilityService = abilityService;
        _logger = logger;
    }

    public override void OnMissionTick(float dt)
    {
        _tickAccumulator += dt;
        if (_tickAccumulator < TickInterval) return;
        _tickAccumulator -= TickInterval;

        var hero = Hero.MainHero;
        if (hero == null) return;

        var heroId = hero.StringId;
        if (!_dataService.HasCareer(heroId)) return;

        _abilityService.Tick(heroId, TickInterval);
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (affectorAgent == null) return;
        if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;

        var hero = Hero.MainHero;
        if (hero == null) return;

        var mainAgent = Mission.Current?.MainAgent;
        if (mainAgent == null || affectorAgent != mainAgent) return;

        _abilityService.AddCharge(hero.StringId, 1f, ChargeType.Kills);
    }

    protected override void OnEndMission()
    {
        _abilityService.ClearAll();
    }
}
