using System;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

// Phase 9b — extracted from inline bodies of TaomAgentStatCalculateModel /
// TaomAgentApplyDamageModel to satisfy gamemodels.md rule 4 (no branching in override
// bodies). Closes deferred audit-issue #142.
//
// Per ADR-007 the surface accepts primitive `string? heroId` / `int agentIndex`, not
// sealed `Agent`/`Hero`. Boundary GameModels extract `(agent.Character as
// CharacterObject)?.HeroObject?.StringId` and `agent.Index` at the call site.
//
// `AgentDrivenProperties` is a TaleWorlds reference type (class, not struct) — passed
// by-value, mutations on the same reference flow back to the caller. Verified via
// `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentDrivenProperties.cs`.
public interface ICareerAgentStatService
{
    /// <summary>
    /// Apply career passive bonuses + active ability buffs to an agent's driven properties.
    /// Mutates <paramref name="props"/> in place. Caller should invoke this AFTER
    /// <c>base.UpdateAgentStats</c> so the base recalc does not overwrite our buffs.
    /// </summary>
    /// <param name="heroId">StringId of the hero (null for non-hero agents).</param>
    /// <param name="agentIndex">Mission-local agent index (used for AoE ally buff lookup).</param>
    /// <param name="isHuman">True if the agent is a human (non-humans skip all branches).</param>
    /// <param name="isHero">True if the agent is a player/companion hero.</param>
    /// <param name="props">The agent's driven properties — mutated in place.</param>
    void ApplyAgentStatModifiers(string? heroId, int agentIndex, bool isHuman, bool isHero, AgentDrivenProperties props);

    /// <summary>
    /// Returns a MOUNT's adjusted max health — a multiplicative <c>MountHealth</c> bonus when the
    /// mount's rider is a hero carrying that passive. Pass null for any non-mount agent.
    /// <para>
    /// The hero <c>Health</c> passive is NOT handled here (#394): it is applied campaign-side by
    /// <c>TaomCharacterStatsModel.MaxHitpoints</c>, and <c>SandboxAgentStatCalculateModel</c>'s
    /// hero branch returns <c>agent.Character.MaxHitPoints()</c>, which already routes through that
    /// model. Adding it on this path too would double-count it in battle.
    /// </para>
    /// </summary>
    float ApplyMountHealthPassives(string? mountRiderHeroId, float baseHealth);

    /// <summary>
    /// Applies the rider's mount-side career bonuses to a MOUNT's driven properties (#611): the
    /// rider hero's <c>MountChargeDamage</c> passive, and the Cavalry ability's
    /// <c>MountSpeedBonus</c> / <c>ChargeDamageBonus</c> from the rider's self buff and the ally buff
    /// keyed by the rider's agent index. Mutates <paramref name="mountProps"/> in place. Both ids
    /// null (a riderless horse, or a human agent) is a no-op.
    /// <para>
    /// Why the mount and not the rider: the base models write <c>MountChargeDamage</c> and
    /// <c>MountSpeed</c> only in their horse branch, and the engine reads them off the horse
    /// (<c>AttackInformation</c> takes the charge property from the attacker, and the charge
    /// callback's attacker is the mount). A rider-side multiply scales the default 0 and is never
    /// read. Call AFTER <c>base.UpdateAgentStats</c> on the mount, which rewrites both.
    /// </para>
    /// </summary>
    void ApplyMountStatModifiers(string? riderHeroId, int? riderAgentIndex, AgentDrivenProperties mountProps);

    /// <summary>
    /// The charge half of <see cref="ApplyMountStatModifiers"/> as one multiplier: (1 + the rider hero's
    /// <c>MountChargeDamage</c> passive) x (1 + the self buff's <c>ChargeDamageBonus</c>) x (1 + the ally buff's,
    /// keyed by the rider's agent index). 1 when both ids are null, nothing applies, or the product is not finite
    /// (an overflow). The mount's own charge is scaled by exactly this, and the antler attacks read it at the moment they
    /// fire (the great elk's, #636, and the Animalia elk's and moose's, #646), so the two agree for any product the antler accepts, (0, 10].
    /// </summary>
    float MountChargeMultiplier(string? riderHeroId, int? riderAgentIndex);

    /// <summary>The hero's <c>Ammo</c> passive as a fraction (0.10 for +10%), 0 for no hero, no
    /// passive or a negative value. The model hands it to <c>CareerAmmoApplier</c> from
    /// <c>InitializeMissionEquipment</c> (#613).</summary>
    float AmmoBonus(string? heroId);

