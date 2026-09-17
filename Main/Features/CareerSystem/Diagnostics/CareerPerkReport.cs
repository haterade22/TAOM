using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// Renders a <see cref="CareerPerkSnapshot"/> into the lines <c>taom.career_perks</c> prints and
/// writes to the TAOM debug log (#613). Pure: the command gathers, this formats.
/// </summary>
public static class CareerPerkReport
{
    public const string Prefix = "[CareerPerks]";

    public static List<string> Render(CareerPerkSnapshot s)
    {
        var lines = new List<string>();
        var who = string.IsNullOrEmpty(s.HeroName) ? s.HeroId : $"{s.HeroName} ({s.HeroId})";

        if (!s.HasCareer)
        {
            lines.Add($"{who}: no career. Nothing to report.");
            return lines;
        }

        lines.Add($"{who}: career '{s.CareerId}', level {s.Level}, {s.Rows.Count} passive type(s) held.");
        foreach (var row in s.Rows)
        {
            var line = $"  {row.Type} {Format(row.Type, row.Magnitude)}";
            if (row.Masked != null && row.Masked.Count > 0)
            {
                var parts = new List<string>(row.Masked.Count);
                foreach (var (mask, magnitude) in row.Masked)
                    parts.Add($"{mask} {Format(row.Type, magnitude)}");
                line += " [" + string.Join(", ", parts) + "]";
            }
            line += " -> " + CareerPerkConsumerMap.Describe(row.Type);
            lines.Add(line);
        }

        if (s.Probes.Count > 0)
        {
            lines.Add("  campaign probes (a 'Career' line means the passive reached the number):");
            foreach (var probe in s.Probes)
                lines.Add("    " + probe);
        }

        var m = s.Mission;
        if (m != null)
        {
            if (!m.PlayerAgentAlive)
            {
                lines.Add("  mission: the player agent is not alive; no agent stats to read.");
            }
            else
            {
                lines.Add("  mission, player agent driven properties (after base + career + buffs):");
                lines.Add($"    SwingSpeedMultiplier {F(m.SwingSpeedMultiplier)}, MaxSpeedMultiplier {F(m.MaxSpeedMultiplier)}, DamageMultiplierBonus {F(m.DamageMultiplierBonus)}, ReadySpeedMultiplier {F(m.ReadySpeedMultiplier)}, ArmorEncumbrance {F(m.ArmorEncumbrance)}");
                lines.Add(m.HasMount
                    ? $"    mount: MountChargeDamage {F(m.MountChargeDamage)}, MountSpeed {F(m.MountSpeed)}, HealthLimit {F(m.MountHealthLimit)}"
                    : "    no mount");
                foreach (var slot in m.AmmoSlots)
                    lines.Add("    ammo: " + slot);
                lines.Add("    live ability buff: " + (m.LiveBuff ?? "none"));
            }
        }

        return lines;
    }

    private static string Format(PassiveEffectType type, float magnitude)
        => CareerPerkConsumerMap.IsFlat(type)
            ? (magnitude >= 0 ? "+" : "") + magnitude.ToString("0.##", CultureInfo.InvariantCulture)
            : (magnitude >= 0 ? "+" : "") + (magnitude * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string F(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
