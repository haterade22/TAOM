using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.CareerSystem;

// Thin mission-scoped boundary that wires sealed TaleWorlds APIs (Mission, InformationManager,
// CharacterObject) to the testable controllers. Per ADR-002 / issue #102 the state machines
// live in IAbilityActivationController + IAbilityHudController + IAbilityEffectExecutor;
// this class only owns the mission-scoped _activeContexts expiration list + the OnEndMission
// teardown sequencing.
public class CareerPerkMissionBehavior : MissionBehavior
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerAbilityService _abilityService;
    private readonly IAbilityActivationController _activationController;
    private readonly IAbilityHudController _hudController;
    private readonly IAbilityEffectExecutor _effectExecutor;
    private readonly ICareerPassiveService _passives;
    private readonly IModLogger _logger;

    private bool _loggedMissionStart;
    private readonly List<MissionAbilityExecutionContext> _activeContexts = new List<MissionAbilityExecutionContext>();

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public CareerPerkMissionBehavior(
        ICareerDataService dataService,
        ICareerAbilityService abilityService,
        IAbilityActivationController activationController,
        IAbilityHudController hudController,
        IAbilityEffectExecutor effectExecutor,
        ICareerPassiveService passives,
        IModLogger logger)
    {
        _dataService = dataService;
        _abilityService = abilityService;
        _activationController = activationController;
        _hudController = hudController;
        _effectExecutor = effectExecutor;
        _passives = passives;
        _logger = logger;
    }

    public override void OnMissionTick(float dt)
    {
        if (Campaign.Current == null) return;
        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        if (hero == null) return;

        var heroId = hero.StringId;
        var hasCareer = _dataService.HasCareer(heroId);

        if (!_loggedMissionStart)
        {
            _loggedMissionStart = true;
            var careerId = _dataService.GetCareerStringId(heroId);
            _logger?.LogInfo($"CareerSystem: Mission started — hero='{heroId}' hasCareer={hasCareer} career='{careerId ?? "none"}'");
        }

        _hudController.TryInitialize();
        _hudController.Refresh(heroId);

        var result = _activationController.Tick(dt, heroId, hasCareer);
        if (result.JustBecameReady)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                "Career ability is ready! Press V to activate.", Colors.Green));
        }
        if (result.Activated)
        {
            _logger?.LogInfo($"CareerSystem: Ability activated for hero '{heroId}' via V key");
            _effectExecutor.Execute(heroId, _activeContexts.Add);
        }
        else if (result.Charging)
        {
            var remaining = (int)Math.Ceiling(_abilityService.GetCooldownRemaining(heroId));
            if (remaining < 1) remaining = 1;
            InformationManager.DisplayMessage(new InformationMessage(
                $"Career ability still charging — {remaining}s remaining.", Colors.Gray));
        }

        var currentTime = Mission.Current?.CurrentTime ?? 0f;
        for (var i = _activeContexts.Count - 1; i >= 0; i--)
        {
            _activeContexts[i].Tick(currentTime);
            if (_activeContexts[i].IsExpired)
                _activeContexts.RemoveAt(i);
        }
    }

    // Ammo career passive — a hero with the passive spawns with multiplicatively more ammo in
    // every ranged slot. OnAgentBuild fires once per agent after equipment is built; the
    // non-hero early-out keeps the per-spawn cost negligible for the 99% of agents.
    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (agent == null || !agent.IsHero) return;
        var heroId = (agent.Character as CharacterObject)?.HeroObject?.StringId;
        if (string.IsNullOrEmpty(heroId)) return;

        var bonus = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.Ammo);
        if (bonus <= 0f) return;

        for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
        {
            var weapon = agent.Equipment[slot];
            if (weapon.IsEmpty || !weapon.IsAnyAmmo()) continue;

            int boosted = CareerPassiveMath.BoostAmmo(weapon.ModifiedMaxAmount, weapon.Amount, bonus);
            if (boosted > weapon.Amount)
                agent.SetWeaponAmountInSlot(slot, (short)boosted, enforcePrimaryItem: false);
        }
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
        if (Campaign.Current == null) return;

        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        if (hero == null) return;

        var mainAgent = Mission.Current?.MainAgent;
        if (affectedAgent == mainAgent)
        {
            CareerAbilityBuffTracker.ClearBuff(hero.StringId);
            CareerAbilityBuffTracker.ClearAllAllyBuffs();
            _activeContexts.Clear();
        }
    }

    protected override void OnEndMission()
    {
        // Deep-review #102 MED — singleton-controller-per-mission-behavior lifetime asymmetry.
        // Each cleanup op runs in its own try/catch so a throw from one (the HUD layer's
        // RemoveLayer is the most plausible per the screen-mismatch RCA) does not abort the
        // others, which would leave singleton state stuck across mission boundaries.
        try { _hudController.Cleanup(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission _hudController.Cleanup() threw — {ex.Message}"); }

        try { _activationController.Reset(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission _activationController.Reset() threw — {ex.Message}"); }

        try { _abilityService.ClearAll(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission _abilityService.ClearAll() threw — {ex.Message}"); }

        try { CareerAbilityBuffTracker.ClearAll(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission CareerAbilityBuffTracker.ClearAll() threw — {ex.Message}"); }

        _logger?.LogInfo("CareerSystem: Mission ended — clearing abilities");
        _loggedMissionStart = false;
        _activeContexts.Clear();
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        CareerAbilityBuffTracker.ClearAllyBuff(affectedAgent.Index);
    }
}
