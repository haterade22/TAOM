using TaleWorlds.CampaignSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerPassiveService
{
    void RefreshCache(ICareerDataService dataService, ICareerRegistry registry);

    /// <summary>
    /// The unfiltered per-type total: every passive of <paramref name="type"/> the hero holds, summed,
    /// whatever its attack_type_mask or troop_class. The reader for every non-combat type.
    /// </summary>
    float GetPassiveMagnitude(string? heroStringId, PassiveEffectType type);

    /// <summary>
    /// Sum of the cached magnitudes for <paramref name="type"/> whose authored
    /// <see cref="AttackTypeMask"/> applies to the hit described by <paramref name="hitMask"/>
    /// (a single Melee or Ranged bit). All-masked entries apply to every hit. Used by the damage
    /// path so a "+X% ranged damage" pip only fires on ranged hits.
    /// </summary>
    float GetMaskedMagnitude(string? heroStringId, PassiveEffectType type, AttackTypeMask hitMask);

    /// <summary>
    /// #769 E9: the sum of the cached magnitudes for <paramref name="type"/> that reach a troop of class
    /// <paramref name="troopClass"/>: a passive counts when its troop_class is All or shares a bit with the
    /// troop's, and, when <paramref name="hitMask"/> is given, when its attack_type_mask matches the hit.
    /// A troop of class None still receives the All-class passives. With no hit mask, a passive whose mask is None
    /// (an unparseable one) is skipped. Serves the per-blow damage path, the troop counters, the console and the
    /// army-wide <see cref="ApplyFactor"/> read (and <see cref="ApplyFlat"/> without a class) for the class-filtered types
    /// (<see cref="PassiveEffectConsumers.ClassFiltered"/>).
    /// </summary>
    float GetTroopMagnitude(string? heroStringId, PassiveEffectType type, TroopClassMask troopClass, AttackTypeMask? hitMask = null);

    bool HasActivePassive(string? heroStringId, PassiveEffectType type);

    // Phase 9b #173 — replaces the static CareerPassiveHelper.ApplyFactor / ApplyFlat. Per ADR-007,
    // these accept `string heroStringId` (primitive) not sealed `Hero` — boundary GameModels
    // extract `hero?.StringId` at the call site. `ExplainedNumber` is a public TaleWorlds value
    // type, not a sealed reference; its public API is mockable, so it's adapter-safe.
    // For a class-filtered type (PassiveEffectConsumers.ClassFiltered) ApplyFactor reads the ARMY-WIDE pips only, as a
    // troop of class None would, and so does ApplyFlat unless it is given a troop class: a caller with no troop class
    // must not count a class-only pip at full magnitude.
    void ApplyFactor(string? heroStringId, ref ExplainedNumber result, PassiveEffectType type);

    /// <summary>
    /// A flat count added to <paramref name="result"/>, worth exactly its magnitude whatever factors are already on
    /// the number, so call it after the last AddFactor. <paramref name="troopClass"/> is for a class-filtered type
    /// whose consumer HAS a class (<see cref="PassiveEffectType.TroopTraining"/>: the training model is asked once
    /// per roster element): the pips that reach that class are read, and a troop with no class gets the All-class
    /// pips. Left at None it reads the army-wide pips only.
    /// </summary>
    void ApplyFlat(string? heroStringId, ref ExplainedNumber result, PassiveEffectType type, TroopClassMask troopClass = TroopClassMask.None);

    /// <summary>
    /// #769 E11: a raw float scaled by the hero's passive, <c>value * (1 + magnitude)</c>, for a model method that
    /// returns a float (the raid loot multiplier, the trade penalty factor) or hands over one share of a list (the
    /// loot gold chances). The value comes back untouched for no hero or no passive. The scale floors at zero, so a
    /// magnitude of -1 or less is the full cut (a value of 0), with no cliff at -1 and never a negative. The loader
    /// keeps NaN and infinity out of the cache, so the scale is always finite. Writes the <c>[CareerPerks]</c> line for
    /// an event-scoped type when the value moved.
    /// </summary>
    float ScaleByPassive(string? heroStringId, float value, PassiveEffectType type);

    /// <summary>
    /// #769 E11: <see cref="PassiveEffectType.WoundRecovery"/> on the party's daily healing. A factor on a
    /// negative value (a starving party, a raft) would wound the troops faster, and the <c>isPrisoner</c> call is
    /// the captor healing its prisoners, not the leader's own troops, so the factor applies only to a healing value
    /// above zero on the non-prisoner path. The factor stops at -1, so a total below it can never turn a recovery
    /// into a daily wound.
    /// </summary>
    void ApplyWoundRecovery(string? heroStringId, bool isPrisoner, ref ExplainedNumber result);
}