    /// <summary>Clears the <c>[CareerPerks]</c> dedupe state (the stat and mount lines and the
    /// per-hit lines) so the next mission logs again. Called from the mission behavior's OnEndMission and
    /// again from its OnRemoveBehavior, so it runs on every way a mission can end (#613). Before clearing, writes
    /// the mission's per-hit summary at INFO: one line per hit combination with its hit count, multiplier range and
    /// damage totals. Also writes and clears the troop counter summary (one line per leader and class, #769 E10);
    /// the set of hooks that already reported a fault is never cleared, so each hook reports once per session.
    /// The second call after a normal end finds nothing to write.</summary>
    void ResetDiagnostics();

    /// <summary>
    /// Returns the post-amplification damage value. Applies the attacker's ArmorPenetration
    /// career passive and the attacker's <c>Damage</c> passive for the hit's delivery type
    /// (<paramref name="hitMask"/> — a melee or ranged Damage pip only fires on the matching hit),
    /// plus the <c>TroopDamage</c> passive of the attacker's party leader when the attacker is a
    /// non-hero troop (the offensive mirror of <c>TroopResistance</c>), honouring that passive's
    /// attack_type_mask against <paramref name="hitMask"/> and its troop_class against
    /// <paramref name="attackerTroopClass"/> (#769 E9; None for a troop with no class, which still gets
    /// the All-class passives). <paramref name="attackerHeroId"/> and <paramref name="attackerTroopLeaderHeroId"/>
    /// are mutually exclusive — the boundary returns null for the latter when the attacker is a hero.
    /// </summary>
    float CalculateDamageAmplification(string? attackerHeroId, string? attackerTroopLeaderHeroId, TroopClassMask attackerTroopClass, AttackTypeMask hitMask, float baseResult);

    /// <summary>
    /// Returns the post-reduction damage value. Applies the victim's Resistance career passive
    /// for the hit's delivery type (<paramref name="hitMask"/>) + self-buff DamageReductionBonus,
    /// the <c>TroopResistance</c> passive of the victim's party leader (for non-hero troops, matched on
    /// the hit's attack_type_mask and on <paramref name="victimTroopClass"/>, #769 E9), and
    /// any AoE ally-buff DamageReductionBonus that matches <paramref name="victimAgentIndex"/>.
    /// </summary>
    float CalculateDamageReduction(string? victimHeroId, int? victimAgentIndex, string? troopLeaderHeroId, TroopClassMask victimTroopClass, AttackTypeMask hitMask, float baseResult);

    /// <summary>
    /// #769 E10: the enemy's fear drain against a troop of <paramref name="troopClass"/> led by
    /// <paramref name="leaderHeroId"/>, shrunk by that leader's TroopFearResistance (a magnitude of 1 or more
    /// drains nothing). Unchanged with no leader, no positive magnitude, or a drain that is not finite.
    /// </summary>
    float ScaleTroopFear(string? leaderHeroId, TroopClassMask troopClass, float drain);

    /// <summary>
    /// #769 E10: the knockdown resistance of a troop led by <paramref name="leaderHeroId"/>, times
    /// (1 + TroopKnockdownResistance). The engine multiplies the result into HealthLimit, and vanilla returns
    /// float.MaxValue for a non-human, so a result that is not finite comes back as the input.
    /// </summary>
    float ScaleTroopKnockdownResistance(string? leaderHeroId, TroopClassMask troopClass, float resistance);

    /// <summary>
    /// #769 E10: whether a troop led by <paramref name="leaderHeroId"/> holds against a crush-through,
    /// at the chance TroopCrushHold gives (clamped 0 to 1). <paramref name="roll"/> is drawn only when the
    /// troop has the perk, so a soldier without it never advances the random stream.
    /// </summary>
    bool HoldsAgainstCrush(string? leaderHeroId, TroopClassMask troopClass, Func<float> roll);

    /// <summary>
    /// #769 E10: a troop-counter hook (<c>CareerTroopHooks</c>) caught an exception and handed its input back.
    /// Logs the first fault of each <paramref name="hook"/> once, so a counter that has gone inert after a
    /// refactor or an engine bump leaves a line in the log instead of silence. Never throws.
    /// </summary>
    void ReportHookFault(string hook, Exception ex);

    /// <summary>
    /// Returns true if the victim hero has a non-zero ShrugOff career passive.
    /// Caller is responsible for short-circuiting on the base GameModel result first.
    /// </summary>
    bool ShouldShrugOffBlow(string? victimHeroId);
}
