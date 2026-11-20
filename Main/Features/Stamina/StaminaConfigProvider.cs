using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Features.Stamina;

/// <summary>
/// Validating loader for <c>configs/stamina.json</c> (#1497). A missing file gives the compiled defaults and a
/// warning; a file that does not parse gives the defaults and an error; a value out of its range, or not a number,
/// gives its default and a warning. A band may not be milder than the band before it: if one multiplier breaks that
/// order, that multiplier goes back to its defaults in all three bands. Loaded once per process: an edit needs a game
/// restart.
/// </summary>
public sealed class StaminaConfigProvider
{
    private const float MinSwing = 0.5f;
    private const float MinHandling = 0.5f;
    private const float MinMove = 0.1f;
    // AgentStatCalculateModel clamps AIAttackOnDecideChance to 0.05 at the bottom; a factor below it only hits that floor.
    private const float MinAiAttack = 0.05f;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<StaminaTuning> _tuning;
    private bool _rejected;

    public StaminaConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _tuning = new Lazy<StaminaTuning>(Load);
    }

    public StaminaTuning GetTuning() => _tuning.Value;

    private StaminaTuning Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "configs", "stamina.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"StaminaConfigProvider: stamina.json not found at {path}, using the compiled defaults");
            return StaminaTuning.Default;
        }

        StaminaConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<StaminaConfig>(File.ReadAllText(path)) ?? new StaminaConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"StaminaConfigProvider: failed to parse stamina.json, using the compiled defaults: {ex.Message}");
            return StaminaTuning.Default;
        }

        var tuning = Validate(parsed);
        if (_rejected)
            _logger.LogWarning("StaminaConfigProvider: one or more stamina.json values were rejected and replaced by their defaults; see the warnings above");
        else
            _logger.LogInfo("StaminaConfigProvider: loaded stamina.json");
        return tuning;
    }

    private StaminaTuning Validate(StaminaConfig parsed)
    {
        var winded = parsed.Effects?.Winded;
        var tired = parsed.Effects?.Tired;
        var exhausted = parsed.Effects?.Exhausted;
        var dw = StaminaTuning.DefaultWinded;
        var dt = StaminaTuning.DefaultTired;
        var de = StaminaTuning.DefaultExhausted;

        var swing = Ordered("swing", MinSwing, winded?.Swing, tired?.Swing, exhausted?.Swing, dw.Swing, dt.Swing, de.Swing);
        var handling = Ordered("handling", MinHandling, winded?.Handling, tired?.Handling, exhausted?.Handling, dw.Handling, dt.Handling, de.Handling);
        var move = Ordered("move", MinMove, winded?.Move, tired?.Move, exhausted?.Move, dw.Move, dt.Move, de.Move);
        var aiAttack = Ordered("aiAttack", MinAiAttack, winded?.AiAttack, tired?.AiAttack, exhausted?.AiAttack, dw.AiAttack, dt.AiAttack, de.AiAttack);

        var sweep = InRange(parsed.Tick?.SweepSeconds, StaminaTuning.DefaultSweepSeconds, 0.1f, 1f, "tick.sweepSeconds");
        var minAge = InRange(parsed.Tick?.MinRefreshAgeSeconds, StaminaTuning.DefaultMinRefreshAgeSeconds, 0f, 5f, "tick.minRefreshAgeSeconds");
        var refreshes = parsed.Tick?.MaxRefreshesPerFrame ?? StaminaTuning.DefaultMaxRefreshesPerFrame;
        if (refreshes < 1 || refreshes > 64)
        {
            Reject($"tick.maxRefreshesPerFrame {refreshes} is outside 1 to 64, using {StaminaTuning.DefaultMaxRefreshesPerFrame}");
            refreshes = StaminaTuning.DefaultMaxRefreshesPerFrame;
        }

        return new StaminaTuning(
            new StaminaBandEffects(swing[0], handling[0], move[0], aiAttack[0]),
            new StaminaBandEffects(swing[1], handling[1], move[1], aiAttack[1]),
            new StaminaBandEffects(swing[2], handling[2], move[2], aiAttack[2]),
            sweep, refreshes, minAge);
    }

    // One multiplier across the three bands: each in its range, and never milder in a more tired band.
    private float[] Ordered(string name, float min, float? winded, float? tired, float? exhausted, float dWinded, float dTired, float dExhausted)
    {
        var values = new[]
        {
            InRange(winded, dWinded, min, 1f, $"effects.winded.{name}"),
            InRange(tired, dTired, min, 1f, $"effects.tired.{name}"),
            InRange(exhausted, dExhausted, min, 1f, $"effects.exhausted.{name}"),
        };
        if (values[1] <= values[0] && values[2] <= values[1])
            return values;
        Reject($"effects.*.{name} must not rise from winded to tired to exhausted ({values[0]}, {values[1]}, {values[2]}), using {dWinded}, {dTired}, {dExhausted}");
        return new[] { dWinded, dTired, dExhausted };
    }

    private float InRange(float? value, float fallback, float min, float max, string name)
    {
        if (value == null)
            return fallback;
        if (FiniteFloatValidator.IsFiniteInRange(value.Value, min, max))
            return value.Value;
        Reject($"{name} {value.Value} is outside {min} to {max}, using {fallback}");
        return fallback;
    }

    private void Reject(string message)
    {
        _rejected = true;
        _logger.LogWarning("StaminaConfigProvider: " + message);
    }
}
