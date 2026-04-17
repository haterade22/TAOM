using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.Mutations;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.CareerSystem;

public class CareerPerkMissionBehavior : MissionBehavior
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerAbilityService _abilityService;
    private readonly ICareerConfigProvider _configProvider;
    private readonly CareerAbilityEffectRegistry _effectRegistry;
    private readonly IMutationService _mutationService;
    private readonly ICareerHeroAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;

    private float _tickAccumulator;
    private const float TickInterval = 1f;
    private bool _loggedMissionStart;
    private bool _abilityReadyNotified;

    private GauntletLayer _hudLayer;
    private CareerAbilityHudVM _hudVM;
    private GauntletMovieIdentifier _hudMovie;
    private bool _hudInitialized;

    private readonly List<MissionAbilityExecutionContext> _activeContexts = new List<MissionAbilityExecutionContext>();

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public CareerPerkMissionBehavior(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerAbilityService abilityService,
        ICareerConfigProvider configProvider,
        CareerAbilityEffectRegistry effectRegistry,
        IMutationService mutationService,
        ICareerHeroAdapterFactory adapterFactory,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _abilityService = abilityService;
        _configProvider = configProvider;
        _effectRegistry = effectRegistry;
        _mutationService = mutationService;
        _adapterFactory = adapterFactory;
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

        // Tick all active execution contexts to expire timed buffs; remove finished ones.
        var currentTime = Mission.Current?.CurrentTime ?? 0f;
        for (int i = _activeContexts.Count - 1; i >= 0; i--)
        {
            _activeContexts[i].Tick(currentTime);
            if (_activeContexts[i].IsExpired)
                _activeContexts.RemoveAt(i);
        }

        // Check ability activation input (every frame, once per key press)
        if (Input.IsKeyPressed(InputKey.V))
        {
            if (_abilityService.IsAbilityReady(heroId))
            {
                _abilityService.ActivateAbility(heroId);
                _abilityReadyNotified = false;
                _logger.LogInfo($"CareerSystem: Ability activated for hero '{heroId}' via V key");
                ExecuteAbilityEffect(heroId);
            }
        }
    }

    private void ExecuteAbilityEffect(string heroId)
    {
        var careerId = _dataService.GetCareerStringId(heroId);
        if (string.IsNullOrEmpty(careerId)) return;

        var career = _registry.GetCareer(careerId);
        if (career == null) return;

        // Apply hero mutations to the raw template so Duration/Radius/etc reflect choice-tree choices.
        var rawTemplate = _configProvider.GetAbilityTemplate(career.AbilityTemplateId);
        var template = MutateTemplate(rawTemplate, heroId);

        // Reconcile max charge: mutations may have changed it since ability was first created.
        if (template != null && template.MaxCharge > 0f)
        {
            var ability = _abilityService.GetOrCreateAbility(heroId, _registry, _dataService);
            if (ability != null && ability.MaxCharge != template.MaxCharge)
            {
                ability.SetMaxCharge(template.MaxCharge);
                _logger.LogDebug($"CareerSystem: MaxCharge updated to {template.MaxCharge} for hero '{heroId}'");
            }
        }

        var duration = template?.Duration ?? 8f;
        var radius = template?.Radius ?? 10f;

        var mainAgent = Mission.Current?.MainAgent;
        var context = new MissionAbilityExecutionContext(
            heroId, duration, radius, mainAgent, Mission.Current, _logger);

        _activeContexts.Add(context);

        var executor = _effectRegistry.GetExecutor(careerId);
        executor.Execute(context);

        var abilityName = new TaleWorlds.Localization.TextObject(career.DisplayName).ToString();
        InformationManager.DisplayMessage(new InformationMessage(
            $"{abilityName} activated!", Colors.Yellow));

        if (!string.IsNullOrEmpty(template?.SoundEffect))
            context.PlaySound(template.SoundEffect);
        if (!string.IsNullOrEmpty(template?.ParticleEffect))
            context.PlayParticle(template.ParticleEffect);
    }

    private AbilityTemplateData MutateTemplate(AbilityTemplateData rawTemplate, string heroId)
    {
        if (rawTemplate == null) return null;

        var hero = Hero.MainHero;
        if (hero == null || hero.StringId != heroId) return rawTemplate;

        var heroAdapter = _adapterFactory.Create(hero);
        return _mutationService.MutateAbility(rawTemplate, heroAdapter, _dataService, _registry);
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
            _hudVM.Update(false, null, null, 0f, 0f, false);
            return;
        }

        var ability = _abilityService.GetOrCreateAbility(heroId, _registry, _dataService);
        if (ability == null)
        {
            _hudVM.Update(false, null, null, 0f, 0f, false);
            return;
        }

        var careerId = _dataService.GetCareerStringId(heroId);
        var career = careerId != null ? _registry.GetCareer(careerId) : null;
        var rawName = career?.DisplayName ?? ability.TemplateId;
        var abilityName = new TaleWorlds.Localization.TextObject(rawName).ToString();
        var abilitySprite = career != null
            ? $"CareerSystem\\Abilities\\{career.AbilityTemplateId}"
            : null;

        _hudVM.Update(true, abilityName, abilitySprite, ability.CurrentCharge, ability.MaxCharge, ability.IsReady);
    }

    public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
    {
        if (isBlocked || damagedHp <= 0f) return;

        var hero = Hero.MainHero;
        if (hero == null) return;

        var mainAgent = Mission.Current?.MainAgent;
        if (mainAgent == null) return;

        var heroId = hero.StringId;
        if (affectorAgent == mainAgent)
            _abilityService.AddCharge(heroId, damagedHp, ChargeType.DamageDone);
        else if (affectedAgent == mainAgent)
            _abilityService.AddCharge(heroId, damagedHp, ChargeType.DamageTaken);
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;

        var hero = Hero.MainHero;
        if (hero == null) return;

        var mainAgent = Mission.Current?.MainAgent;

        // Kill charge: main agent killed someone
        if (affectorAgent != null && mainAgent != null && affectorAgent == mainAgent)
            _abilityService.AddCharge(hero.StringId, 1f, ChargeType.Kills);

        // Death cleanup: main agent died while buffs were active
        if (affectedAgent == mainAgent)
        {
            CareerAbilityBuffTracker.ClearBuff(hero.StringId);
            CareerAbilityBuffTracker.ClearAllAllyBuffs();
            _activeContexts.Clear();
        }
    }

    protected override void OnEndMission()
    {
        CleanupHud();
        _logger.LogInfo("CareerSystem: Mission ended — clearing abilities");
        _loggedMissionStart = false;
        _abilityReadyNotified = false;
        _activeContexts.Clear();
        CareerAbilityBuffTracker.ClearAll();
        _abilityService.ClearAll();
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        // Clean up ally buff entry when any agent is removed from the mission
        CareerAbilityBuffTracker.ClearAllyBuff(affectedAgent.Index);
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
