using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Features.CareerSystem.UI;

/// <summary>
/// Issue #382 — pure mapping from <see cref="CareerAbility"/> runtime state to the energy
/// bar's visual state. Kept free of TaleWorlds types so the drain/refill semantics (and
/// especially the refill rescale) are unit-testable.
/// </summary>
public static class CareerEnergyBarStateMapper
{
    // Usable fill width inside the native ShieldHealthBar canvas (prefab units), measured
    // by the reference implementation: canvas minus L12/R10 insets.
    public const float FillMaxWidth = 169f;

    public static CareerEnergyBarState Map(CareerAbility ability)
    {
        var state = default(CareerEnergyBarState);
        if (ability == null) return state;

        if (ability.IsActive)
        {
            state.IsActive = true;
            state.Fill01 = Clamp01(ability.ActiveProgress01);
            return state;
        }

        if (ability.IsReady)
        {
            state.IsReady = true;
            state.Fill01 = 1f;
            return state;
        }

        state.IsCooldown = true;

        // Refill rescale: the cooldown drains during the active window, so at drain-end
        // the raw progress is already ActiveDuration/CooldownDuration (measured live:
        // 0.13 at cast → 0.40 after 8s). Rescale so the refill starts empty instead of
        // snapping. Both durations are known state — no captured hand-off needed.
        var raw = Clamp01(ability.ReadyProgress01);
        var cooldown = ability.CooldownDuration;
        var active = ability.ActiveDuration;
        if (cooldown > 0f && active > 0f && active < cooldown)
        {
            var drainEndProgress = active / cooldown;
            state.Fill01 = Clamp01((raw - drainEndProgress) / (1f - drainEndProgress));
        }
        else
        {
            state.Fill01 = raw;
        }

        return state;
    }

    private static float Clamp01(float v)
    {
        // Positive-requirement form: NaN fails both gates and lands at 0 (NaN-gate rule).
        if (!(v > 0f)) return 0f;
        if (v > 1f) return 1f;
        return v;
    }
}

public struct CareerEnergyBarState
{
    public bool IsReady;
    public bool IsActive;
    public bool IsCooldown;
    public float Fill01;
}
