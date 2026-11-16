using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Hooks;
using TAOM.Features.CombatMechanics.Domain;
using TAOM.Features.RaceAbilities.Hooks;

namespace TAOM.Features.CombatMechanics.Hooks;

/// <summary>
/// What the damage models (<c>TaomCombatMechanicsModel</c> in the campaign, <c>TaomCustomBattleDamageModel</c> in Custom Battle) ask of the six combat services, one call per seam so
/// each override there stays base plus a delegate (gamemodels.md rule 4; ADR-002's 150 lines, #737). Each method turns the
/// engine's arguments into the primitives or context its service takes and returns the service's answer; the model keeps
/// the order the features run in. Three gates live here rather than in a service: <see cref="ChargeKnockdown"/> and
/// <see cref="ShieldBashKnockdown"/> each decline a hit that is not their kind before they build a context, and
/// <see cref="HorseChargePenetration"/> applies the charge toggle to the engine's own fall-through. The services keep every
/// other decision, the NaN gates included. Like the model it came from, it holds only the service references and a read-only
/// enum-name cache.
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
    private readonly IShieldBashKnockdownService _shieldBashKnockdownService;
    private readonly ICareerAgentStatService _careerAgentStatService;
    private readonly ICombatMechanicsSettingsProvider _settingsProvider;
    private readonly ICleavingBlowService _cleavingBlowService;

    public CombatMechanicsHooks(
        ICrushThroughService crushThroughService,
        IChargeKnockdownService chargeKnockdownService,
        ICreatureCombatService creatureCombatService,
        IShieldPenetrationService shieldPenetrationService,
        IShieldBashKnockdownService shieldBashKnockdownService,
        ICareerAgentStatService careerAgentStatService,
        ICombatMechanicsSettingsProvider settingsProvider,
        ICleavingBlowService cleavingBlowService)
    {
        _crushThroughService = crushThroughService;
        _chargeKnockdownService = chargeKnockdownService;
        _creatureCombatService = creatureCombatService;
        _shieldPenetrationService = shieldPenetrationService;
        _shieldBashKnockdownService = shieldBashKnockdownService;
        _careerAgentStatService = careerAgentStatService;
        _settingsProvider = settingsProvider;
        _cleavingBlowService = cleavingBlowService;
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

    // Cleaving Blows. The crush call carries a default Blow and keeps vanilla's 0.3. A non-collider, an alternative attack and a
    // hit without damage never qualify, so they decline before the context's skill reads.
    public float? CleavingBlowMomentum(float originalMomentum, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
    {
        if (isCrushThrough || attacker == null || !collisionData.IsColliderAgent || collisionData.IsAlternativeAttack || b.InflictedDamage <= 0)
            return null;

        var context = BuildCleavingBlowContext(in b, in collisionData, attacker, victim, in attackerWeapon);
        return _cleavingBlowService.CalculateMomentum(in context, originalMomentum);
    }

    public MeleeCollisionReaction CleavingBlowReaction(in Blow registeredBlow, in AttackCollisionData collisionData, Agent attacker, Agent defender, in MissionWeapon attackerWeapon, float momentumRemaining, MeleeCollisionReaction colReaction)
    {
        if (attacker == null || !(momentumRemaining > 0f) || !collisionData.IsColliderAgent || collisionData.IsAlternativeAttack || registeredBlow.InflictedDamage <= 0)
            return colReaction;

        var context = BuildCleavingBlowContext(in registeredBlow, in collisionData, attacker, defender, in attackerWeapon);
        return _cleavingBlowService.ShouldSliceThrough(in context, momentumRemaining) ? MeleeCollisionReaction.SlicedThrough : colReaction;
    }

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

    // Hammerhand (#895). One cost gate: only an alternative attack (a bash or kick) between two characters may reach the two skill
    // reads and the roll. The toggle, the horse charge, the shield class, the humanoid test and the immunity verdict are the
    // service's. Fighters the engine did not give a character (unreachable: the melee callers dereference both) decline.
    public bool? ShieldBashKnockdown(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon)
    {
        if (!collisionData.IsAlternativeAttack || attackerAgent?.Character == null || victimAgent?.Character == null)
            return null;

        var context = BuildShieldBashContext(attackerAgent, victimAgent, in collisionData, attackerWeapon);
        return _shieldBashKnockdownService.Decide(in context);
    }

    // A ranged hero kit (#1361) names its shooters here; HeroAbilitiesModule sets it. Any thread reads it: model methods.
    internal static Func<Agent, bool>? ShooterPiercesShields { get; set; }

    // The hero grant comes after the service, so the service's MCM switch (ShieldPenetrationService.cs:51) does not gate
    // it. Only CanPenetrateShield: the arrow passes one shield, never a second body.
    public WeaponFlags PenetrationFlags(Agent? attackerAgent, in MissionWeapon missileWeapon, WeaponFlags missileWeaponFlags)
    {
        var flags = (WeaponFlags)_shieldPenetrationService.ApplyPenetrationFlags(
            missileWeapon.Item?.StringId,
            GetWeaponClassName(missileWeapon.CurrentUsageItem),
            (ulong)missileWeaponFlags);
        return attackerAgent != null && ShooterPiercesShields?.Invoke(attackerAgent) == true
            ? flags | WeaponFlags.CanPenetrateShield
            : flags;
    }

    // With the flag, the engine passes a missile through an unbroken shield only when the final rounded damage is above
    // ShieldPenetrationOffset + ShieldPenetrationFactor x the shield's armour (Mission.cs:5836). That damage is
    // CalculateDamage's result (Mission.cs:6536-6538), and ApplyGeneralDamageModifiers is its last step
    // (AgentApplyDamageModel.cs:18), so a floor placed earlier would be cut by the reductions. The engine calls
    // CalculateDamage, and so this floor, only when its pre-model shield damage is above 0 (Mission.cs:6533-6534). Side
    // effect: the raised damage also wears the shield down, and it often breaks.
    public float ShieldPierceFloor(in AttackInformation attackInformation, in AttackCollisionData collisionData, float damage)
    {
        if (!collisionData.IsMissile || !collisionData.AttackBlockedWithShield)
            return damage;
        var attacker = attackInformation.AttackerAgent;
        if (attacker == null || ShooterPiercesShields?.Invoke(attacker) != true || attackInformation.VictimShield.IsEmpty)
            return damage;
        // TaleWorlds.Core's, the one Mission.cs:5836 reads (the campaign assembly has a type of the same name).
        var parameters = TaleWorlds.Core.ManagedParameters.Instance;
        return ShieldPierceMath.Floor(
            damage,
            parameters.GetManagedParameter(TaleWorlds.Core.ManagedParametersEnum.ShieldPenetrationOffset),
            parameters.GetManagedParameter(TaleWorlds.Core.ManagedParametersEnum.ShieldPenetrationFactor),
            attackInformation.VictimShield.GetGetModifiedArmorForCurrentUsage());
    }

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

    private static CleavingBlowContext BuildCleavingBlowContext(in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon)
    {
        var usage = attackerWeapon.IsEmpty ? null : attackerWeapon.CurrentUsageItem;
        return new CleavingBlowContext(
            attackerIdentity: attacker,
            attackerKind: attacker.IsPlayerControlled ? CleaveAttackerKind.Player
                : attacker.IsAIControlled ? CleaveAttackerKind.Ai : CleaveAttackerKind.None,
            attackerIsHuman: attacker.IsHuman,
            attackerIsMounted: attacker.HasMount,
            isSwing: b.StrikeType == StrikeType.Swing,
            isAlternativeAttack: collisionData.IsAlternativeAttack,
            isPassiveAttack: attacker.IsDoingPassiveAttack,
            isHorseCharge: collisionData.IsHorseCharge,
            isColliderAgent: collisionData.IsColliderAgent,
            blockedByShield: collisionData.AttackBlockedWithShield || collisionData.CollidedWithShieldOnBack,
            hitWithAnotherBone: MissionCombatMechanicsHelper.HitWithAnotherBone(in collisionData, attacker, in attackerWeapon),
            victimShruggedOff: (b.BlowFlag & BlowFlags.ShrugOff) != 0,
            isFatal: victim != null && victim.Health <= 0f,
            inflictedDamage: b.InflictedDamage,
            absorbedByArmor: b.AbsorbedByArmor,
            weaponClassName: GetWeaponClassName(usage),
            attackerWeaponSkill: GetSkillValue(attacker, usage),
            victimWeaponSkill: GetSkillValue(victim, GetWieldedUsageItem(victim)));
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

    // The immunities replace the ShrugOff blow flag, which vanilla sets on nearly every bash and kick: creature stagger immunity, a live
    // race ability that shrugs off blows, and the hero career shrug-off passive (the test TaomAgentApplyDamageModel makes).
    private ShieldBashContext BuildShieldBashContext(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon)
    {
        var statModel = MissionGameModels.Current.AgentStatCalculateModel;
        return new ShieldBashContext(
            isAlternativeAttack: collisionData.IsAlternativeAttack,
            isHorseCharge: collisionData.IsHorseCharge,
            attackerWeaponIsNull: attackerWeapon == null,
            attackerWeaponIsShield: attackerWeapon != null && IsShieldClass(attackerWeapon),
            attackerIsHuman: attackerAgent.IsHuman,
            victimIsImmune: _creatureCombatService.IsUnstoppable(victimAgent.Monster?.StringId, collisionData.InflictedDamage)
                || RaceAbilityHooks.IsShruggingOff(victimAgent)
                || (victimAgent.IsHero && _careerAgentStatService.ShouldShrugOffBlow((victimAgent.Character as CharacterObject)?.HeroObject?.StringId)),
            attackerAthletics: statModel.GetEffectiveSkill(attackerAgent, DefaultSkills.Athletics),
            victimAthletics: statModel.GetEffectiveSkill(victimAgent, DefaultSkills.Athletics),
            victimRaceId: GetRaceId(victimAgent),
            victimAbilityResistanceFactor: RaceAbilityHooks.KnockDownResistance(victimAgent, 1f),
            victimIsPlayer: victimAgent.IsMainAgent,
            randomRoll: MBRandom.RandomFloat,
            // The career TroopKnockdownResistance counter (#769 E10) reaches a bash or kick only here: Hammerhand never asks
            // GetKnockDownResistance. The service applies the 1 + magnitude scale after its gates, so a declined bash is not counted.
            victimCareerResistanceScale: factor => CareerTroopHooks.KnockDownResistance(_careerAgentStatService, victimAgent, factor));
    }

    internal static bool IsShieldClass(WeaponComponentData weapon)
        => weapon.WeaponClass == WeaponClass.SmallShield || weapon.WeaponClass == WeaponClass.LargeShield;

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
