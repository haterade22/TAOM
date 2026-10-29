using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// Validating loader for <c>siege/staged_siege.json</c> (docs/features/staged-siege.md): the tunables, the reserve shares and
/// the recorded scenes (csharp-architecture.md "Config Providers MUST Validate"). A bad tunable reverts to its default with one
/// warning that names the key; a bad scene is skipped with one warning and never costs a good one its place; a file that is
/// missing, not JSON or not an object leaves no scenes and the default tunables. Every float is checked for NaN and infinity
/// BEFORE its range (every NaN comparison is false), a number must be a JSON number (a string or a boolean is refused because a
/// parser would coerce <c>"3"</c> and <c>true</c>), and an integer tunable must be a JSON integer.
///
/// The load never throws (a faulted <see cref="Lazy{T}"/> rethrows forever) and happens once per process, so an edit needs a
/// restart of the game. The map from scene name to plan is case-insensitive: the engine's scene names are not consistently cased.
/// </summary>
public sealed class StagedSiegeSceneCatalog
{
    private const string Tag = "StagedSiegeSceneCatalog";
    private const string FileName = "staged_siege.json";
    private const double MaxCoordinate = 100000d;

    private static readonly string[] Letters = { "A", "B", "C", "D" };

    private sealed class Loaded
    {
        public StagedSiegeTunables Tunables = StagedSiegeTunables.Default;
        public readonly Dictionary<string, ScenePlan> Scenes = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly IModLogger _logger;
    private readonly Lazy<Loaded> _loaded;

    public StagedSiegeSceneCatalog(IPathService pathService, IModLogger logger)
    {
        _logger = logger;
        _loaded = new Lazy<Loaded>(() => LoadFile(pathService));
    }

    private StagedSiegeSceneCatalog(string? text, IModLogger logger)
    {
        _logger = logger;
        _loaded = new Lazy<Loaded>(() => text == null ? Fallback($"{FileName} not found") : Parse(text));
    }

    /// <summary>For tests: the same validation over text instead of the file. Null text is the missing-file case.</summary>
    internal static StagedSiegeSceneCatalog FromText(string? json, IModLogger logger) => new(json, logger);

    public StagedSiegeTunables Tunables => _loaded.Value.Tunables;

    public int SceneCount => _loaded.Value.Scenes.Count;

    public bool TryGetScene(string sceneName, out ScenePlan scene)
    {
        scene = null!;
        return !string.IsNullOrEmpty(sceneName) && _loaded.Value.Scenes.TryGetValue(sceneName, out scene!);
    }

