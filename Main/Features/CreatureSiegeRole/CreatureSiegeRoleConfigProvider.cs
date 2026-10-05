using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.CreatureSiegeRole;

/// <summary>
/// Validating loader for <c>siege/creature_siege_role.json</c>, a single number: <c>{ "gateDamageMultiplier": 2.0 }</c>, the
/// factor a creature's melee blow on a castle gate is multiplied by (csharp-architecture.md "Config Providers MUST Validate").
/// Every way the file can be wrong reverts to <see cref="DefaultGateDamageMultiplier"/> with one warning that names the key:
/// a missing file, text that is not JSON, JSON that is not an object, a missing key (a typo'd key is a missing key), a value
/// that is not a JSON number (a string or a boolean is refused on purpose, because a parser would coerce <c>"3"</c> and
/// <c>true</c>), a NaN or an infinity (rejected BEFORE the range check, since every NaN comparison is false), and a finite
/// value outside [<see cref="MinGateDamageMultiplier"/>, <see cref="MaxGateDamageMultiplier"/>]. The range check runs on the
/// double, because 1e300 is finite as a double and an infinity as a float.
///
/// The shipped value is a PROVISIONAL 2.0: the in-game measurement of one troll's blow on a gate sets the final one, and the
/// number is TAOM's own, not copied from another mod. The load never throws (a faulted <see cref="Lazy{T}"/> rethrows its
/// exception forever), and it happens once per process, so an edit needs a restart of the game, not a new campaign or a reload.
/// </summary>
public sealed class CreatureSiegeRoleConfigProvider
{
    /// <summary>The value used when the file is missing or wrong.</summary>
    public const float DefaultGateDamageMultiplier = 2f;

    /// <summary>The lowest accepted multiplier. Below 1 the multiplier would slow a gate break down.</summary>
    public const float MinGateDamageMultiplier = 1f;

    /// <summary>The highest accepted multiplier.</summary>
    public const float MaxGateDamageMultiplier = 10f;

    private const string Tag = "CreatureSiegeRoleConfigProvider";
    private const string Key = "gateDamageMultiplier";
    private const string FileName = "creature_siege_role.json";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<float> _gateDamageMultiplier;

    public CreatureSiegeRoleConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _gateDamageMultiplier = new Lazy<float>(Load);
    }

    /// <summary>The validated multiplier: finite and in [1, 10].</summary>
    public float GateDamageMultiplier => _gateDamageMultiplier.Value;

    private float Load()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, "siege", FileName);
            if (!File.Exists(path))
                return Fallback($"{FileName} not found at {path}");

            JToken root;
            try
            {
                root = JToken.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                return Fallback($"{FileName} could not be read ({ex.GetType().Name}: {ex.Message})");
            }

            if (!(root is JObject json))
                return Fallback($"{FileName} is not a JSON object");

            if (!json.TryGetValue(Key, out var token))
                return Fallback($"{FileName} has no '{Key}' key");

            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                return Fallback($"'{Key}' in {FileName} is not a number (it is {token.Type})");

            var value = token.Value<double>();
            if (!FiniteFloatValidator.IsFinite(value))
                return Fallback($"'{Key}' in {FileName} is not finite ({value.ToString("R", CultureInfo.InvariantCulture)})");

            if (!FiniteFloatValidator.IsFiniteInRange(value, MinGateDamageMultiplier, MaxGateDamageMultiplier))
                return Fallback($"'{Key}' in {FileName} is {value.ToString("R", CultureInfo.InvariantCulture)}, " +
                                $"not between {Format(MinGateDamageMultiplier)} and {Format(MaxGateDamageMultiplier)}");

            var result = (float)value;
            _logger.LogInfo($"{Tag}: loaded {FileName}, {Key}={Format(result)}");
            return result;
        }
        catch (Exception ex)
        {
            return Fallback($"{FileName} could not be read ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private float Fallback(string reason)
    {
        _logger.LogWarning($"{Tag}: {reason}; using the default {Key}={Format(DefaultGateDamageMultiplier)}");
        return DefaultGateDamageMultiplier;
    }

    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);
}
