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
    /// Returns the post-amplification damage value. Applies the attacker's ArmorPenetration
    /// career passive and the attacker's <c>Damage</c> passive for the hit's delivery type
    /// (<paramref name="hitMask"/> — a melee or ranged Damage pip only fires on the matching hit),
    /// plus the <c>TroopDamage</c> passive of the attacker's party leader when the attacker is a
    /// non-hero troop (the offensive mirror of <c>TroopResistance</c>, not mask-gated).
    /// <paramref name="attackerHeroId"/> and <paramref name="attackerTroopLeaderHeroId"/> are
    /// mutually exclusive — the boundary returns null for the latter when the attacker is a hero.
    /// </summary>
    float CalculateDamageAmplification(string? attackerHeroId, string? attackerTroopLeaderHeroId, AttackTypeMask hitMask, float baseResult);

    /// <summary>
    /// Returns the post-reduction damage value. Applies the victim's Resistance career passive
    /// for the hit's delivery type (<paramref name="hitMask"/>) + self-buff DamageReductionBonus,
    /// the <c>TroopResistance</c> passive of the victim's party leader (for non-hero troops), and
    /// any AoE ally-buff DamageReductionBonus that matches <paramref name="victimAgentIndex"/>.
    /// </summary>
    float CalculateDamageReduction(string? victimHeroId, int? victimAgentIndex, string? troopLeaderHeroId, AttackTypeMask hitMask, float baseResult);

    /// <summary>
    /// Returns true if the victim hero has a non-zero ShrugOff career passive.
    /// Caller is responsible for short-circuiting on the base GameModel result first.
    /// </summary>
    bool ShouldShrugOffBlow(string? victimHeroId);
}
