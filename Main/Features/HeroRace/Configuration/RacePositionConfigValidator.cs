using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace.Configuration;

/// <summary>
/// Semantic validation for the two race-framing configs, per the "Config Providers MUST Validate"
/// rule in <c>.claude/rules/csharp-architecture.md</c>.
///
/// <para>These files are hand-edited (the documented workflow for adding a race is "tune values
/// in-game, then edit the JSON"), and every value in them is multiplied into an entity origin that
/// goes straight to a native <c>SetFrame</c>. <see cref="RacePositionConfig.LoadConfig"/> used to
/// catch only PARSE failures, so <c>"Vertical": NaN</c> deserialised cleanly and produced a
/// character with a non-finite position — not a visible mis-frame, an invisible one.</para>
///
/// <para>Fallback is ROW-level on purpose. Zeroing only the offending axis would frame the race
/// half-right, which reads as a bad tuning pass and sends the next person hunting the wrong thing.
/// Dropping the row restores the state the feature already documents and handles: a race with no
/// entry keeps the vanilla frame.</para>
/// </summary>
public static class RacePositionConfigValidator
{
    /// <summary>
    /// Widest offset accepted on any axis, in metres of tableau space. Sized to comfortably admit
    /// real authored values (the largest known tuning is a cave troll at Zoom -4.0) while still
    /// rejecting the misplaced-decimal and wrong-units mistakes a hand edit actually produces.
    /// </summary>
    public const float MaxOffset = 10f;

    /// <inheritdoc cref="MaxOffset"/>
    public const float MinOffset = -10f;

    /// <summary>
    /// Returns a copy of <paramref name="config"/> containing only rows that are safe to apply,
    /// with race names normalised to lower case and duplicates collapsed last-wins (matching the
    /// pre-existing lookup semantics, so sanitising can never change which row a race resolves to).
    /// Never returns null and never throws.
    /// </summary>
    /// <param name="sourceName">Config file name, used only to make warnings actionable.</param>
    /// <param name="warnings">One entry per dropped or collapsed row. Empty when the file is clean.</param>
    public static RacePositionConfig Sanitize(
        RacePositionConfig config,
        string sourceName,
        out IReadOnlyList<string> warnings)
    {
        var messages = new List<string>();
        warnings = messages;

        var sanitized = new RacePositionConfig();
        if (config?.Items == null)
            return sanitized;

        // Preserves file order for the kept rows; the index lets a later duplicate overwrite an
        // earlier one in place rather than appending, so "last wins" holds without a second pass.
        var indexByRace = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var item in config.Items)
        {
            if (item == null)
            {
                messages.Add($"{sourceName}: skipped a null entry.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Race))
            {
                messages.Add($"{sourceName}: skipped an entry with no Race name.");
                continue;
            }

            var race = item.Race.Trim().ToLowerInvariant();

            if (!IsUsableOffset(item.Horizontal) ||
                !IsUsableOffset(item.Vertical) ||
                !IsUsableOffset(item.Zoom))
            {
                messages.Add(
                    $"{sourceName}: dropped race '{race}' — offsets must be finite and within "
                    + $"[{MinOffset}, {MaxOffset}] (got Horizontal={item.Horizontal}, "
                    + $"Vertical={item.Vertical}, Zoom={item.Zoom}). That race now uses vanilla framing.");
                continue;
            }

            var kept = new RacePositionConfigItem
            {
                Race = race,
                Horizontal = item.Horizontal,
                Vertical = item.Vertical,
                Zoom = item.Zoom,
            };

            if (indexByRace.TryGetValue(race, out var existing))
            {
                messages.Add($"{sourceName}: duplicate entry for race '{race}' — the last one wins.");
                sanitized.Items[existing] = kept;
                continue;
            }

            indexByRace[race] = sanitized.Items.Count;
            sanitized.Items.Add(kept);
        }

        return sanitized;
    }

    // Positive requirement, so NaN fails the gate rather than sneaking through an inverted check.
    private static bool IsUsableOffset(float value)
        => FiniteFloatValidator.IsFiniteInRange(value, MinOffset, MaxOffset);
}
