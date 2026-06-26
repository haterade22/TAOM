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
    /// Returns the agent's adjusted max health. A hero gets a flat <c>Health</c> career-passive
    /// add; a MOUNT whose rider is a hero gets a multiplicative <c>HorseHealth</c> bonus. Exactly
    /// one of <paramref name="heroId"/> / <paramref name="mountRiderHeroId"/> is expected to be
    /// non-null (the boundary never sets both — an agent is a hero or a mount, not both).
    /// </summary>
    float ApplyMaxHealthPassives(string? heroId, string? mountRiderHeroId, float baseHealth);

    /// <summary>
    /// Returns the post-amplification damage value. Applies the attacker's ArmorPenetration
    /// career passive and the attacker's <c>Damage</c> passive for the hit's delivery type
    /// (<paramref name="hitMask"/> — a melee or ranged Damage pip only fires on the matching hit).
    /// </summary>
    float CalculateDamageAmplification(string? attackerHeroId, AttackTypeMask hitMask, float baseResult);

    /// <summary>
    /// Returns the post-reduction damage value. Applies the victim's Resistance career passive
    /// for the hit's delivery type (<paramref name="hitMask"/>) + self-buff DamageReductionBonus,
    /// the <c>TroopResistance</c> passive of the victim's party leader (for non-hero troops), and
    /// any AoE ally-buff DamageReductionBonus that matches <paramref name="victimAgentIndex"/>.
    /// </summary>
    float CalculateDamageReduction(string? victimHeroId, int? victimAgentIndex, string? troopLeaderHeroId, AttackTypeMask hitMask, float baseResult);

    /// <summary>
    /// Returns true if the victim hero has a non-zero ShruggedOff career passive.
    /// Caller is responsible for short-circuiting on the base GameModel result first.
    /// </summary>
    bool ShouldShrugOffBlow(string? victimHeroId);
}
