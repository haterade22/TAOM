using System;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.SiegeForces;

public interface ISiegeForcesConfigProvider
{
    /// <summary>
    /// True when oversized creature troops (<c>OversizedCreatureRaces</c>) start unticked on the troop selection
    /// screen. The player can still tick them. This is JSON and not MCM so that a later flip reaches installs that
    /// already hold a saved MCM file.
    /// </summary>
    bool StartOversizedUnticked { get; }
}

/// <summary>
/// Validating loader for <c>siege/siege_forces.json</c>, a single switch: <c>{ "startOversizedUnticked": true }</c>
/// (csharp-architecture.md "Config Providers MUST Validate"). Every way the file can be wrong reverts to true with one
/// warning: a missing file, text that is not JSON, JSON that is not an object, a missing key (a typo'd key is a missing
/// key, so the warning names the key to type) and a value that is not a JSON boolean. A string or a number is refused
/// on purpose, because a parser would coerce <c>"false"</c> and <c>0</c> to false and flip the switch on a hand edit.
/// The load never throws: a faulted <see cref="Lazy{T}"/> rethrows its exception forever. Read once per process, so an
/// edit needs a restart of the game, not a new campaign or a reload.
/// </summary>
public sealed class SiegeForcesConfigProvider : ISiegeForcesConfigProvider
{
    private const string Tag = "SiegeForcesConfigProvider";
    private const string Key = "startOversizedUnticked";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<bool> _startOversizedUnticked;

    public SiegeForcesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _startOversizedUnticked = new Lazy<bool>(Load);
    }

    public bool StartOversizedUnticked => _startOversizedUnticked.Value;

    private bool Load()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, "siege", "siege_forces.json");
            if (!File.Exists(path))
                return Fallback($"siege_forces.json not found at {path}");

            JToken root;
            try
            {
                root = JToken.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                return Fallback($"siege_forces.json could not be read ({ex.GetType().Name}: {ex.Message})");
            }

            if (!(root is JObject json))
                return Fallback("siege_forces.json is not a JSON object");

            if (!json.TryGetValue(Key, out var token))
                return Fallback($"siege_forces.json has no '{Key}' key");

            if (token.Type != JTokenType.Boolean)
                return Fallback($"'{Key}' in siege_forces.json is not a boolean (it is {token.Type})");

            var value = token.Value<bool>();
            _logger.LogInfo($"{Tag}: loaded siege_forces.json, {Key}={value}");
            return value;
        }
        catch (Exception ex)
        {
            return Fallback($"siege_forces.json could not be read ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private bool Fallback(string reason)
    {
        _logger.LogWarning($"{Tag}: {reason}; oversized troops start unticked (the default)");
        return true;
    }
}
