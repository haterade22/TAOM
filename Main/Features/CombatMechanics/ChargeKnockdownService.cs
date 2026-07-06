using System;
using TAOM.Core.Validation;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Spec mechanic 5 (the combat-mechanics spec) — TAOM-original
// weight-driven two-branch charge knockdown. Branch A: overwhelming mass (weight ratio ≥ the MCM
// auto-knockdown slider) at charging speed knocks down unconditionally, ignoring the engine's
// KnockBack (0.7-dot) gate. Branch B: the vanilla deterministic formula with the penetration term
// scaled by weight ratio — heavier victims see a smaller effective penetration, so a horse should
// not floor a troll. Per-hit hot path: config cached at construction, no LINQ, no allocation.
public class ChargeKnockdownService : IChargeKnockdownService
{
    // Sanity window for Monster.RelativeSpeedLimitForCharge — humanoid Monsters carry the engine
    // sentinel float.MaxValue when the XML attribute is absent; anything non-finite or outside
    // this window falls back to the config default (4.3, Native horse).
    private const float MinValidSpeedReference = 0.1f;
    private const float MaxValidSpeedReference = 1e6f;

    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly IRaceCombatModifiersResolver _raceModifiers;

    // Cached once — JSON config is fixed for the process (singleton provider). Settings getters
    // are read per call because MCM toggles/sliders change mid-session.
    private readonly ChargeKnockdownConfig _config;

    public ChargeKnockdownService(
        ICombatMechanicsConfigProvider configProvider,
        ICombatMechanicsSettingsProvider settings,
        IRaceCombatModifiersResolver raceModifiers)
    {
        _settings = settings;
        _raceModifiers = raceModifiers;
        _config = configProvider.GetConfig().ChargeKnockdown ?? new ChargeKnockdownConfig();
    }

    public bool? DecideChargeKnockdown(in ChargeKnockdownContext context)
    {
        if (!_settings.ChargeKnockdownEnabled)
            return null;
        if (!context.IsHorseCharge)
            return null;

        // Engine-faithful: MissionCombatMechanicsHelper also refuses a knockdown on ShrugOff.
        if (context.HasShrugOffFlag)
            return null;

        float speedRef = FiniteFloatValidator.IsFiniteInRange(
            context.ChargerSpeedLimitForCharge, MinValidSpeedReference, MaxValidSpeedReference)
            ? context.ChargerSpeedLimitForCharge
            : _config.DefaultChargeSpeedReference;
        float speedFactor = Clamp01(context.ChargeVelocity / speedRef);

        // Corrupt engine input (NaN velocity/resistance) → defer to vanilla rather than emit an
        // owned verdict computed from garbage: NaN comparisons are all false, so without this a
        // NaN would sail past Branch A into Branch B's owned `false` and suppress knockdowns
        // vanilla's own (velocity-free) formula might grant.
        if (float.IsNaN(speedFactor) || float.IsNaN(context.VictimKnockDownResistance))
            return null;

        int chargerWeight = context.ChargerWeight + (_config.IncludeRiderWeight ? context.RiderWeight : 0);
        float weightRatio = chargerWeight / (float)Math.Max(context.VictimWeight, 1);

        // Branch A — overwhelming mass at charging speed; ignores the KnockBack (0.7-dot) gate.
        if (weightRatio >= _settings.ChargeAutoKnockdownWeightRatio
            && speedFactor >= _config.AutoKnockdownMinSpeedFactor)
        {
            return true;
        }

        // Branch B — weight-scaled vanilla formula; the KnockBack flag keeps 0.7-dot parity.
        if (!context.HasKnockBackFlag)
            return null;

        float raceResist = _raceModifiers.Resolve(context.VictimRaceId).KnockdownResistanceMultiplier;
        float penetration = _config.HorseChargePenetration
            * Clamp(weightRatio / _config.NeutralWeightRatio, _config.MinPenetrationFactor, _config.MaxPenetrationFactor)
            * speedFactor;

        // false here is an OWNED verdict — deliberately stricter than vanilla for light chargers;
        // never convert it to null.
        return context.InflictedDamage >= context.VictimMaxHealth
            * Math.Max(0f, context.VictimKnockDownResistance * raceResist - penetration);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;
        return value > 1f ? 1f : value;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }
}
