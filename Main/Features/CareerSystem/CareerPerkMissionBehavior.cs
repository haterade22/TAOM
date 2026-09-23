using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.CareerSystem;

// Thin mission-scoped boundary that wires sealed TaleWorlds APIs (Mission, InformationManager,
// CharacterObject) to the testable controllers. Per ADR-002 / issue #102 the state machines
// live in IAbilityActivationController + IAbilityEffectExecutor; this class only owns the
// mission-scoped _activeContexts expiration list + the OnEndMission teardown sequencing.
// (The old AbilityHUD square panel + its controller were retired by Issue #382 — the energy
// bar injected into AgentStatus via MissionAgentStatusCareerMixin/CareerEnergyBarPrefab is
// the in-battle career UI now.)
public class CareerPerkMissionBehavior : MissionBehavior
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerAbilityService _abilityService;
    private readonly IAbilityActivationController _activationController;
    private readonly IAbilityEffectExecutor _effectExecutor;
    private readonly ICareerAgentStatService _agentStats;
    private readonly AbilityDamageAttributionReporter _attributionReporter;
    private readonly IModLogger _logger;

    private bool _loggedMissionStart;
    private readonly List<MissionAbilityExecutionContext> _activeContexts = new List<MissionAbilityExecutionContext>();

    // The player's death and score hits are native's to place, and a v1.4.8 player log caught
    // OnAgentRemoved off the main thread (#634): their work is replayed from OnMissionTick.
    private readonly DeferredCallbackQueue _deferred;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public CareerPerkMissionBehavior(
        ICareerDataService dataService,
        ICareerAbilityService abilityService,
        IAbilityActivationController activationController,
        IAbilityEffectExecutor effectExecutor,
        ICareerAgentStatService agentStats,
        ICareerConfigProvider config,
        IModLogger logger)
    {
        _dataService = dataService;
        _abilityService = abilityService;
        _activationController = activationController;
        _effectExecutor = effectExecutor;
        _agentStats = agentStats;
        _attributionReporter = new AbilityDamageAttributionReporter(config);
        _logger = logger;
        _deferred = new DeferredCallbackQueue(message => _logger?.LogWarning(message));
    }

    public override void OnMissionTick(float dt)
    {
        MissionThreadGuard.MarkMainThread();
        _deferred.Drain();

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

        // Issue #377 — the controlled agent must BE the career hero for the HUD to show and
        // V to activate. In co-op (and after being wounded in singleplayer) the player can be
        // controlling a soldier; the campaign PlayerCharacter lookup above cannot see that.
        // Computed here — the sanctioned Mission.Current boundary — and passed down as a bool
        // so the controllers stay free of TaleWorlds statics (#102 invariant).
        var isControllingCareerHero =
            CareerHeroIdentityGate.IsCareerHeroAgent(Mission.Current?.MainAgent, hero);

        var result = _activationController.Tick(dt, heroId, hasCareer, isControllingCareerHero);
        if (result.JustBecameReady)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                "Career ability is ready! Press V to activate.", Colors.Green));
        }
        if (result.Activated)
        {
            _logger?.LogInfo($"CareerSystem: Ability activated for hero '{heroId}' via V key");
            _attributionReporter.OnAbilityActivated();
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

    // Issue #383 — combat-log damage attribution. Boundary guards only; the reporting
    // (zero-bonus notice + attribution line) lives in AbilityDamageAttributionReporter.
    // Signature pinned against the installed v1.4.7 DLL 2026-08-05 (taom-src
    // MissionBehavior.cs:73) — the two `in` params silently no-op the override if wrong.
    public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon,
        bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData,
        float damagedHp, float hitDistance, float shotDifficulty)
    {
        // Codex review 2026-08-05 P2 — siege-engine missiles arrive with the operating
        // player as affector, but the ability buff is an agent stat that does not drive
        // siege damage; attributing "+N from ability" there would be a false claim.
        if (isSiegeEngineHit) return;
        // Same reason for TAOM's own blows (a creature's attack, owned by its rider since #643; a signature strike's
        // ring): they write their damage directly and never run the damage model the buff feeds. Not a null-weapon
        // test: a punch or kick is weaponless too, and the buff's DamageMultiplierBonus does reach it.
        if (CustomAttacksUtils.IsRegisteringSyntheticBlow) return;
        if (isBlocked || !(damagedHp > 0f)) return;
        if (affectorAgent == null || affectorAgent != Mission.Current?.MainAgent) return;
        if (Campaign.Current == null) return;

        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        if (hero == null || !CareerHeroIdentityGate.IsCareerHeroAgent(affectorAgent, hero)) return;

        var heroId = hero.StringId;
        var targetName = affectedAgent?.Name;
        _deferred.RunOrDefer("CareerPerkMissionBehavior.OnScoreHit",
            () => _attributionReporter.ReportHit(heroId, targetName, damagedHp));
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
        if (Campaign.Current == null) return;

        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        if (hero == null) return;

        var mainAgent = Mission.Current?.MainAgent;
        if (affectedAgent == mainAgent)
            EndAbilitiesForFallenHero(hero.StringId);
    }

    // Empties the list OnMissionTick walks and recomputes other agents' stats, so off the main thread
    // it waits for the next mission tick.
    internal void EndAbilitiesForFallenHero(string heroId) =>
        _deferred.RunOrDefer("CareerPerkMissionBehavior.OnAgentRemoved", () => EndAbilitiesNow(heroId));

    private void EndAbilitiesNow(string heroId)
    {
        // Deep-review 2026-08-05 — snapshot the buffed allies BEFORE the clear, then
        // force a stat recompute on each. UpdateAgentProperties is event-triggered
        // (never per-tick), and clearing _activeContexts kills the scheduled restores
        // that would have refreshed them — without this loop the allies keep the
        // buffed speed/damage/draw-speed baked in for the rest of the mission.
        var buffedAllies = CareerAbilityBuffTracker.GetBuffedAllyIndices();

        CareerAbilityBuffTracker.ClearBuff(heroId);
        CareerAbilityBuffTracker.ClearAllAllyBuffs();
        _activeContexts.Clear();

        var mission = Mission.Current;
        if (mission != null)
        {
            foreach (var allyIndex in buffedAllies)
            {
                var ally = mission.FindAgentWithIndex(allyIndex);
                if (ally != null && ally.IsActive())
                {
                    ally.UpdateAgentProperties();
                    ally.MountAgent?.UpdateAgentProperties(); // #611: the mount carries the buff's mount fields
                }
            }
        }
    }

    protected override void OnEndMission()
    {
        // Deep-review #102 MED — singleton-controller-per-mission-behavior lifetime asymmetry.
        // Each cleanup op runs in its own try/catch so a throw from one does not abort the
        // others, which would leave singleton state stuck across mission boundaries.
        try { _activationController.Reset(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission _activationController.Reset() threw — {ex.Message}"); }

        try { _abilityService.ClearAll(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission _abilityService.ClearAll() threw — {ex.Message}"); }

        try { CareerAbilityBuffTracker.ClearAll(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission CareerAbilityBuffTracker.ClearAll() threw — {ex.Message}"); }

        // #613: the [CareerPerks] dedupe lives on the singleton stat service; without this the next
        // battle's spawn logs nothing when the values match the last one.
        try { _agentStats.ResetDiagnostics(); }
        catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission ResetDiagnostics() threw: {ex.Message}"); }

        _logger?.LogInfo("CareerSystem: Mission ended — clearing abilities");
        _loggedMissionStart = false;
        _deferred.Clear();
        _activeContexts.Clear();
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        CareerAbilityBuffTracker.ClearAllyBuff(affectedAgent.Index);
    }
}
