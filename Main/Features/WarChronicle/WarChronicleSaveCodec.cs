using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Validation;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// The War Chronicle payload as JSON: <c>{"v":1,"effects":[{"s","k","kind","m","end"}],"baselines":{},
/// "rally":{"tiers":{}},"chronicle":[]}</c>. Pure. Parsing is forgiving by
/// section: a bad section resets alone with a warning, a bad row inside a section is skipped alone, and
/// nothing here throws on save content. Effect kinds are saved by name; an unknown name (a kind from a
/// newer build) is skipped. The chronicle array is carried as parsed JSON, dates left as text, and is
/// never interpreted here.
/// </summary>
internal static class WarChronicleSaveCodec
{
    internal const int Version = 1;
    internal const string EmptyArray = "[]";

    internal static string Serialize(WarChroniclePayload payload)
    {
        var effects = new JArray();
        foreach (var effect in payload.Effects)
        {
            effects.Add(new JObject
            {
                ["s"] = effect.SourceId,
                ["k"] = effect.KingdomId,
                ["kind"] = effect.Kind.ToString(),
                ["m"] = effect.Magnitude,
                ["end"] = effect.EndTimeHours,
            });
        }

        var root = new JObject
        {
            ["v"] = Version,
            ["effects"] = effects,
            ["baselines"] = IntMap(payload.Baselines),
            ["rally"] = new JObject { ["tiers"] = IntMap(payload.RallyTiers) },
            ["chronicle"] = ChronicleToken(payload.ChronicleJson),
        };
        return root.ToString(Formatting.None);
    }

    /// <summary>
    /// Null, empty or whitespace is an empty payload, not a fault: a corrupt or out-of-range record (a save
    /// from before this feature never reaches SyncData, CampaignBehaviorDataStore.LoadBehaviorData).
    /// </summary>
    internal static WarChroniclePayload Parse(string? json)
    {
        var payload = new WarChroniclePayload();
        if (string.IsNullOrWhiteSpace(json))
            return payload;

        JToken token;
        try
        {
            token = Load(json!);
        }
        catch (JsonException ex)
        {
            payload.Warnings.Add("the save payload is not readable JSON, so every section is reset: " + ex.Message);
            return payload;
        }

        if (!(token is JObject root))
        {
            payload.Warnings.Add("the save payload is not a JSON object, so every section is reset");
            return payload;
        }

        // A pattern, never JToken's explicit long conversion: that throws OverflowException on an integer
        // wider than 64 bits (a BigInteger value), and only JsonException is caught above.
        if (!(root["v"] is JValue { Value: long version } && version == Version))
        {
            payload.Warnings.Add("the save payload has an unknown version, so every section is reset");
            return payload;
        }

        ReadEffects(root, payload);
        ReadBaselines(root, payload);
        ReadRally(root, payload);
        ReadChronicle(root, payload);
        return payload;
    }

    private static JToken Load(string json)
    {
        // Dates stay text: the chronicle section must come back as it went in.
        using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
        {
            return JToken.Load(reader);
        }
    }

    private static JObject IntMap(IReadOnlyDictionary<string, int>? map)
    {
        var result = new JObject();
        if (map == null)
            return result;
        foreach (var pair in map)
            result[pair.Key] = pair.Value;
        return result;
    }

    private static JToken ChronicleToken(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JArray();
        try
        {
            return Load(json!) as JArray ?? new JArray();
        }
        catch (JsonException)
        {
            return new JArray();
        }
    }

    private static void ReadEffects(JObject root, WarChroniclePayload payload)
    {
        var section = root["effects"];
        if (section == null)
            return;
        if (!(section is JArray rows))
        {
            payload.Warnings.Add("the effects section is not an array, so it is reset");
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (TryReadEffect(rows[i], out var effect, out var problem))
                payload.Effects.Add(effect!);
            else
                payload.Warnings.Add($"effects row {i} skipped: {problem}");
        }
    }

