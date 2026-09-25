using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Shared race-modifier lookup for the combat services. Validates config race keys against
// IRaceManager.IsValidRaceName lazily on first resolve (the race registry is engine state,
// unavailable at provider construction) and caches per race id for the per-hit hot path.
public interface IRaceCombatModifiersResolver
{
    // Neutral when the id is null/invalid (validate-before-lookup — never the "human" fallback
    // row), when the race has no config row, or when race modifiers are disabled.
    RaceCombatModifiers Resolve(int? raceId);

    // Health to add on top of the engine's campaign base (RaceCombatModifiers.EngineBaseHitPoints) for this race:
    // its row's BaseHitPoints minus the base, or 0 under the same conditions Resolve returns Neutral.
    int BaseHitPointsBonus(int? raceId);
}
