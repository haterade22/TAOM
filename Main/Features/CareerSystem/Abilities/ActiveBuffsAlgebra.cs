namespace TAOM.Features.CareerSystem.Abilities;

// Pure algebra helpers for composing ActiveBuffs without any TaleWorlds dependency.
// Extracted so the accumulate-on-apply / subtract-on-expire correctness can be unit-tested.
//
// Why this matters: Codex review P2 found that the old SetAllyBuff replace-semantics
// silently dropped concurrent auras. The fix — accumulate on apply, subtract on expire —
// depends on these four operations being exact inverses for every field. A test suite on
// this class makes the guarantee mechanical, not tribal.
public static class ActiveBuffsAlgebra
{
    // target += deltas, field-by-field
    public static void Accumulate(ActiveBuffs target, ActiveBuffs deltas)
    {
        target.SpeedMultiplier += deltas.SpeedMultiplier;
        target.CombatSpeedMultiplier += deltas.CombatSpeedMultiplier;
        target.DamageBonus += deltas.DamageBonus;
        target.ArmorReduction += deltas.ArmorReduction;
        target.DrawSpeedBonus += deltas.DrawSpeedBonus;
        target.MountSpeedBonus += deltas.MountSpeedBonus;
        target.ChargeDamageBonus += deltas.ChargeDamageBonus;
        target.DamageReductionBonus += deltas.DamageReductionBonus;
    }

    // target -= deltas, field-by-field. Exact inverse of Accumulate.
    public static void Subtract(ActiveBuffs target, ActiveBuffs deltas)
    {
        target.SpeedMultiplier -= deltas.SpeedMultiplier;
        target.CombatSpeedMultiplier -= deltas.CombatSpeedMultiplier;
        target.DamageBonus -= deltas.DamageBonus;
        target.ArmorReduction -= deltas.ArmorReduction;
        target.DrawSpeedBonus -= deltas.DrawSpeedBonus;
        target.MountSpeedBonus -= deltas.MountSpeedBonus;
        target.ChargeDamageBonus -= deltas.ChargeDamageBonus;
        target.DamageReductionBonus -= deltas.DamageReductionBonus;
    }

    // Field-level clone. ExpiresAt is intentionally NOT copied — callers schedule that themselves.
    public static ActiveBuffs Clone(ActiveBuffs source) => new ActiveBuffs
    {
        SpeedMultiplier = source.SpeedMultiplier,
        CombatSpeedMultiplier = source.CombatSpeedMultiplier,
        DamageBonus = source.DamageBonus,
        ArmorReduction = source.ArmorReduction,
        DrawSpeedBonus = source.DrawSpeedBonus,
        MountSpeedBonus = source.MountSpeedBonus,
        ChargeDamageBonus = source.ChargeDamageBonus,
        DamageReductionBonus = source.DamageReductionBonus,
    };
}
