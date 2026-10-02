using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.FactionUI.CharacterCreation;

/// <summary>
/// Kysaro's camera tuning for the themed character-creation screens (#704), from
/// <c>ModuleData/FactionUI/facegen_camera.json</c>: one object per screen (<c>FaceGen</c>,
/// <c>Narrative</c>, <c>Review</c>, <c>Options</c>) with <c>x_offset</c>, <c>distance_offset</c> and
/// <c>fov_offset</c>. Read once per process, so an edit applies after a restart. Each value must be
/// finite and in range, or that value falls back to 0 with a warning; a key that is no screen or no
/// offset, or a screen that is not an object, is ignored with a warning; any of these ends with one
/// summary warning (TAOM's config-provider rule). Keys starting with <c>_</c> are notes.
/// </summary>
public sealed class FaceGenCameraConfigProvider
{
    public const string FileName = "facegen_camera.json";

    internal const float MaxPositionOffset = 3f;
    internal const float MinFovOffset = -0.5f;
    internal const float MaxFovOffset = 1f;

    private static readonly string[] Screens = { "FaceGen", "Narrative", "Review", "Options" };
    private static readonly string[] Offsets = { "x_offset", "distance_offset", "fov_offset" };

    private readonly IFrontEndResourceAdapter _adapter;
    private readonly FactionUIPaths _paths;
    private readonly IModLogger _logger;
    private Dictionary<string, CameraOffsets>? _offsets;

    public FaceGenCameraConfigProvider(IFrontEndResourceAdapter adapter, FactionUIPaths paths, IModLogger logger)
    {
        _adapter = adapter;
        _paths = paths;
        _logger = logger;
    }

    public CameraOffsets OffsetsFor(string screen) =>
        (_offsets ??= Load()).TryGetValue(screen, out var offsets) ? offsets : CameraOffsets.None;

    private Dictionary<string, CameraOffsets> Load()
    {
        var result = new Dictionary<string, CameraOffsets>(StringComparer.Ordinal);
        var path = Path.Combine(_paths.ConfigDirectory, FileName);
        var text = _adapter.ReadAllText(path);
        if (text == null)
            return result;

        JObject root;
        try
        {
            root = JObject.Parse(text);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning($"[FactionUI] {FileName} is not valid JSON, cameras left at vanilla: {ex.Message}");
            return result;
        }

        // Every value reverted to 0 and every entry ignored counts toward the one summary warning.
        var problems = 0;
        foreach (var property in root.Properties())
        {
            if (!property.Name.StartsWith("_", StringComparison.Ordinal) && Array.IndexOf(Screens, property.Name) < 0)
            {
                problems++;
                _logger.LogWarning($"[FactionUI] {FileName}: \"{property.Name}\" is not a screen ({string.Join(", ", Screens)}); ignored");
            }
        }

        foreach (var screen in Screens)
        {
            var token = root[screen];
            if (token == null)
                continue;
            if (token is not JObject entry)
            {
                problems++;
                _logger.LogWarning($"[FactionUI] {FileName}: \"{screen}\" must be an object of offsets; ignored");
                continue;
            }
            foreach (var key in entry.Properties())
            {
                if (Array.IndexOf(Offsets, key.Name) < 0)
                {
                    problems++;
                    _logger.LogWarning($"[FactionUI] {FileName}: {screen}.{key.Name} is not an offset ({string.Join(", ", Offsets)}); ignored");
                }
            }
            result[screen] = new CameraOffsets(
                Read(entry, screen, "x_offset", -MaxPositionOffset, MaxPositionOffset, ref problems),
                Read(entry, screen, "distance_offset", -MaxPositionOffset, MaxPositionOffset, ref problems),
                Read(entry, screen, "fov_offset", MinFovOffset, MaxFovOffset, ref problems));
        }

        if (problems > 0)
            _logger.LogWarning($"[FactionUI] {FileName}: {problems} {(problems == 1 ? "entry" : "entries")} reverted or ignored, see the warnings above");
        return result;
    }

    private float Read(JObject entry, string screen, string key, float min, float max, ref int problems)
    {
        var token = entry[key];
        if (token == null)
            return 0f;

        if (token.Type is JTokenType.Float or JTokenType.Integer)
        {
            var value = token.Value<float>();
            if (FiniteFloatValidator.IsFiniteInRange(value, min, max))
                return value;
        }

        problems++;
        _logger.LogWarning($"[FactionUI] {FileName}: {screen}.{key} = {token} is not a number in [{min}, {max}]; using 0");
        return 0f;
    }
}
