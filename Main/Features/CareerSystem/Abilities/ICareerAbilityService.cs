using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

public interface ICareerAbilityService
{
    CareerAbility GetOrCreateAbility(string heroStringId, ICareerRegistry registry, ICareerDataService dataService);
    void Tick(string heroStringId, float dt);
    bool IsAbilityReady(string heroStringId);
    float GetCooldownRemaining(string heroStringId);
    void ActivateAbility(string heroStringId);

    // Issue #104 Option B — shorten the active cooldown by reductionSeconds (clamped at
    // minCooldownSeconds). Called by AbilityEffectExecutor AFTER the mutated template is
    // produced so designer CooldownReduction mutations on choice trees take effect on the
    // CURRENT activation. No-op for unknown heroes or non-cooldown-based abilities.
    void ApplyCooldownAdjustment(string heroStringId, float reductionSeconds, float minCooldownSeconds);

    void ClearAll();
}
