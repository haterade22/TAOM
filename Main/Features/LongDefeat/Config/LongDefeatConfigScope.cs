using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The reading rules shared by the three long_defeat config files (csharp-architecture.md "Config Providers MUST Validate"). Never throws.
/// </summary>
internal sealed class LongDefeatConfigScope
{
    private const string Tag = "LongDefeatConfigCatalog";

    private readonly IModLogger _logger;

    public LongDefeatConfigScope(IModLogger logger, string fileName)
    {
        _logger = logger;
        FileName = fileName;
    }

    public string FileName { get; }

    /// <summary>How many warnings this file has cost so far; the summary line in <see cref="Loaded"/> reads it.</summary>
    public int Warnings { get; private set; }

    public void Warn(string message)
    {
        Warnings++;
        _logger.LogWarning($"{Tag}: {FileName}: {message}");
    }

    /// <summary>
    /// The closing line of a file that opened: info when it read clean, otherwise one warning (config-providers.md item 6) so a
    /// reader of the log knows a value reverted and where to look. It does not count as a warning of its own.
    /// </summary>
    public void Loaded(string what)
    {
        if (Warnings == 0)
            Info($"loaded, {what}");
        else
            _logger.LogWarning($"{Tag}: {FileName}: loaded, {what}, but {Warnings} value(s) reverted or were skipped; see the warnings above");
    }

    public void Info(string message) => _logger.LogInfo($"{Tag}: {FileName}: {message}");

    /// <summary>The root object of the file, or null after one warning when the text is missing, not JSON or not an object.</summary>
    public JObject? OpenRoot(string? text, string missingNote)
    {
        if (text == null)
        {
            Warn(missingNote);
            return null;
        }

        try
        {
            var root = JToken.Parse(text);
            if (root is JObject json)
                return json;
            Warn("is not a JSON object; nothing loaded from it");
            return null;
        }
        catch (Exception ex)
        {
            Warn($"could not be read ({ex.GetType().Name}: {ex.Message}); nothing loaded from it");
            return null;
        }
    }

    /// <summary>A named child object: null when absent, and null after one warning when present but not an object.</summary>
    public JObject? Group(JObject? parent, string key, string path)
    {
        if (parent == null || !parent.TryGetValue(key, out var token))
            return null;
        if (token is JObject group)
            return group;
        Warn($"'{Join(path, key)}' is not an object; using the defaults for it");
        return null;
    }

    public float Float(JObject? o, string path, string key, float fallback, float min, float max)
    {
        if (o == null || !o.TryGetValue(key, out var token))
            return fallback;
        if (TryFloat(token, min, max, out var value, out var why))
            return value;
        Warn($"'{Join(path, key)}' {why}; using the default {Format(fallback)}");
        return fallback;
    }

    public int Int(JObject? o, string path, string key, int fallback, int min, int max)
    {
        if (o == null || !o.TryGetValue(key, out var token))
            return fallback;
        if (TryInt(token, min, max, out var value, out var why))
            return value;
        Warn($"'{Join(path, key)}' {why}; using the default {fallback.ToString(CultureInfo.InvariantCulture)}");
        return fallback;
    }

    /// <summary>A required or optional text value: null when absent, not a string or blank.</summary>
    public static string? Text(JToken? token)
    {
        if (token == null || token.Type != JTokenType.String)
            return null;
        var text = ((string?)token)?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>The formation class of a row: only the two the engine fills, so a typo skips the row rather than defaulting.</summary>
    public static bool TrySlot(JToken? token, out FormationSlot slot, out string why)
    {
        slot = FormationSlot.Infantry;
        var text = Text(token);
        if (text != null && text.Equals("Infantry", StringComparison.Ordinal))
        {
            why = "";
            return true;
        }

        if (text != null && text.Equals("Ranged", StringComparison.Ordinal))
        {
            slot = FormationSlot.Ranged;
            why = "";
            return true;
        }

        why = $"is '{text ?? token?.ToString() ?? "missing"}', not 'Infantry' or 'Ranged'";
        return false;
    }

    public static bool TryFloat(JToken? token, float min, float max, out float value, out string why)
    {
        value = 0f;
        if (token == null)
        {
            why = "is missing";
            return false;
        }

        if (!IsNumber(token))
        {
            why = $"is not a number (it is {token.Type})";
            return false;
        }

        if (IsBeyondLong(token))
        {
            why = "is out of range";
            return false;
        }

        var d = token.Value<double>();
        if (!FiniteFloatValidator.IsFinite(d))
        {
            why = $"is not finite ({Format(d)})";
            return false;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(d, min, max))
        {
            why = $"is {Format(d)}, not between {Format(min)} and {Format(max)}";
            return false;
        }

        value = (float)d;
        why = "";
        return true;
    }

    public static bool TryInt(JToken? token, int min, int max, out int value, out string why)
    {
        value = 0;
        if (token == null)
        {
            why = "is missing";
            return false;
        }

        if (token.Type != JTokenType.Integer)
        {
            why = token.Type == JTokenType.Float ? "must be a whole number" : $"is not a number (it is {token.Type})";
            return false;
        }

        if (IsBeyondLong(token))
        {
            why = $"is out of range, not between {min} and {max}";
            return false;
        }

        var d = token.Value<double>();
        if (!FiniteFloatValidator.IsFiniteInRange(d, min, max))
        {
            why = $"is {Format(d)}, not between {min} and {max}";
            return false;
        }

        value = (int)d;
        why = "";
        return true;
    }

    public static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Newtonsoft reads an integer too large for a long as a BigInteger, and <c>Value&lt;double&gt;</c> throws on it.</summary>
    private static bool IsBeyondLong(JToken token) => token is JValue v && v.Value is System.Numerics.BigInteger;

    private static bool IsNumber(JToken token) => token.Type == JTokenType.Float || token.Type == JTokenType.Integer;

    private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;
}
