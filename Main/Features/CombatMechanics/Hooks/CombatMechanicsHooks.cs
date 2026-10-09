using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics.Hooks;

/// <summary>
/// What the damage models (<c>TaomCombatMechanicsModel</c> in the campaign, <c>TaomCustomBattleDamageModel</c> in Custom Battle) ask of the four combat services, one call per seam so
/// each override there stays base plus a delegate (gamemodels.md rule 4; ADR-002's 150 lines, #737). Each method turns the
/// engine's arguments into the primitives or context its service takes and returns the service's answer; the model keeps
/// the order the features run in. Two gates live here rather than in a service: <see cref="ChargeKnockdown"/> declines any
/// hit that is not a horse charge before it builds a context, and <see cref="HorseChargePenetration"/> applies the charge
/// toggle to the engine's own fall-through. The services keep every other decision, the NaN gates included. Like the model
/// it came from, it holds only the service references and a read-only enum-name cache.
/// </summary>
public sealed class CombatMechanicsHooks
{
    // Enum-name cache: WeaponClass.ToString() allocates per call and the missile-flag /
    // shield-damage paths run per missile hit (Mission.MissileHitCallback) / per shield hit.
    private static readonly Dictionary<WeaponClass, string> WeaponClassNames = BuildWeaponClassNames();

    private readonly ICrushThroughService _crushThroughService;
    private readonly IChargeKnockdownService _chargeKnockdownService;
    private readonly ICreatureCombatService _creatureCombatService;
    private readonly IShieldPenetrationService _shieldPenetrationService;
    private readonly ICombatMechanicsSettingsProvider _settingsProvider;

    public CombatMechanicsHooks(
        ICrushThroughService crushThroughService,
        IChargeKnockdownService chargeKnockdownService,
        ICreatureCombatService creatureCombatService,
        IShieldPenetrationService shieldPenetrationService,
        ICombatMechanicsSettingsProvider settingsProvider)
    {
        _crushThroughService = crushThroughService;
        _chargeKnockdownService = chargeKnockdownService;
        _creatureCombatService = creatureCombatService;
        _shieldPenetrationService = shieldPenetrationService;
        _settingsProvider = settingsProvider;
    }

    // The crush context (two skill lookups and a roll) is built only when the model asks, after the race abilities.
    public bool? CrushThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
        => _crushThroughService.DecideCrushThrough(BuildCrushThroughContext(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit));

    public float? CleaveMomentum(Agent attacker, float originalMomentum, in AttackCollisionData collisionData)
        => _creatureCombatService.CalculateCleaveMomentum(attacker?.Monster?.StringId, originalMomentum, collisionData.IsColliderAgent);

    public MeleeCollisionReaction CollisionReaction(Agent attacker, float momentumRemaining, in AttackCollisionData collisionData, MeleeCollisionReaction colReaction)
        => _creatureCombatService.ShouldForceSliceThrough(attacker?.Monster?.StringId, momentumRemaining, collisionData.IsColliderAgent, collisionData.InflictedDamage)
            ? MeleeCollisionReaction.SlicedThrough
            : colReaction;

    public bool IsUnstoppable(Agent victimAgent, in AttackCollisionData collisionData)
        => _creatureCombatService.IsUnstoppable(victimAgent?.Monster?.StringId, collisionData.InflictedDamage);

    public float StaggerThreshold(Agent defenderAgent, float baseThreshold)
        => _creatureCombatService.ApplyStaggerThresholdMultiplier(GetRaceId(defenderAgent), baseThreshold);

    // Guard keeps the boundary extraction (incl. the stat-model resistance read) off the
    // ordinary melee path: the charge service only owns horse-charge verdicts. IsHorseCharge is
    // ChargeVelocity > 0f, so a NaN velocity is no charge, deliberately: the model routed it so before #737.
    public bool? ChargeKnockdown(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
    {
        if (!collisionData.IsHorseCharge)
            return null;

        var context = BuildChargeKnockdownContext(attackerAgent, victimAgent, in collisionData, in blow);
        return _chargeKnockdownService.DecideChargeKnockdown(in context);
    }

    public WeaponFlags PenetrationFlags(in MissionWeapon missileWeapon, WeaponFlags missileWeaponFlags)
        => (WeaponFlags)_shieldPenetrationService.ApplyPenetrationFlags(
            missileWeapon.Item?.StringId,
            GetWeaponClassName(missileWeapon.CurrentUsageItem),
            (ulong)missileWeaponFlags);

    public float ShieldDamage(in AttackInformation attackInformation, float baseResult)
    {
        var weapon = attackInformation.AttackerWeapon;
        var usage = weapon.CurrentUsageItem;
        return _shieldPenetrationService.ApplyRuntimeFlagCorrection(
            weapon.Item?.StringId,
            GetWeaponClassName(usage),
            usage != null && (usage.WeaponFlags & WeaponFlags.CanPenetrateShield) != 0,
            baseResult);
    }

    // The charge penetration for the engine's own Branch-B-style fall-through: the same
    // ChargeHorsePenetration getter ChargeKnockdownService's Branch B reads, so both use one number
    // (default 0.4 = vanilla SandBox). Gated so a tuned value doesn't survive the feature being toggled
    // off: "master off = exactly pre-feature behavior" (ChargeKnockdownEnabled folds the master).
    // Null when off, so the model asks base for vanilla's own value.
    public float? HorseChargePenetration()
        => _settingsProvider.ChargeKnockdownEnabled
            ? _settingsProvider.ChargeHorsePenetration
            : null;

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
    // SandboxAgentApplyDamageModel v1.5.4 :773-787).
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
