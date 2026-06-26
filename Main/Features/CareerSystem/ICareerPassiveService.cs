using TaleWorlds.CampaignSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerPassiveService
{
    void RefreshCache(ICareerDataService dataService, ICareerRegistry registry);
    float GetPassiveMagnitude(string heroStringId, PassiveEffectType type);

    /// <summary>
    /// Sum of the cached magnitudes for <paramref name="type"/> whose authored
    /// <see cref="AttackTypeMask"/> applies to the hit described by <paramref name="hitMask"/>
    /// (a single Melee or Ranged bit). All-masked entries apply to every hit. Used by the damage
    /// path so a "+X% ranged damage" pip only fires on ranged hits.
    /// </summary>
    float GetMaskedMagnitude(string heroStringId, PassiveEffectType type, AttackTypeMask hitMask);

    bool HasActivePassive(string heroStringId, PassiveEffectType type);

    // Phase 9b #173 — replaces the static CareerPassiveHelper.ApplyFactor / ApplyFlat. Per ADR-007,
    // these accept `string heroStringId` (primitive) not sealed `Hero` — boundary GameModels
    // extract `hero?.StringId` at the call site. `ExplainedNumber` is a public TaleWorlds value
    // type, not a sealed reference; its public API is mockable, so it's adapter-safe.
    void ApplyFactor(string heroStringId, ref ExplainedNumber result, PassiveEffectType type);
    void ApplyFlat(string heroStringId, ref ExplainedNumber result, PassiveEffectType type);
}
