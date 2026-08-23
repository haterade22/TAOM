using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.CombatMechanics.Domain;
using TAOM.Features.Refuge;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CombatMechanics.Models;

// CombatMechanics feature. Derives from the CareerSystem model so career
// passives ride along via inheritance — only ONE AgentApplyDamageModel can be registered.
// Thin boundary per gamemodels.md rule 4: each override is primitive extraction + one service
// call + fall-through to base; all decisions live in the four pure services. Short-circuit
// guards follow the parent's accepted idiom.
public class TaomCombatMechanicsModel : TaomAgentApplyDamageModel
{
    // Enum-name cache: WeaponClass.ToString() allocates per call and the missile-flag /
    // shield-damage paths run per missile spawn / per shield hit.
    private static readonly Dictionary<WeaponClass, string> WeaponClassNames = BuildWeaponClassNames();

    private readonly ICrushThroughService _crushThroughService;
    private readonly IChargeKnockdownService _chargeKnockdownService;
    private readonly ICreatureCombatService _creatureCombatService;
    private readonly IShieldPenetrationService _shieldPenetrationService;
    private readonly ICombatMechanicsConfigProvider _configProvider;
    private readonly ICombatMechanicsSettingsProvider _settingsProvider;
    private readonly IRefugeDefenseService _refugeDefense;

    public TaomCombatMechanicsModel(
        ICareerAgentStatService careerAgentStatService,
        ICrushThroughService crushThroughService,
        IChargeKnockdownService chargeKnockdownService,
        ICreatureCombatService creatureCombatService,
        IShieldPenetrationService shieldPenetrationService,
        ICombatMechanicsConfigProvider configProvider,
        ICombatMechanicsSettingsProvider settingsProvider,
        IRefugeDefenseService refugeDefense = null)
        : base(careerAgentStatService)
    {
        _crushThroughService = crushThroughService;
        _chargeKnockdownService = chargeKnockdownService;
        _creatureCombatService = creatureCombatService;
        _shieldPenetrationService = shieldPenetrationService;
        _configProvider = configProvider;
        _settingsProvider = settingsProvider;
        _refugeDefense = refugeDefense;
    }

    // Refuge (#507): defenders of a ready refuge take reduced real-time damage. base runs the
    // career-passive reduction chain first (the parent's override), then the refuge factor rides
    // on top: one service, shared with the auto-resolve path, so the two cannot drift. The source
    // module reached this line with a Harmony postfix on the SAME method TAOM's chain overrides,
    // an accidental and untested ordering; the override makes the order explicit.
    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var result = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        var reduction = _refugeDefense?.DefenderDamageReduction(VictimPartyId(attackInformation.VictimAgentOrigin)) ?? 0f;
        // Shared composition contract (RefugeDamageReduction): (1 - r) on the final damage; the
        // auto-resolve site applies the identical contract. NaN/out-of-range applies nothing.
        result = RefugeDamageReduction.Apply(result, reduction);

