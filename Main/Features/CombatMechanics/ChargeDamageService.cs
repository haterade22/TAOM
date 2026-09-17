using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.CombatMechanics;

/// <summary>
/// Pure lookup: culture id to charge multiplier. The table is rebuilt once per process from the
/// validated config into a case-insensitive dictionary; the toggle is read per call because MCM
/// changes mid-session. Runs on every mount stat update, so no allocation per call.
/// </summary>
public sealed class ChargeDamageService : IChargeDamageService
{
    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly Dictionary<string, float> _multipliers;

    public ChargeDamageService(ICombatMechanicsConfigProvider configProvider, ICombatMechanicsSettingsProvider settings)
    {
        _settings = settings;
        _multipliers = Index(configProvider.GetConfig().ChargeDamage?.CultureMultipliers);
    }

    public float Multiplier(string? cultureId)
    {
        if (string.IsNullOrEmpty(cultureId) || !_settings.CultureChargeDamageEnabled)
            return 1f;

        return _multipliers.TryGetValue(cultureId!, out var factor) ? factor : 1f;
    }

    // The provider already rejects non-finite and out-of-range factors; re-checking here costs a
    // comparison on a table built once and keeps a hand-built config out of an engine float.
    private static Dictionary<string, float> Index(Dictionary<string, float>? source)
    {
        var index = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
            return index;

        foreach (var pair in source)
        {
            if (string.IsNullOrEmpty(pair.Key) || !FiniteFloatValidator.IsFinite(pair.Value) || !(pair.Value > 0f))
                continue;
            index[pair.Key] = pair.Value;
        }

        return index;
    }
}