    private Loaded LoadFile(IPathService pathService)
    {
        try
        {
            var path = Path.Combine(pathService.ModuleDataPath, "siege", FileName);
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : Fallback($"{FileName} not found at {path}");
        }
        catch (Exception ex)
        {
            return Fallback($"{FileName} could not be read ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private Loaded Fallback(string reason)
    {
        _logger.LogWarning($"{Tag}: {reason}; no staged scenes, default tunables");
        return new Loaded();
    }

    private Loaded Parse(string text)
    {
        try
        {
            JToken root;
            try
            {
                root = JToken.Parse(text);
            }
            catch (Exception ex)
            {
                return Fallback($"{FileName} could not be read ({ex.GetType().Name}: {ex.Message})");
            }

            if (!(root is JObject json))
                return Fallback($"{FileName} is not a JSON object");

            var reverted = 0;
            var values = StagedSiegeTunables.DefaultValues();
            for (var i = 0; i < StagedSiegeTunables.Specs.Length; i++)
            {
                var spec = StagedSiegeTunables.Specs[i];
                if (!json.TryGetValue(spec.Key, out var token))
                    continue;
                if (TryReadTunable(token, spec, out var value, out var why))
                {
                    values[i] = value;
                    continue;
                }

                reverted++;
                _logger.LogWarning($"{Tag}: '{spec.Key}' in {FileName} {why}; using the default {Format(spec.Default)}");
            }

            var shares = ReadShares(json, ref reverted);
            var loaded = new Loaded { Tunables = new StagedSiegeTunables(values, shares) };
            ReadScenes(json, loaded);

            if (reverted > 0)
                _logger.LogWarning($"{Tag}: {reverted} value(s) in {FileName} reverted to their defaults");
            _logger.LogInfo($"{Tag}: loaded {FileName}, {loaded.Scenes.Count} scene(s)");
            return loaded;
        }
        catch (Exception ex)
        {
            return Fallback($"{FileName} could not be read ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static bool IsNumber(JToken token) => token.Type == JTokenType.Float || token.Type == JTokenType.Integer;

    private static bool TryReadTunable(JToken token, TunableSpec spec, out double value, out string why)
    {
        value = 0;
        if (!IsNumber(token))
        {
            why = $"is not a number (it is {token.Type})";
            return false;
        }

        if (spec.IsInteger && token.Type != JTokenType.Integer)
        {
            why = "must be a whole number";
            return false;
        }

        value = token.Value<double>();
        if (!FiniteFloatValidator.IsFinite(value))
        {
            why = $"is not finite ({Format(value)})";
            return false;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(value, spec.Min, spec.Max))
        {
            why = $"is {Format(value)}, not between {Format(spec.Min)} and {Format(spec.Max)}";
            return false;
        }

        why = "";
        return true;
    }

    private ReserveShares ReadShares(JObject json, ref int reverted)
    {
        var defaults = ReserveShares.Default;
        if (!json.TryGetValue("reserveShares", out var token))
            return defaults;
        if (!(token is JObject block))
        {
            reverted++;
            _logger.LogWarning($"{Tag}: 'reserveShares' in {FileName} is not an object; using the default shares");
            return defaults;
        }

        return new ReserveShares(
            ReadShareArray(block, "attacker", defaults.Attacker, ref reverted),
            ReadShareArray(block, "defenderPlayerAttacks", defaults.DefenderPlayerAttacks, ref reverted),
            ReadShareArray(block, "defenderPlayerDefends", defaults.DefenderPlayerDefends, ref reverted));
    }

    private float[] ReadShareArray(JObject block, string key, float[] fallback, ref int reverted)
    {
        if (!block.TryGetValue(key, out var token))
            return fallback;

        var why = CheckShares(token, out var shares);
        if (why == null)
            return shares;

        reverted++;
        _logger.LogWarning($"{Tag}: '{key}' in {FileName} {why}; using the default shares");
        return fallback;
    }

    private static string? CheckShares(JToken token, out float[] shares)
    {
        shares = Array.Empty<float>();
        if (!(token is JArray array) || array.Count != 4)
            return "is not an array of exactly 4 numbers";

        var result = new float[4];
        double sum = 0;
        for (var i = 0; i < 4; i++)
        {
            if (!IsNumber(array[i]))
                return $"has a non-number at position {i}";
            var value = array[i].Value<double>();
            if (!FiniteFloatValidator.IsFinite(value))
                return $"has a value that is not finite at position {i}";
            if (!FiniteFloatValidator.IsFiniteInRange(value, 0d, 1d))
                return $"has {Format(value)} at position {i}, not between 0 and 1";
            result[i] = (float)value;
            sum += value;
        }

        if (sum > 1.0001d)
            return $"sums to {Format(sum)}, above 1";
        shares = result;
        return null;
    }

    private void ReadScenes(JObject json, Loaded loaded)
    {
        if (!json.TryGetValue("scenes", out var token))
            return;
        if (!(token is JObject scenes))
        {
            _logger.LogWarning($"{Tag}: 'scenes' in {FileName} is not an object; no staged scenes");
            return;
        }

        foreach (var property in scenes.Properties())
        {
            if (TryReadScene(property.Name, property.Value, loaded.Tunables.CaptureRadius, out var plan, out var why))
                loaded.Scenes[property.Name] = plan;
            else
                _logger.LogWarning($"{Tag}: scene '{property.Name}' skipped ({why})");
        }
    }

    private static bool TryReadScene(string name, JToken token, float defaultRadius, out ScenePlan plan, out string why)
    {
        plan = null!;
        why = "";
        if (string.IsNullOrWhiteSpace(name))
        {
            why = "its name is empty";
            return false;
        }

        if (!(token is JObject scene))
        {
            why = "it is not an object";
            return false;
        }

        if (!(scene["points"] is JObject points))
        {
            why = "it has no 'points' object";
            return false;
        }

        var zones = new StageZone[4];
        for (var i = 0; i < 4; i++)
        {
            if (!(points[Letters[i]] is JObject point))
            {
                why = $"point {Letters[i]} is missing";
                return false;
            }

            if (!TryReadPoint(point, out var center) || !TryReadRadius(point, defaultRadius, out var radius))
            {
                why = $"point {Letters[i]} has a bad coordinate or radius";
                return false;
            }

            zones[i] = new StageZone((StageId)i, center, radius);
        }

        if (!(scene["detachment"] is JObject detachment) || !TryReadFrame(detachment, out var detachmentFrame))
        {
            why = "the detachment frame is missing or bad";
            return false;
        }

        if (!(scene["defenders"] is JObject defenders))
        {
            why = "it has no 'defenders' object";
            return false;
        }

        var frames = new StageFrame[3];
        for (var i = 0; i < 3; i++)
        {
            if (!(defenders[Letters[i + 1]] is JObject frame) || !TryReadFrame(frame, out frames[i]))
            {
                why = $"the defender frame for {Letters[i + 1]} is missing or bad";
                return false;
            }
        }

        plan = new ScenePlan(name, zones, detachmentFrame, frames[0], frames[1], frames[2]);
        return true;
    }

    private static bool TryCoordinate(JObject o, string key, out float value)
    {
        value = 0f;
        if (!o.TryGetValue(key, out var token) || !IsNumber(token))
            return false;
        var d = token.Value<double>();
        if (!FiniteFloatValidator.IsFinite(d) || Math.Abs(d) > MaxCoordinate)
            return false;
        value = (float)d;
        return true;
    }

    private static bool TryReadPoint(JObject o, out StagePoint point)
    {
        point = default;
        if (!TryCoordinate(o, "x", out var x) || !TryCoordinate(o, "y", out var y) || !TryCoordinate(o, "z", out var z))
            return false;
        point = new StagePoint(x, y, z);
        return true;
    }

    private static bool TryReadRadius(JObject o, float defaultRadius, out float radius)
    {
        radius = defaultRadius;
        if (!o.TryGetValue("radius", out var token))
            return true;
        var spec = StagedSiegeTunables.Specs[0];
        if (!IsNumber(token))
            return false;
        var d = token.Value<double>();
        if (!FiniteFloatValidator.IsFiniteInRange(d, spec.Min, spec.Max))
            return false;
        radius = (float)d;
        return true;
    }

    private static bool TryReadFrame(JObject o, out StageFrame frame)
    {
        frame = default;
        if (!TryReadPoint(o, out var point) || !TryCoordinate(o, "fx", out var fx) || !TryCoordinate(o, "fy", out var fy)
            || !StageFacing.TryNormalize(fx, fy, out var facing))
            return false;
        frame = new StageFrame(point, facing);
        return true;
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