        return result;
    }

    private static string VictimPartyId(TaleWorlds.Core.IAgentOriginBase origin)
    {
        var party = origin switch
        {
            PartyAgentOrigin p => p.Party,
            PartyGroupAgentOrigin g => g.Party,
            _ => null,
        };
        return party?.MobileParty?.StringId;
    }

    public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
    {
        var context = BuildCrushThroughContext(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit);
        return _crushThroughService.DecideCrushThrough(in context)
            ?? base.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit);
    }

    public override float CalculateRemainingMomentum(float originalMomentum, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
    {
        return _creatureCombatService.CalculateCleaveMomentum(attacker?.Monster?.StringId, originalMomentum, collisionData.IsColliderAgent)
            ?? base.CalculateRemainingMomentum(originalMomentum, in b, in collisionData, attacker, victim, in attackerWeapon, isCrushThrough);
    }

    public override void DecideWeaponCollisionReaction(in Blow registeredBlow, in AttackCollisionData collisionData, Agent attacker, Agent defender, in MissionWeapon attackerWeapon, bool isFatalHit, bool isShruggedOff, float momentumRemaining, out MeleeCollisionReaction colReaction)
    {
        base.DecideWeaponCollisionReaction(in registeredBlow, in collisionData, attacker, defender, in attackerWeapon, isFatalHit, isShruggedOff, momentumRemaining, out colReaction);
        colReaction = _creatureCombatService.ShouldForceSliceThrough(attacker?.Monster?.StringId, momentumRemaining, collisionData.IsColliderAgent, collisionData.InflictedDamage)
            ? MeleeCollisionReaction.SlicedThrough
            : colReaction;
    }

    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
    {
        // Base = vanilla stagger threshold (which re-enters our CalculateStaggerThresholdDamage
        // via the registered model) + career shrug-off passives.
        if (base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)) return true;
        return _creatureCombatService.IsUnstoppable(victimAgent?.Monster?.StringId, collisionData.InflictedDamage);
    }

    public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
    {
        var baseThreshold = base.CalculateStaggerThresholdDamage(defenderAgent, in blow);
        return _creatureCombatService.ApplyStaggerThresholdMultiplier(GetRaceId(defenderAgent), baseThreshold);
    }

    public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
    {
        // Guard keeps the boundary extraction (incl. the stat-model resistance read) off the
        // ordinary melee path — the service only owns horse-charge verdicts.
        if (!collisionData.IsHorseCharge)
            return base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

        var context = BuildChargeKnockdownContext(attackerAgent, victimAgent, in collisionData, in blow);
        return _chargeKnockdownService.DecideChargeKnockdown(in context)
            ?? base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);
    }

    public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
    {
        // Base first — preserves the vanilla Javelin + Impale perk grant.
        base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
        missileWeaponFlags = (WeaponFlags)_shieldPenetrationService.ApplyPenetrationFlags(
            missileWeapon.Item?.StringId,
            GetWeaponClassName(missileWeapon.CurrentUsageItem),
            (ulong)missileWeaponFlags);
    }

    public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
    {
        var baseResult = base.CalculateShieldDamage(in attackInformation, baseDamage);
        var weapon = attackInformation.AttackerWeapon;
        var usage = weapon.CurrentUsageItem;
        return _shieldPenetrationService.ApplyRuntimeFlagCorrection(
            weapon.Item?.StringId,
            GetWeaponClassName(usage),
            usage != null && (usage.WeaponFlags & WeaponFlags.CanPenetrateShield) != 0,
            baseResult);
    }

    // Single source for the charge penetration constant: feeds the engine's own Branch-B-style
    // fall-through AND our ChargeKnockdownContext, so both use the same number (default 0.4 =
    // vanilla SandBox). Gated so a tuned value doesn't survive the feature being toggled off —
    // "master off = exactly pre-feature behavior" (ChargeKnockdownEnabled folds the master).
    public override float GetHorseChargePenetration()
        => _settingsProvider.ChargeKnockdownEnabled
            ? _configProvider.GetConfig().ChargeKnockdown.HorseChargePenetration
            : base.GetHorseChargePenetration();

    // Primitive extractors — pure boundary conversion, no decisions (parent-model idiom).

    private CrushThroughContext BuildCrushThroughContext(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
    {
        var weapon = GetWieldedUsageItem(attackerAgent);
        return new CrushThroughContext(
            totalAttackEnergy,
            isSwing: strikeType == StrikeType.Swing,
            isOverhead: attackDirection == Agent.UsageDirection.AttackUp,
            isPassiveUsage: isPassiveUsageHit,
            hasMeleeWeapon: weapon != null && weapon.IsMeleeWeapon && !weapon.IsShield,
            defendItemIsShield: defendItem != null && defendItem.IsShield,
            attackerWeaponSkill: GetSkillValue(attackerAgent, weapon),
            defenderWeaponSkill: GetSkillValue(defenderAgent, defendItem),
            attackerRaceId: GetRaceId(attackerAgent),
            defenderRaceId: GetRaceId(defenderAgent),
            attackerMonsterId: attackerAgent?.Monster?.StringId,
            isAiControlled: attackerAgent != null && attackerAgent.IsAIControlled,
            randomRoll: MBRandom.RandomFloat);
    }

    private static ChargeKnockdownContext BuildChargeKnockdownContext(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
    {
        return new ChargeKnockdownContext(
            isHorseCharge: collisionData.IsHorseCharge,
            chargeVelocity: collisionData.ChargeVelocity,
            chargerWeight: attackerAgent?.Monster?.Weight ?? 1,
            riderWeight: attackerAgent?.RiderAgent?.Monster?.Weight ?? 0,
            victimWeight: victimAgent?.Monster?.Weight ?? 1,
            victimRaceId: GetRaceId(victimAgent),
            victimMaxHealth: victimAgent?.HealthLimit ?? 1f,
            inflictedDamage: collisionData.InflictedDamage,
            // Null victim is unreachable per engine contract (all three DecideAgentKnockedDownByBlow
            // call sites deref the victim first) — 1f = neutral resistance, guarded for consistency.
            victimKnockDownResistance: victimAgent != null
                ? MissionGameModels.Current.AgentStatCalculateModel.GetKnockDownResistance(victimAgent)
                : 1f,
            chargerSpeedLimitForCharge: attackerAgent?.Monster?.RelativeSpeedLimitForCharge ?? float.MaxValue,
            hasShrugOffFlag: (blow.BlowFlag & BlowFlags.ShrugOff) != 0,
            hasKnockBackFlag: (blow.BlowFlag & BlowFlags.KnockBack) != 0);
    }

    // Mirrors the engine's own wield resolution in DecideCrushedThrough (offhand, else primary —
    // SandboxAgentApplyDamageModel v1.4.6 :640-655).
    private static WeaponComponentData GetWieldedUsageItem(Agent agent)
    {
        if (agent == null) return null;
        var index = agent.GetOffhandWieldedItemIndex();
        if (index == EquipmentIndex.None) index = agent.GetPrimaryWieldedItemIndex();
        return index != EquipmentIndex.None ? agent.Equipment[index].CurrentUsageItem : null;
    }

    private static int GetSkillValue(Agent agent, WeaponComponentData item)
    {
        if (agent?.Character == null || item?.RelevantSkill == null) return 0;
        return agent.Character.GetSkillValue(item.RelevantSkill);
    }

    private static int? GetRaceId(Agent agent)
        => agent?.Character != null ? agent.Character.Race : (int?)null;

    private static string GetWeaponClassName(WeaponComponentData usage)
        => usage != null && WeaponClassNames.TryGetValue(usage.WeaponClass, out var name) ? name : null;

    private static Dictionary<WeaponClass, string> BuildWeaponClassNames()
    {
        var map = new Dictionary<WeaponClass, string>();
        foreach (WeaponClass weaponClass in Enum.GetValues(typeof(WeaponClass)))
            map[weaponClass] = weaponClass.ToString();
        return map;
    }
}
