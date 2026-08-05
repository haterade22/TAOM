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

    // Issue #377 — start the ability's active window. Called by AbilityEffectExecutor with
    // the same (possibly mutation-extended) duration that schedules the buff restores, so
    // HUD state and buff expiry stay in sync. No-op for unknown heroes.
    void BeginActiveWindow(string heroStringId, float durationSeconds);

    // Codex review 2026-08-05 P2 — true while the active window is live. Activation is
    // blocked during it: duration mutations can push the window past the cooldown floor
    // (olog_hai: 16s window vs 15s cooldown), and a recast inside the window would
    // double-stack buff contributions for the overlap.
    bool IsAbilityActive(string heroStringId);

    void ClearAll();
}
