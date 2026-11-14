using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Validation;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Parses and validates <c>war_chronicle/war_chronicle.json</c> (docs/features/war-chronicle.md
/// "Validation rules"). Pure: every problem is a line in the caller's warning list.
///
/// <list type="bullet">
/// <item>The file: readable JSON, <c>schemaVersion</c> 1, an <c>events</c> array, at most
/// <see cref="MaxEvents"/> events (the rest are skipped).</item>
/// <item>An event is skipped for: a blank or duplicate id; an unknown kind or anchor; an <c>Event</c>
/// anchor without an <c>anchorEvent</c>, or one naming an event that is not earlier in the file (so no
/// cycle can form); an <c>anchorEvent</c> on a phase anchor; a <c>requiresOutcome</c> without an Event
/// anchor, of an unknown state, or one the anchor's kind never reaches; an offset outside 0 to 365; no
/// title; a narrative without an <c>occurred</c> outcome; a siege with a window outside 1 to 60, no
/// target, no defender, no attacker, the defender among the attackers, or an anchor chain that opens
/// before the Full War (decision D2: peace is not blocked before Phase 2).</item>
/// <item>An effect row is skipped alone for: a blank target; <c>attacker</c> or <c>defender</c> on a
/// narrative; an unknown kind; a magnitude that is not finite, is zero, or is past
/// <see cref="MaxMagnitude"/> either way (finite-checked before the range check); a duration outside 1
/// to 60.</item>
/// <item>A momentum award is dropped alone when it is not an object, for a side other than <c>free</c> or
/// <c>evil</c>, or units outside 1 to <see cref="MaxMomentumUnits"/>.</item>
/// <item>Kinds, anchors, outcome states and effect kinds are names only (<see cref="EnumNames"/>: trimmed,
/// any case); a number, a sign or a comma list never parses.</item>
/// <item>A key the level does not know (event, outcome, effect, momentum) warns and is ignored, so a typo
/// cannot drop a block silently; a key starting with <c>_</c> is an author's note. An optional text that
/// is not a string, a siege's <c>outcomes</c> that is not an object and <c>effects</c> that is not an
/// array warn too; a <c>requiresOutcome</c> that is not a string skips the event, since the author meant
/// a gate.</item>
/// </list>
/// Settlement and kingdom ids are checked against the campaign at session launch, not here.
/// </summary>
public static class ChronicleConfigValidator
{
    public const int SchemaVersion = 1;
    public const int MaxEvents = 32;
    public const int MaxOffsetDays = 365;
    public const int MinWindowDays = 1;
    public const int MaxWindowDays = 60;
    public const float MaxMagnitude = 0.25f;
    public const int MinDurationDays = 1;
    public const int MaxDurationDays = 60;
    /// <summary>The momentum service clamps to the same cap; the chronicle never redefines it.</summary>
    public const int MaxMomentumUnits = MomentumAwardService.MaxScriptedUnits;

    internal const string TargetAttacker = "attacker";
    internal const string TargetDefender = "defender";
    internal const string TargetFree = "free";
    internal const string TargetEvil = "evil";

    private static readonly string[] NarrativeKeys = { "id", "kind", "anchor", "anchorEvent", "requiresOutcome", "offsetDays", "title", "image", "outcomes" };
    private static readonly string[] SiegeKeys =
    {
        "id", "kind", "anchor", "anchorEvent", "requiresOutcome", "offsetDays", "title", "image", "outcomes",
        "windowDays", "target", "attackers", "defender", "openText", "lapsedText", "story",
    };

    private static readonly string[] StoryKeys = { "mission", "fate", "fateText" };

    private static readonly string[] OutcomeKeys = { "text", "effects", "momentum" };
    private static readonly string[] EffectKeys = { "target", "kind", "magnitude", "durationDays" };
    private static readonly string[] MomentumKeys = { "side", "units" };

    public static IReadOnlyList<ChronicleEventDefinition> Parse(string? json, IList<string> warnings)
    {
        var result = new List<ChronicleEventDefinition>();
        if (string.IsNullOrWhiteSpace(json))
        {
            warnings.Add("the file is empty, so no chronicle event loads");
            return result;
        }

        JToken root;
        try
        {
            using (var reader = new JsonTextReader(new StringReader(json!)) { DateParseHandling = DateParseHandling.None })
                root = JToken.Load(reader);
        }
        catch (JsonException ex)
        {
            warnings.Add("the file is not readable JSON, so no chronicle event loads: " + ex.Message);
            return result;
        }

        var version = root is JObject o ? o["schemaVersion"] : null;
        if (!(version is JValue jv && jv.Value is long v && v == SchemaVersion))
        {
            warnings.Add($"schemaVersion must be {SchemaVersion}, so no chronicle event loads");
            return result;
        }

        if (!(root["events"] is JArray events))
        {
            warnings.Add("the file has no events array, so no chronicle event loads");
            return result;
        }

        if (events.Count > MaxEvents)
            warnings.Add($"the file lists {events.Count} events; at most {MaxEvents} load, the rest are skipped");

        var byId = new Dictionary<string, ChronicleEventDefinition>(StringComparer.Ordinal);
        var reachesFullWar = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var i = 0; i < events.Count && i < MaxEvents; i++)
        {
            var definition = ReadEvent(events[i], i, byId, reachesFullWar, warnings);
            if (definition == null)
                continue;

            byId[definition.Id] = definition;
            result.Add(definition);
        }

        return result;
    }

    private static ChronicleEventDefinition? ReadEvent(
        JToken token, int index, Dictionary<string, ChronicleEventDefinition> earlier,
        Dictionary<string, bool> reachesFullWar, IList<string> warnings)
    {
        if (!(token is JObject e))
        {
            warnings.Add($"event {index} skipped: it is not an object");
            return null;
        }

        var id = Text(e["id"]);
        if (string.IsNullOrWhiteSpace(id))
        {
            warnings.Add($"event {index} skipped: its id is missing or empty");
            return null;
        }

        ChronicleEventDefinition? Skip(string why)
        {
            warnings.Add($"event '{id}' skipped: {why}");
            return null;
        }

        if (earlier.ContainsKey(id!))
            return Skip("a duplicate id");

        if (!EnumNames.TryParse<ChronicleEventKind>(Text(e["kind"]), out var kind))
            return Skip($"kind '{Text(e["kind"])}' is not Narrative or Siege");

        if (!EnumNames.TryParse<ChronicleAnchor>(Text(e["anchor"]), out var anchor))
            return Skip($"anchor '{Text(e["anchor"])}' is not CampaignStart, Phase1, Phase2 or Event");

        WarnUnknownKeys(e, kind == ChronicleEventKind.Siege ? SiegeKeys : NarrativeKeys, $"event '{id}'", warnings);

        var definition = new ChronicleEventDefinition { Id = id!, Kind = kind, Anchor = anchor };

        var anchorEvent = Text(e["anchorEvent"]);
        ChronicleEventDefinition? anchorDefinition = null;
        if (anchor == ChronicleAnchor.Event)
        {
            if (string.IsNullOrWhiteSpace(anchorEvent))
                return Skip("an Event anchor needs an anchorEvent");
            if (!earlier.TryGetValue(anchorEvent!, out anchorDefinition))
                return Skip($"anchorEvent '{anchorEvent}' must name an earlier event that loaded");
            definition.AnchorEventId = anchorEvent;
        }
        else if (!string.IsNullOrWhiteSpace(anchorEvent))
        {
            return Skip("anchorEvent is set but the anchor is not Event");
        }

        var requiresToken = e["requiresOutcome"];
        if (requiresToken != null && requiresToken.Type != JTokenType.Null && requiresToken.Type != JTokenType.String)
            return Skip("requiresOutcome must be an outcome name in quotes");

        var requires = Text(requiresToken);
        if (!string.IsNullOrWhiteSpace(requires))
        {
            if (anchor != ChronicleAnchor.Event)
                return Skip("requiresOutcome needs an Event anchor");
            if (!EnumNames.TryParse<ChronicleEventState>(requires, out var required) || !Reachable(anchorDefinition!.Kind, required))
                return Skip($"requiresOutcome '{requires}' is not an outcome the anchor event can reach");
            definition.RequiresOutcome = required;
        }

        if (!TryInt(e["offsetDays"], 0, MaxOffsetDays, out var offset))
            return Skip($"offsetDays must be a whole number from 0 to {MaxOffsetDays}");
        definition.OffsetDays = offset;

        definition.Title = Text(e["title"]) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(definition.Title))
            return Skip("it has no title");
        var image = OptionalText(e, "image", $"event '{id}'", warnings);
        definition.Image = string.IsNullOrWhiteSpace(image) ? null : image;

        var fullWar = anchor == ChronicleAnchor.Phase2
                      || (anchor == ChronicleAnchor.Event && reachesFullWar.TryGetValue(anchorEvent!, out var parent) && parent);

        var outcomes = e["outcomes"] as JObject;
        if (outcomes == null && e["outcomes"] != null && e["outcomes"]!.Type != JTokenType.Null)
            warnings.Add($"event '{id}': outcomes is not an object, so it is ignored");
        if (kind == ChronicleEventKind.Siege)
        {
            if (!ReadSiege(e, definition, fullWar, outcomes, warnings, why => Skip(why)))
                return null;
        }
        else
        {
            var occurred = outcomes?["occurred"];
            if (occurred == null)
                return Skip("a narrative event needs an occurred outcome");
            definition.Occurred = ReadOutcome(occurred, definition, "occurred", warnings);
            WarnUnreached(outcomes!, definition, warnings, "occurred");
        }

        // A siege that loaded had fullWar true (ReadSiege refuses one without it).
        reachesFullWar[definition.Id] = fullWar;
        return definition;
    }

    private static bool ReadSiege(
        JObject e, ChronicleEventDefinition definition, bool fullWar, JObject? outcomes,
        IList<string> warnings, Action<string> skip)
    {
        if (!fullWar)
        {
            skip("a siege must open at Phase 2 or later (anchor Phase2, or an Event chain that reaches it)");
            return false;
        }

        if (!TryInt(e["windowDays"], MinWindowDays, MaxWindowDays, out var window))
        {
            skip($"windowDays must be a whole number from {MinWindowDays} to {MaxWindowDays}");
            return false;
        }

        definition.WindowDays = window;

        definition.TargetSettlementId = Text(e["target"]);
        if (string.IsNullOrWhiteSpace(definition.TargetSettlementId))
        {
            skip("a siege needs a target settlement");
            return false;
        }

        definition.Defender = Text(e["defender"]);
        if (string.IsNullOrWhiteSpace(definition.Defender))
        {
            skip("a siege needs a defender kingdom");
            return false;
        }

        var attackers = new List<string>();
        if (e["attackers"] is JArray list)
        {
            foreach (var item in list)
            {
                var attacker = Text(item);
                if (!string.IsNullOrWhiteSpace(attacker) && !attackers.Contains(attacker!))
                    attackers.Add(attacker!);
            }
        }

        if (attackers.Count == 0)
        {
            skip("a siege needs at least one kingdom in attackers");
            return false;
        }

        if (attackers.Contains(definition.Defender!))
        {
            skip($"the defender '{definition.Defender}' is also listed among the attackers");
            return false;
        }

        definition.Attackers = attackers;
        definition.OpenText = OptionalText(e, "openText", $"event '{definition.Id}'", warnings);
        definition.LapsedText = OptionalText(e, "lapsedText", $"event '{definition.Id}'", warnings);
        if (e["story"] != null && e["story"]!.Type != JTokenType.Null)
            definition.Story = ReadStory(e["story"]!, definition.Id, warnings);
        if (outcomes != null)
        {
            if (outcomes["held"] != null)
                definition.Held = ReadOutcome(outcomes["held"]!, definition, "held", warnings);
            if (outcomes["fell"] != null)
                definition.Fell = ReadOutcome(outcomes["fell"]!, definition, "fell", warnings);
            WarnUnreached(outcomes, definition, warnings, "held", "fell");
        }

        return true;
    }

    /// <summary>
    /// A siege's optional story offer (#1105 slice 4). No mission id drops the story and keeps the event; a fate other than Held or Lapsed
    /// falls back to Held with a warning. Whether the id names a loaded story mission is Story Missions' check, at offer time and in
    /// <c>ShippedStoryOffersTests</c>, because this file loads without that catalog.
    /// </summary>
    private static ChronicleStoryDefinition? ReadStory(JToken token, string eventId, IList<string> warnings)
    {
        var where = $"event '{eventId}': story";
        if (!(token is JObject o))
        {
            warnings.Add($"{where} is not an object, so the event offers no story mission");
            return null;
        }

        WarnUnknownKeys(o, StoryKeys, where, warnings);
        var mission = OptionalText(o, "mission", where, warnings);
        if (string.IsNullOrWhiteSpace(mission))
        {
            warnings.Add($"{where} names no mission, so the event offers no story mission");
            return null;
        }

        var story = new ChronicleStoryDefinition { MissionId = mission!, FateText = OptionalText(o, "fateText", where, warnings) };
        if (o["fate"] != null && o["fate"]!.Type != JTokenType.Null)
        {
            // Read as requiresOutcome is read (EnumNames.TryParse: trimmed, any case).
            if (EnumNames.TryParse<ChronicleEventState>(Text(o["fate"]), out var fate) && fate is ChronicleEventState.Held or ChronicleEventState.Lapsed)
                story.Fate = fate;
            else
                warnings.Add($"{where}: fate '{o["fate"]}' is not Held or Lapsed, so it is Held");
        }

        return story;
    }

    private static void WarnUnreached(JObject outcomes, ChronicleEventDefinition definition, IList<string> warnings, params string[] known)
    {
        foreach (var property in outcomes.Properties())
        {
            if (Array.IndexOf(known, property.Name) < 0)
                warnings.Add($"event '{definition.Id}': outcome '{property.Name}' is ignored; a {definition.Kind} event resolves only as {string.Join(" or ", known)}");
        }
    }

    private static ChronicleOutcomeDefinition ReadOutcome(JToken token, ChronicleEventDefinition definition, string name, IList<string> warnings)
    {
        var outcome = new ChronicleOutcomeDefinition();
        if (!(token is JObject o))
        {
            warnings.Add($"event '{definition.Id}': outcome '{name}' is not an object, so it pays nothing");
            return outcome;
        }

        var where = $"event '{definition.Id}': outcome '{name}'";
        WarnUnknownKeys(o, OutcomeKeys, where, warnings);
        outcome.Text = OptionalText(o, "text", where, warnings);
        var effects = new List<ChronicleEffectDefinition>();
        if (o["effects"] != null && o["effects"]!.Type != JTokenType.Null && !(o["effects"] is JArray))
            warnings.Add($"{where}: effects is not an array, so it pays no effect");
        if (o["effects"] is JArray rows)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] is JObject effectRow)
                    WarnUnknownKeys(effectRow, EffectKeys, $"{where} effect {i}", warnings);
                var effect = ReadEffect(rows[i], definition, out var problem);
                if (effect != null)
                    effects.Add(effect);
                else
                    warnings.Add($"event '{definition.Id}': {name} effect {i} skipped: {problem}");
            }
        }

        outcome.Effects = effects;
        if (o["momentum"] != null)
            outcome.Momentum = ReadMomentum(o["momentum"]!, definition, name, warnings);
        return outcome;
    }

    private static ChronicleEffectDefinition? ReadEffect(JToken token, ChronicleEventDefinition definition, out string problem)
    {
        problem = string.Empty;
        if (!(token is JObject row))
        {
            problem = "it is not an object";
            return null;
        }

        var target = Text(row["target"]);
        if (string.IsNullOrWhiteSpace(target))
        {
            problem = "its target is missing or empty";
            return null;
        }

        if (!definition.IsSiege && (target == TargetAttacker || target == TargetDefender))
        {
            problem = $"target '{target}' exists only on a siege";
            return null;
        }

        if (!EnumNames.TryParse<WarEffectKind>(Text(row["kind"]), out var kind))
        {
            problem = $"kind '{Text(row["kind"])}' is not VolunteerRate or PrisonerEscape";
            return null;
        }

        // Finite first: NaN fails every comparison, so the range test alone would let it through.
        if (!TryNumber(row["magnitude"], out var magnitude) || !FiniteFloatValidator.IsFinite(magnitude)
            || magnitude == 0f || !FiniteFloatValidator.IsFiniteInRange(magnitude, -MaxMagnitude, MaxMagnitude))
        {
            problem = $"magnitude must be a finite, non-zero number within -{MaxMagnitude} to {MaxMagnitude}";
            return null;
        }

        if (!TryInt(row["durationDays"], MinDurationDays, MaxDurationDays, out var days))
        {
            problem = $"durationDays must be a whole number from {MinDurationDays} to {MaxDurationDays}";
            return null;
        }

        return new ChronicleEffectDefinition { Target = target!, Kind = kind, Magnitude = magnitude, DurationDays = days };
    }

    private static ChronicleMomentumAward? ReadMomentum(JToken token, ChronicleEventDefinition definition, string name, IList<string> warnings)
    {
        // A non-object would throw on the indexer below and take the whole file down with it.
        if (!(token is JObject m))
        {
            warnings.Add($"event '{definition.Id}': {name} momentum dropped: it is not an object");
            return null;
        }

        WarnUnknownKeys(m, MomentumKeys, $"event '{definition.Id}': {name} momentum", warnings);
        var side = Text(m["side"]);
        MomentumSide parsed;
        if (side == TargetFree)
            parsed = MomentumSide.Free;
        else if (side == TargetEvil)
            parsed = MomentumSide.Evil;
        else
        {
            warnings.Add($"event '{definition.Id}': {name} momentum dropped: side '{side}' is not free or evil");
            return null;
        }

        if (!TryInt(m["units"], 1, MaxMomentumUnits, out var units))
        {
            warnings.Add($"event '{definition.Id}': {name} momentum dropped: units must be a whole number from 1 to {MaxMomentumUnits}");
            return null;
        }

        return new ChronicleMomentumAward { Side = parsed, Units = units };
    }

    /// <summary>A narrative resolves only as Occurred; a siege as Held, Fell or Lapsed.</summary>
    private static bool Reachable(ChronicleEventKind anchorKind, ChronicleEventState state) =>
        anchorKind == ChronicleEventKind.Narrative
            ? state == ChronicleEventState.Occurred
            : state == ChronicleEventState.Held || state == ChronicleEventState.Fell || state == ChronicleEventState.Lapsed;

    private static string? Text(JToken? token) =>
        token is JValue v && v.Type == JTokenType.String ? (string?)v.Value : null;

    /// <summary>Warns on every key the level does not know; a key starting with <c>_</c> is an author's note.</summary>
    private static void WarnUnknownKeys(JObject o, string[] known, string where, IList<string> warnings)
    {
        foreach (var property in o.Properties())
        {
            if (!property.Name.StartsWith("_", StringComparison.Ordinal) && Array.IndexOf(known, property.Name) < 0)
                warnings.Add($"{where}: '{property.Name}' is not a known key, so it is ignored");
        }
    }

    /// <summary>A text key's string; a present value of another type warns and reads as absent.</summary>
    private static string? OptionalText(JObject o, string key, string where, IList<string> warnings)
    {
        var token = o[key];
        if (token != null && token.Type != JTokenType.Null && token.Type != JTokenType.String)
            warnings.Add($"{where}: {key} is not a string, so it is ignored");
        return Text(token);
    }

    private static bool TryInt(JToken? token, int min, int max, out int value)
    {
        value = 0;
        if (token == null || token.Type != JTokenType.Integer || !(((JValue)token).Value is long raw) || raw < min || raw > max)
            return false;
        value = (int)raw;
        return true;
    }

    private static bool TryNumber(JToken? token, out float value)
    {
        value = float.NaN;
        if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            return false;
        try
        {
            value = (float)Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }
}
