using System.Collections.Generic;
using System.Globalization;

namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>One-line rendering of a buff entry for the <c>[CareerPerks]</c> log and the
/// <c>taom.career_perks</c> report (#613).</summary>
public static class ActiveBuffsFormat
{
    public static string Describe(ActiveBuffs b)
    {
        var parts = new List<string>(6);
        if (b.DamageBonus != 0f) parts.Add("dmg " + Pct(b.DamageBonus));
        if (b.DamageReductionBonus != 0f) parts.Add("reduction " + Pct(b.DamageReductionBonus));
        if (b.SpeedMultiplier != 0f) parts.Add("speed " + Pct(b.SpeedMultiplier));
        if (b.DrawSpeedBonus != 0f) parts.Add("draw " + Pct(b.DrawSpeedBonus));
        if (b.MountSpeedBonus != 0f) parts.Add("mount speed " + Pct(b.MountSpeedBonus));
        if (b.ChargeDamageBonus != 0f) parts.Add("charge " + Pct(b.ChargeDamageBonus));
        return parts.Count == 0 ? "(empty)" : string.Join(" ", parts);
    }

    public static string Pct(float magnitude)
        => (magnitude >= 0f ? "+" : "") + (magnitude * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
