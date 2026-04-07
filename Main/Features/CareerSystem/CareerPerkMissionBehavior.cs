using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.CareerSystem;

public class CareerPerkMissionBehavior : MissionBehavior
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerAbilityService _abilityService;
    private readonly IModLogger _logger;

    private float _tickAccumulator;
    private const float TickInterval = 1f;
    private bool _loggedMissionStart;
    private bool _abilityReadyNotified;

    private GauntletLayer _hudLayer;
    private CareerAbilityHudVM _hudVM;
    private GauntletMovieIdentifier _hudMovie;
    private bool _hudInitialized;

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
        var hero = Hero.MainHero;
        if (hero == null) return;

        var heroId = hero.StringId;

        if (!_loggedMissionStart)
        {
            _loggedMissionStart = true;
            var hasCareer = _dataService.HasCareer(heroId);
            var careerId = _dataService.GetCareerStringId(heroId);
            _logger.LogInfo($"CareerSystem: Mission started — hero='{heroId}' hasCareer={hasCareer} career='{careerId ?? "none"}'");
        }

        TryInitializeHud();
        UpdateHud(heroId);

        if (!_dataService.HasCareer(heroId)) return;

        // Tick ability cooldowns/timers once per second
        _tickAccumulator += dt;
        if (_tickAccumulator >= TickInterval)
        {
            _tickAccumulator -= TickInterval;
            _abilityService.Tick(heroId, TickInterval);
        }

        // Check ability ready notification (every frame, not gated by tick interval)
        if (_abilityService.IsAbilityReady(heroId) && !_abilityReadyNotified)
        {
            _abilityReadyNotified = true;
            InformationManager.DisplayMessage(new InformationMessage(
                "Career ability is ready! Press V to activate.", Colors.Green));
        }

        // Check ability activation input (every frame, once per key press)
        if (Input.IsKeyPressed(InputKey.V))
        {
            if (_abilityService.IsAbilityReady(heroId))
            {
                _abilityService.ActivateAbility(heroId);
                _abilityReadyNotified = false;
                _logger.LogInfo($"CareerSystem: Ability activated for hero '{heroId}' via V key");
                InformationManager.DisplayMessage(new InformationMessage(
                    "Career ability activated!", Colors.Yellow));
            }
        }
    }

    private void TryInitializeHud()
    {
        if (_hudInitialized) return;

        var topScreen = ScreenManager.TopScreen;
        if (topScreen == null) return;

        _hudVM = new CareerAbilityHudVM();
        _hudLayer = new GauntletLayer("CareerAbilityHUD", 50);
        _hudMovie = _hudLayer.LoadMovie("AbilityHUD", _hudVM);
        topScreen.AddLayer(_hudLayer);
        _hudInitialized = true;
        _logger.LogInfo("CareerSystem: HUD layer initialized");
    }

    private void UpdateHud(string heroId)
    {
        if (_hudVM == null) return;

        if (!_dataService.HasCareer(heroId))
        {
            _hudVM.Update(false, null, 0f, 0f, false);
            return;
        }

        var ability = _abilityService.GetOrCreateAbility(heroId, _registry, _dataService);
        if (ability == null)
        {
            _hudVM.Update(false, null, 0f, 0f, false);
            return;
        }

        var careerId = _dataService.GetCareerStringId(heroId);
        var career = careerId != null ? _registry.GetCareer(careerId) : null;
        var abilityName = career?.DisplayName ?? ability.TemplateId;

        _hudVM.Update(true, abilityName, ability.CurrentCharge, ability.MaxCharge, ability.IsReady);
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
        _logger.LogDebug($"CareerSystem: Kill charge added for hero '{hero.StringId}'");
    }

    protected override void OnEndMission()
    {
        CleanupHud();
        _logger.LogInfo("CareerSystem: Mission ended — clearing abilities");
        _loggedMissionStart = false;
        _abilityReadyNotified = false;
        _abilityService.ClearAll();
    }

    private void CleanupHud()
    {
        if (!_hudInitialized) return;

        var topScreen = ScreenManager.TopScreen;
        if (topScreen != null && _hudLayer != null)
        {
            topScreen.RemoveLayer(_hudLayer);
        }

        if (_hudMovie != null && _hudLayer != null)
            _hudLayer.ReleaseMovie(_hudMovie);

        _hudVM?.OnFinalize();
        _hudLayer = null;
        _hudVM = null;
        _hudMovie = null;
        _hudInitialized = false;
    }
}
