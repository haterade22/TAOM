using System;
using System.Text;
using TAOM.Adapters;
using TAOM.Core.Validation;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Rally;
using Culture = System.Globalization.CultureInfo;

namespace TAOM.Features.WarChronicle.Ledger;

/// <summary>
/// The exact <c>[WarLedger] v=1</c> lines (space-separated key=value tokens, every key present, in this
/// order) that <c>tools/analyze_war_ledger.py</c> parses. Pure and culture-proof: numbers are formatted
/// with the invariant culture so a German thread writes <c>0.300</c>, never <c>0,300</c>. Any text that
/// is not a number goes through <see cref="Token"/>, so an id cannot split a line or forge a key.
/// A change to a line is a format change: bump the version and teach the analyzer.
/// </summary>
internal static class WarLedgerFormatter
{
    private const string Head = "[WarLedger] v=1";
    private const string Unknown = "unknown";

    internal static string Kingdom(
        string campaignId, int day, WarPhase phase, KingdomWarSnapshot k, FactionSide side,
        int? baseline, int tier, float volunteerMultiplier, float escapeMultiplier)
    {
        var points = k.FortificationPoints;
        var cap = k.HomeHeld.HasValue ? (k.HomeHeld.Value ? "1" : "0") : "na";
        return string.Concat(
            Inv($"{Head} t=kingdom cid={Token(campaignId)} day={day} phase={PhaseName(phase)} k={Token(k.Id)} "),
            Inv($"side={SideName(side)} ai={(k.IsPlayerRuled ? 0 : 1)} war={(k.AtWar ? 1 : 0)} "),
            Inv($"towns={k.Towns} castles={k.Castles} pts={points} base={Base(baseline)} loss={Loss(baseline, points)} "),
            Inv($"cap={cap} str={Strength(k.Strength)} pris={k.PrisonerLords} tier={Tier(tier)} "),
            Inv($"vr={Multiplier(volunteerMultiplier)} pe={Multiplier(escapeMultiplier)}"));
    }

    internal static string Side(string campaignId, int day, FactionSide side, int points, int? baseline, int alive) =>
        Inv($"{Head} t=side cid={Token(campaignId)} day={day} side={SideName(side)} pts={points} base={Base(baseline)} alive={alive}");

    /// <summary>The Free share of the Free and Evil points; null (or NaN) is written as na.</summary>
    internal static string Share(string campaignId, int day, double? share)
    {
        var text = share.HasValue && FiniteFloatValidator.IsFinite(share.Value)
            ? share.Value.ToString("0.000", Culture.InvariantCulture)
            : "na";
        return Inv($"{Head} t=share cid={Token(campaignId)} day={day} share={text}");
    }

    internal static string TierEvent(string campaignId, int day, string kingdomId, int from, int to, float loss) =>
        string.Concat(
            Inv($"{Head} t=event cid={Token(campaignId)} day={day} ev=tier k={Token(kingdomId)} "),
            Inv($"from={Tier(from)} to={Tier(to)} loss={Fraction(loss)}"));

    internal static string DestroyedEvent(string campaignId, int day, string kingdomId) =>
        Inv($"{Head} t=event cid={Token(campaignId)} day={day} ev=destroyed k={Token(kingdomId)}");

    /// <summary>The outcome is Held, Fell, Lapsed, Occurred or Skipped; any other text is kept as a safe token.</summary>
    internal static string ChronicleEvent(string campaignId, int day, string eventId, string outcome) =>
        Inv($"{Head} t=event cid={Token(campaignId)} day={day} ev=chronicle id={Token(eventId)} outcome={Token(outcome)}");

    internal static string SideName(FactionSide side)
    {
        switch (side)
        {
            case FactionSide.Free: return "free";
            case FactionSide.Evil: return "evil";
            default: return "neutral";
        }
    }

    private static string Inv(FormattableString text) => text.ToString(Culture.InvariantCulture);

    private static string PhaseName(WarPhase phase) =>
        Enum.IsDefined(typeof(WarPhase), phase) ? phase.ToString() : Unknown;

    private static string Base(int? baseline) =>
        baseline.HasValue ? baseline.Value.ToString(Culture.InvariantCulture) : "na";

    /// <summary>Share of the baseline lost (the rally's own measure); na without a baseline to measure from.</summary>
    private static string Loss(int? baseline, int points)
    {
        var loss = WarBaselineService.LossOf(baseline, points);
        return loss.HasValue ? Fraction(loss.Value) : "na";
    }

    private static string Fraction(float value)
    {
        var clamped = FiniteFloatValidator.IsFinite(value) ? Math.Min(Math.Max(value, 0f), 1f) : 0f;
        return clamped.ToString("0.000", Culture.InvariantCulture);
    }

    private static string Multiplier(float value) =>
        (FiniteFloatValidator.IsFinite(value) ? value : 1f).ToString("0.00", Culture.InvariantCulture);

    private static int Tier(int tier) => Math.Min(Math.Max(tier, 0), 2);

    /// <summary>The strength as a whole number; -1 when the engine gave NaN, an infinity or a negative.</summary>
    private static int Strength(float strength)
    {
        if (!FiniteFloatValidator.IsFinite(strength) || strength < 0f)
            return -1;
        var rounded = Math.Round((double)strength, MidpointRounding.AwayFromZero);
        return rounded >= int.MaxValue ? int.MaxValue : (int)rounded;
    }

    /// <summary>One safe token: whitespace and '=' become '_', and nothing becomes "unknown".</summary>
    internal static string Token(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Unknown;

        var sb = new StringBuilder(text!.Length);
        foreach (var c in text)
            sb.Append(char.IsWhiteSpace(c) || c == '=' ? '_' : c);
        return sb.ToString();
    }
}
