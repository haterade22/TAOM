using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Turns a <see cref="ChronicleOutcomeReport"/> into the event window's effects list (docs/features/war-chronicle.md
/// "The event window"). Values are this event's contribution, the configured fraction times the War effect strength,
/// before the registry's caps (<see cref="WarEffectMath.Clamp"/>), which bind a single effect too: volunteers 0.4 at
/// strength 2 shows +80% but applies +50%. The window never shows a net value. Escape is a second daily roll at
/// <c>p x (multiplier - 1)</c> (<see cref="WarEscapeService"/>), so <c>+20%</c> is a roll at a fifth of the usual
/// chance, not a 20% larger chance. At strength 0 the registry applies nothing, so the timed rows are left out. A duration every
/// row shares is one line under the list; otherwise each row carries its own. Pure: the language and the kingdom names come in as functions.
/// </summary>
internal static class ChronicleEffectFormatter
{
    internal const string FreeSide = "{=taom_event_side_free}Free Peoples";
    internal const string EvilSide = "{=taom_event_side_evil}The Shadow";
    internal const string VolunteerLabel = "{=taom_event_effect_volunteers}Volunteer recruitment rate";
    internal const string EscapeLabel = "{=taom_event_effect_escape}Additional escape roll";
    internal const string MomentumLabel = "{=taom_event_effect_momentum}War of the Ring momentum";
    internal const string Note = "{=taom_event_effect_note}Values are this event's contributions before caps, and the caps apply even to a single effect. Your own clan's fiefs and lords are never affected.";
    internal const string EscapeNote = "{=taom_event_effect_escape_note}The additional escape roll's chance is the shown percentage of a lord's usual escape chance.";

    /// <param name="report">What the resolution applied; null or empty gives null.</param>
    /// <param name="strength">The War effect strength setting, sanitized as the registry sanitizes it.</param>
    /// <param name="momentumDays">How long a chronicle momentum award lasts; 0 or less when unknown.</param>
    /// <param name="localize">Resolves a <c>{=key}English</c> string.</param>
    /// <param name="forDays">The duration text for a number of days ("For 15 days").</param>
    /// <param name="kingdomName">A kingdom's name, empty when unknown.</param>
    public static EventWindowEffects? Format(ChronicleOutcomeReport? report, float strength, int momentumDays,
        Func<string, string> localize, Func<int, string> forDays, Func<string, string> kingdomName)
    {
        if (report == null || report.IsEmpty)
            return null;

        strength = WarEffectMath.SanitizeStrength(strength);
        var rows = new List<(string Target, string Label, string Value, int Days)>();
        var escape = false;
        if (strength > 0f)
        {
            foreach (var effect in report.Effects)
            {
                var percent = Math.Round(effect.Magnitude * strength * 100d, 1, MidpointRounding.AwayFromZero);
                if (percent == 0d)
                    continue;
                rows.Add((TargetName(effect, localize, kingdomName), localize(KindLabel(effect.Kind)),
                    Signed(percent) + "%", effect.DurationDays));
                escape |= effect.Kind == WarEffectKind.PrisonerEscape;
            }
        }

        var timed = rows.Count;
        if (report.Momentum != null)
        {
            rows.Add((localize(report.Momentum.Side == MomentumSide.Free ? FreeSide : EvilSide), localize(MomentumLabel),
                Signed(report.Momentum.Units), momentumDays > 0 ? momentumDays : 0));
        }

        if (rows.Count == 0)
            return null;

        var shared = rows[0].Days > 0 && rows.All(r => r.Days == rows[0].Days);
        var built = rows.Select(r => new EventWindowEffectRow(r.Target, r.Label, r.Value,
            shared || r.Days <= 0 ? null : forDays(r.Days))).ToList();
        var note = timed == 0 ? null : escape ? localize(Note) + " " + localize(EscapeNote) : localize(Note);
        return new EventWindowEffects(built, shared ? forDays(rows[0].Days) : null, note);
    }

    private static string TargetName(ChronicleAppliedEffect effect, Func<string, string> localize, Func<string, string> kingdomName)
    {
        switch (effect.Target)
        {
            case ChronicleConfigValidator.TargetFree:
                return localize(FreeSide);
            case ChronicleConfigValidator.TargetEvil:
                return localize(EvilSide);
            default:
                return string.Join(", ", effect.KingdomIds.Select(id =>
                {
                    var name = kingdomName(id);
                    return string.IsNullOrWhiteSpace(name) ? id : name;
                }));
        }
    }

    private static string KindLabel(WarEffectKind kind) => kind == WarEffectKind.PrisonerEscape ? EscapeLabel : VolunteerLabel;

    private static string Signed(double value) => value.ToString("+0.#;-0.#", System.Globalization.CultureInfo.InvariantCulture);
}
