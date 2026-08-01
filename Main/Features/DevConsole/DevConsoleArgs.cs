using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Validation;

namespace TAOM.Features.DevConsole;

/// <summary>
/// Shared console argument parsing. Pure — no engine state, no side effects — so every branch is
/// unit-testable, which is the point: these are the seams where independently-written commands
/// drift apart (<c>.claude/rules/harness-facts.md</c>, "Parallel builder briefs").
///
/// Two rules encoded here rather than left to each command's author:
///
/// 1. <b>Invariant culture, always.</b> Current-culture parsing reads "12.5" as 125 on a
///    comma-decimal locale.
/// 2. <b>Non-finite floats are rejected, not clamped.</b> <c>float.TryParse</c> happily accepts
///    "NaN" and "Infinity", and every subsequent comparison against a NaN returns false, so the
///    value sails past whatever range check the command applies next. That bug class has shipped
///    five times in TAOM (<c>.claude/rules/csharp-architecture.md</c>, "Engine-Float Decision
///    Gates"); the console is simply one entry point earlier.
///
/// Error strings reuse vanilla's <see cref="CampaignCheats"/> constants where one fits, so TAOM's
/// console reads like the engine's.
/// </summary>
internal static class DevConsoleArgs
{
    /// <summary>
    /// Parses a whole-number count and range-checks it. Rejects float spellings outright rather
    /// than truncating — "2.9" is a typo, and silently delivering 2 hides it.
    /// </summary>
    internal static bool TryParseCount(string raw, int min, int max, out int value, out string error)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = CampaignCheats.EnterNumber;
            return false;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            error = $"{CampaignCheats.EnterNumber} — '{raw}' is not a whole number.";
            return false;
        }

        if (parsed < min || parsed > max)
        {
            error = $"Count must be between {min} and {max} (got {parsed}).";
            return false;
        }

        value = parsed;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses a signed float amount. Deliberately does NOT range-check: sign and magnitude policy
    /// belong to the calling command (a resource grant accepts negatives; a damage amount does not).
    /// Finiteness is enforced here because no command should have to remember it.
    /// </summary>
    internal static bool TryParseAmount(string raw, out float value, out string error)
    {
        value = 0f;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = CampaignCheats.EnterNumber;
            return false;
        }

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            error = $"{CampaignCheats.EnterNumber} — '{raw}' is not a number.";
            return false;
        }

        if (!FiniteFloatValidator.IsFinite(parsed))
        {
            error = $"Amount must be a finite number ('{raw}' parses, but poisons every comparison downstream).";
            return false;
        }

        value = parsed;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses the optional side argument for <c>spawn_troops</c>. Omitted means enemy — when you
    /// spawn troops mid-battle you almost always want something to fight.
    ///
    /// An unrecognised value is an ERROR, never a silent fall-through to the default: a typo'd
    /// "enemey" quietly spawning allies is the parsed-but-unresolvable trap.
    /// </summary>
    internal static bool TryParseSide(string raw, out bool isPlayerSide, out string error)
    {
        isPlayerSide = false;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "enemy": isPlayerSide = false; return true;
            case "ally":
            case "player": isPlayerSide = true; return true;
            default:
                error = $"Unknown side '{raw}' — expected 'enemy' or 'ally'.";
                return false;
        }
    }
}
