using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.Mutations;

namespace TAOM.Features.CareerSystem.Abilities;

// Per-activation effect pipeline extracted from CareerPerkMissionBehavior.ExecuteAbilityEffect
// (Issue #102). Boundary class -- allocates MissionAbilityExecutionContext (touches
// Mission.Current.MainAgent) and emits the InformationManager "activated" toast. The host
// MissionBehavior owns the _activeContexts expiration list and passes registerContext so the
// allocated context joins it.
public class AbilityEffectExecutor : IAbilityEffectExecutor
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerConfigProvider _configProvider;
    private readonly CareerAbilityEffectRegistry _effectRegistry;
    private readonly IMutationService _mutationService;
    private readonly ICareerHeroAdapterFactory _adapterFactory;
    private readonly Abilities.ICareerAbilityService _abilityService;
    private readonly IModLogger _logger;

    public AbilityEffectExecutor(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerConfigProvider configProvider,
        CareerAbilityEffectRegistry effectRegistry,
        IMutationService mutationService,
        ICareerHeroAdapterFactory adapterFactory,
        Abilities.ICareerAbilityService abilityService,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _configProvider = configProvider;
        _effectRegistry = effectRegistry;
        _mutationService = mutationService;
        _adapterFactory = adapterFactory;
        _abilityService = abilityService;
        _logger = logger;
    }

    public void Execute(string heroStringId, Action<MissionAbilityExecutionContext> registerContext)
    {
        var careerId = _dataService.GetCareerStringId(heroStringId);
        if (string.IsNullOrEmpty(careerId)) return;

        var career = _registry.GetCareer(careerId);
        if (career == null) return;

        var rawTemplate = _configProvider.GetAbilityTemplate(career.AbilityTemplateId);
        var template = MutateTemplate(rawTemplate, heroStringId);

        var duration = template?.Duration ?? 8f;
        var radius = template?.Radius ?? 10f;

        var mainAgent = Mission.Current?.MainAgent;
        var context = new MissionAbilityExecutionContext(
            heroStringId, duration, radius, mainAgent, Mission.Current, _logger);

        // Register the context BEFORE executor.Execute so any partial restores scheduled inside
        // ApplyAoeBuff iterations are still tickable by the host even if a later iteration throws.
        // Deep-review #102 considered swapping this; verdict was REFUTED — the swap leaks queued
        // restores from completed iterations.
        registerContext?.Invoke(context);

        var executor = _effectRegistry.GetExecutor(careerId);
        executor.Execute(context);

        // Issue #104 Option B — apply per-activation CooldownReduction (from designer choice
        // mutations) AFTER executor.Execute. Floored at GlobalTuning.MinCooldownSeconds. Order
        // matters: if executor.Execute throws above, we never shorten the cooldown for an
        // activation whose effect didn't fully apply (Codex pass-2 LOW). The HUD pip on the
        // next tick still sees the adjusted cooldown — the toast/sound/particle below run
        // synchronously in the same frame.
        if (template != null && template.CooldownReduction > 0f)
        {
            var min = _configProvider.GetAbilityTuning().Global.MinCooldownSeconds;
            _abilityService.ApplyCooldownAdjustment(heroStringId, template.CooldownReduction, min);
        }

        var abilityName = new TextObject(career.DisplayName).ToString();
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
        if (Campaign.Current == null) return rawTemplate;

        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        if (hero == null || hero.StringId != heroId) return rawTemplate;

        var heroAdapter = _adapterFactory.Create(hero);
        return _mutationService.MutateAbility(rawTemplate, heroAdapter, _dataService, _registry);
    }
}
