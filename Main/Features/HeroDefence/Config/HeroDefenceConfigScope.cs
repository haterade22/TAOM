using System;
using Newtonsoft.Json.Linq;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;

namespace TAOM.Features.HeroDefence;

/// <summary>
/// The reading rules of one hero_defence config file. A bad value costs one warning and takes its default (config-providers.md). The number checks are Long Defeat's. Never throws.
/// </summary>
internal sealed class HeroDefenceConfigScope
{
    private const string Tag = "HeroDefenceConfigCatalog";

    private static readonly JsonLoadSettings StrictLoad = new() { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error };

    private readonly IModLogger _logger;

    public HeroDefenceConfigScope(IModLogger logger, string fileName)
    {
        _logger = logger;
        FileName = fileName;
    }

    public string FileName { get; }

    /// <summary>How many warnings this file has cost. The summary line in <see cref="Loaded"/> reads it.</summary>
    public int Warnings { get; private set; }

    public void Warn(string message)
    {
        Warnings++;
        _logger.LogWarning($"{Tag}: {FileName}: {message}");
    }

    /// <summary>The closing line of a file that opened: info when it read clean, otherwise one summary warning. The summary does not count as a warning of its own.</summary>
    public void Loaded(string what)
    {
        if (Warnings == 0)
            _logger.LogInfo($"{Tag}: {FileName}: loaded, {what}");
        else
            _logger.LogWarning($"{Tag}: {FileName}: loaded, {what}, but {Warnings} value(s) reverted or were skipped; see the warnings above");
    }

    /// <summary>
    /// The root object of the file, or null after one warning when the text is missing, is not JSON or is not an object. A repeated
    /// key fails the parse, because the default parse keeps the last value and would hide the first.
    /// </summary>
    public JObject? OpenRoot(string? text, string missingNote)
    {
        if (text == null)
        {
            Warn(missingNote);
            return null;
        }

        try
        {
            if (JToken.Parse(text, StrictLoad) is JObject root)
                return root;
            Warn("is not a JSON object; nothing loaded from it");
            return null;
        }
        catch (Exception ex)
        {
            Warn($"could not be read ({ex.GetType().Name}: {ex.Message}); nothing loaded from it");
            return null;
        }
    }

    /// <summary>A named child object: null when absent, and null after one warning when it is present but is not an object.</summary>
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
        if (LongDefeatConfigScope.TryFloat(token, min, max, out var value, out var why))
            return value;
        Warn($"'{Join(path, key)}' {why}; using the default {LongDefeatConfigScope.Format(fallback)}");
        return fallback;
    }

    public int Int(JObject? o, string path, string key, int fallback, int min, int max)
    {
        if (o == null || !o.TryGetValue(key, out var token))
            return fallback;
        if (LongDefeatConfigScope.TryInt(token, min, max, out var value, out var why))
            return value;
        Warn($"'{Join(path, key)}' {why}; using the default {LongDefeatConfigScope.Format(fallback)}");
        return fallback;
    }

    private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;
}