    private static bool TryReadEffect(JToken row, out WarEffect? effect, out string problem)
    {
        effect = null;
        problem = string.Empty;
        if (!(row is JObject o))
        {
            problem = "it is not an object";
            return false;
        }

        if (!TryString(o["s"], out var source) || !TryString(o["k"], out var kingdom))
        {
            problem = "its source or kingdom id is missing or empty";
            return false;
        }

        var kindName = o["kind"] is JValue kv && kv.Type == JTokenType.String ? (string?)kv.Value : null;
        if (string.IsNullOrEmpty(kindName) || !Enum.IsDefined(typeof(WarEffectKind), kindName))
        {
            problem = $"the kind '{kindName}' is not one this build knows";
            return false;
        }

        if (!TryNumber(o["m"], out var magnitude) || !FiniteFloatValidator.IsFinite((float)magnitude))
        {
            problem = "its magnitude is missing or not a finite number";
            return false;
        }

        if (!TryNumber(o["end"], out var end))
        {
            problem = "its end time is missing or not a finite number";
            return false;
        }

        effect = new WarEffect(source, kingdom, (WarEffectKind)Enum.Parse(typeof(WarEffectKind), kindName), (float)magnitude, end);
        return true;
    }

    private static void ReadBaselines(JObject root, WarChroniclePayload payload)
    {
        var section = root["baselines"];
        if (section == null)
            return;
        if (!(section is JObject map))
        {
            payload.Warnings.Add("the baselines section is not an object, so it is reset");
            return;
        }

        foreach (var pair in ReadIntMap(map, "baselines", 0, int.MaxValue, payload))
            payload.Baselines[pair.Key] = pair.Value;
    }

    private static void ReadRally(JObject root, WarChroniclePayload payload)
    {
        var section = root["rally"];
        if (section == null)
            return;
        if (!(section is JObject rally))
        {
            payload.Warnings.Add("the rally section is not an object, so it is reset");
            return;
        }

        var tiers = rally["tiers"];
        if (tiers == null)
            return;
        if (!(tiers is JObject map))
        {
            payload.Warnings.Add("the rally tiers are not an object, so they are reset");
            return;
        }

        foreach (var pair in ReadIntMap(map, "rally tier", 0, 2, payload))
            payload.RallyTiers[pair.Key] = pair.Value;
    }

    private static void ReadChronicle(JObject root, WarChroniclePayload payload)
    {
        var section = root["chronicle"];
        if (section == null)
            return;
        if (section is JArray array)
            payload.ChronicleJson = array.ToString(Formatting.None);
        else
            payload.Warnings.Add("the chronicle section is not an array, so it is reset");
    }

    private static Dictionary<string, int> ReadIntMap(JObject map, string what, int min, int max, WarChroniclePayload payload)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in map.Properties())
        {
            var value = property.Value;
            if (value.Type == JTokenType.Integer && TryInt((JValue)value, min, max, out var number))
                result[property.Name] = number;
            else
                payload.Warnings.Add($"{what} '{property.Name}' skipped: {value} is not an integer from {min} to {max}");
        }

        return result;
    }

    private static bool TryInt(JValue value, int min, int max, out int number)
    {
        number = 0;
        if (!(value.Value is long raw) || raw < min || raw > max)
            return false;
        number = (int)raw;
        return true;
    }

    private static bool TryString(JToken? token, out string value)
    {
        value = string.Empty;
        if (!(token is JValue v) || v.Type != JTokenType.String || !(v.Value is string text) || string.IsNullOrWhiteSpace(text))
            return false;
        value = text;
        return true;
    }

    private static bool TryNumber(JToken? token, out double value)
    {
        value = 0d;
        if (!(token is JValue v) || (v.Type != JTokenType.Integer && v.Type != JTokenType.Float))
            return false;
        try
        {
            value = Convert.ToDouble(v.Value, CultureInfo.InvariantCulture);
        }
        catch (InvalidCastException)
        {
            // An integer wider than 64 bits arrives as a BigInteger, which does not convert.
            return false;
        }

        return FiniteFloatValidator.IsFinite(value);
    }
}
