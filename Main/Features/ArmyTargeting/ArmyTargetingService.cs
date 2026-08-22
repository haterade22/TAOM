using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingService : IArmyTargetingService
{
    /// <summary>Lowest border-rescue radius an MCM slider may impose, in town gaps.</summary>
    private const float MinRescueRadius = 1.0f;

    /// <summary>Highest border-rescue radius. Past this the gate stops bounding anything, since vanilla's own distance term saturates at 5 gaps.</summary>
    private const float MaxRescueRadius = 20.0f;

    /// <summary>Compiled fallback for the Defender multiplier, matching TaomSettings.ArmyDefenderPriority.</summary>
    private const float DefaultDefenderMultiplier = 1.6f;

    private readonly IArmyTargetingSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly ArmyTargetingConfig _config;
    private readonly Dictionary<string, Dictionary<string, int>> _priorityIndex;
    private readonly Dictionary<string, float> _aggressionIndex;
    private readonly Dictionary<string, string[]> _theaterIndex;

    // One-shot breadcrumbs: each path fires per-army-per-tick, so log the first occurrence
    // (with real values) then stay silent.
    //
    // INSTANCE, not static, and reset per campaign. These were static, which on a
    // process-lifetime singleton meant the second campaign of a session logged nothing at all:
    // a tester loading save B after save A would read the silence as "the feature is not firing".
    private bool _loggedTargetMultiplier;
    private bool _loggedStrengthMultiplier;
    private bool _loggedTheaterWeight;

    public ArmyTargetingService(IArmyTargetingSettingsProvider settings, IArmyTargetingConfigProvider configProvider, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
        _config = configProvider.GetConfig() ?? new ArmyTargetingConfig();
        _priorityIndex = BuildPriorityIndex(_config);
        _aggressionIndex = BuildFloatIndex(_config.FactionAggressionMultipliers);
        _theaterIndex = BuildTheaterIndex(_config);
        _logger.LogInfo($"ArmyTargeting: loaded {_priorityIndex.Count} priority factions, {_aggressionIndex.Count} aggression entries, {_theaterIndex.Count} theater assignments");
    }

    /// <summary>
    /// Clears the one-shot diagnostic latches so a second campaign in the same process still
    /// produces provenance breadcrumbs.
    /// </summary>
    public void ResetDiagnostics()
    {
        _loggedTargetMultiplier = false;
        _loggedStrengthMultiplier = false;
        _loggedTheaterWeight = false;
    }

    public float GetTargetMultiplier(string candidateId, string committedTargetId, string factionId)
    {
        if (!_settings.EnableArmyStrategicIntelligence)
            return 1.0f;

        float multiplier = 1.0f;

        if (committedTargetId != null && candidateId == committedTargetId)
            multiplier *= _settings.CommitmentMultiplier;

        if (factionId != null && _priorityIndex.TryGetValue(factionId, out var cultureIndex))
        {
            if (cultureIndex.TryGetValue(candidateId, out int idx))
            {
                int total = cultureIndex.Count;
                float t = total > 1 ? idx / (float)(total - 1) : 0f;
                float boost = _settings.MaxPriorityBoost - t * (_settings.MaxPriorityBoost - 1.0f);
                multiplier *= boost;
            }
        }

        if (multiplier != 1.0f && !_loggedTargetMultiplier)
        {
            _loggedTargetMultiplier = true;
            _logger.LogDebug($"ArmyTargeting: {factionId} -> {candidateId} target x{multiplier:F2} (committed={committedTargetId})");
        }

        return multiplier;
    }

    public float GetStrengthMultiplier(string factionId)
    {
        if (!_settings.EnableArmyStrategicIntelligence) return 1.0f;
        if (factionId == null) return 1.0f;
        if (_aggressionIndex.TryGetValue(factionId, out float m))
        {
            float result = m * _settings.EvilAggressionScale;
            if (!_loggedStrengthMultiplier)
            {
                _loggedStrengthMultiplier = true;
                _logger.LogDebug($"ArmyTargeting: {factionId} strength x{result:F2}");
            }
            return result;
        }
        return 1.0f;
    }

    public bool IsInPriorityList(string factionId, string settlementId)
    {
        if (factionId == null || settlementId == null) return false;
        return _priorityIndex.TryGetValue(factionId, out var targets) && targets.ContainsKey(settlementId);
    }

    public bool IsWithinBorderRescueRange(float normalizedDistance)
    {
        if (!_settings.EnableArmyStrategicIntelligence) return true;

        // An unmeasurable distance REFUSES the rescue. This gate decides whether TAOM may overturn
        // vanilla's "unreachable" verdict, and vanilla has already said no; garbage is not grounds
        // to overrule it. csharp-architecture.md's positive-requirement polarity.
        if (!FiniteFloatValidator.IsFinite(normalizedDistance)) return false;

        return normalizedDistance < ResolvedRescueRadius();
    }

    public float GetTheaterWeight(string attackerFactionId, string targetFactionId)
    {
        if (!_settings.EnableArmyStrategicIntelligence || !_settings.EnableWarTheaters) return 1.0f;
        if (attackerFactionId == null || targetFactionId == null) return 1.0f;
        if (string.Equals(attackerFactionId, targetFactionId, StringComparison.Ordinal)) return 1.0f;

        // Fail open on either side. A kingdom absent from the table is neutral, not foreign:
        // player-founded kingdoms are "new_kingdom", rebels are "<settlementId>_rebel_clan", and
        // neither can appear in a shipped config. An empty list marks a deliberately passive
        // kingdom (bluecraig, lindon) rather than an unknown one.
        if (!_theaterIndex.TryGetValue(attackerFactionId, out var attacker) || attacker.Length == 0) return 1.0f;
        if (!_theaterIndex.TryGetValue(targetFactionId, out var target) || target.Length == 0) return 1.0f;

        float weight = _config.ForeignTheaterWeight;
        if (Contains(target, attacker[0]))
        {
            weight = _config.PrimaryTheaterWeight;
        }
        else
        {
            for (int i = 1; i < attacker.Length; i++)
            {
                if (Contains(target, attacker[i]))
                {
                    weight = _config.SecondaryTheaterWeight;
                    break;
                }
            }
        }

        if (!_loggedTheaterWeight)
        {
            _loggedTheaterWeight = true;
            _logger.LogDebug($"ArmyTargeting: theater x{weight:F2} for {attackerFactionId}(primary={attacker[0]}) -> {targetFactionId}");
        }

        return weight;
    }

    public float GetEffectiveStrength(string factionId, bool isBesieger, float ourStrength)
    {
        if (!isBesieger) return ourStrength;
        return ourStrength * GetStrengthMultiplier(factionId);
    }

    public float ApplyTargetScoreModifiers(TargetScoreContext context)
    {
        if (context == null) return 0f;

        // Positive-requirement gate. The previous form was `baseScore <= 0f`, and NaN <= 0f is
        // false, so a NaN base score fell straight into the multiply chain.
        if (!FiniteFloatValidator.IsFinite(context.BaseScore) || context.BaseScore <= 0f)
            return context.BaseScore;

        if (!_settings.EnableArmyStrategicIntelligence)
            return context.BaseScore;

        switch (context.Mission)
        {
            // There is deliberately NO metric distance term here. Vanilla's own besieger factor is
            // MBMath.Map((5G-d)/G, 0f, 5f, 0.9f, 10f), a 10.0-to-0.9 ramp, and
            // CalculateDistanceScoreForBesieging hard-zeroes anything scoring under 0.1 topology,
            // so vanilla already discriminates hard on distance.
            //
            // A TAOM falloff was tried and REMOVED on 2026-08-22. Measured end to end it moved the
            // crossover between a max-boost far target and a near neutral one from 4.029 to 3.746
            // town gaps: 0.283 gaps, in exchange for an adapter on the hot path, a three-way cache,
            // and a path where suppressing a committed target pushed Army.ThinkAboutCohesionBoost
            // under its 0.01f gate and disbanded the army. Distance is vanilla's job. TAOM's job is
            // only to stop its OWN priority boost from overturning vanilla's verdict, and Patch22's
            // border-rescue gate is where that happens.
            case ArmyTargetingMission.Besieger:
                return context.BaseScore
                     * GetTargetMultiplier(context.TargetSettlementId, context.CommittedTargetId, context.FactionId)
                     * GetTheaterWeight(context.FactionId, context.TargetFactionId);

            // Raider is absent on purpose: vanilla already hard-zeroes raiders past 5 town gaps in
            // GetDistanceScoreForRaiding.
            case ArmyTargetingMission.Defender:
                return context.BaseScore * ResolvedDefenderMultiplier();

            default:
                return context.BaseScore;
        }
    }

    private float ResolvedRescueRadius()
    {
        float radius = _settings.BorderRescueRadiusInTownGaps;
        if (!FiniteFloatValidator.IsFiniteInRange(radius, MinRescueRadius, MaxRescueRadius))
            radius = _config.BorderRescueRadiusInTownGaps;
        if (!FiniteFloatValidator.IsFiniteInRange(radius, MinRescueRadius, MaxRescueRadius))
            radius = new ArmyTargetingConfig().BorderRescueRadiusInTownGaps;
        return radius;
    }

    private float ResolvedDefenderMultiplier()
    {
        float m = _settings.DefenderPriorityMultiplier;
        // Falls back to the compiled default, not to 1.0. Reverting to 1.0 would silently disable
        // the home-defence lever on a garbage MCM value instead of restoring its intended strength.
        return FiniteFloatValidator.IsFiniteInRange(m, 1.0f, 5.0f) ? m : DefaultDefenderMultiplier;
    }

    private static bool Contains(string[] haystack, string needle)
    {
        for (int i = 0; i < haystack.Length; i++)
            if (string.Equals(haystack[i], needle, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static Dictionary<string, float> BuildFloatIndex(Dictionary<string, float> source)
    {
        var index = new Dictionary<string, float>();
        if (source == null) return index;
        foreach (var kvp in source)
        {
            // Finite AND inside a sane band, not merely above neutral. An infinity here makes the
            // inflated ourStrength infinite, which defeats vanilla's `ourStrength < defenderStrength
            // * 2` siege veto for every fortress on the map. Json.NET parses 1e39, "Infinity" and a
            // bare Infinity token into float.PositiveInfinity, and a `> 1.0f` test accepts all three.
            if (!FiniteFloatValidator.IsFiniteInRange(kvp.Value, 1.0f, 100.0f)) continue;
            if (kvp.Value > 1.0f) index[kvp.Key] = kvp.Value;
        }
        return index;
    }

    private static Dictionary<string, string[]> BuildTheaterIndex(ArmyTargetingConfig config)
    {
        var index = new Dictionary<string, string[]>();
        if (config.KingdomTheaters == null) return index;

        foreach (var kvp in config.KingdomTheaters)
        {
            if (kvp.Key == null) continue;
            // A null list and an empty list both mean "passive", which the weighting reads as neutral.
            index[kvp.Key] = kvp.Value == null ? new string[0] : kvp.Value.ToArray();
        }

        return index;
    }

    private static Dictionary<string, Dictionary<string, int>> BuildPriorityIndex(ArmyTargetingConfig config)
    {
        var index = new Dictionary<string, Dictionary<string, int>>();
        if (config.FactionPriorityTargets == null) return index;

        foreach (var kvp in config.FactionPriorityTargets)
        {
            if (kvp.Value == null || kvp.Value.Count == 0)
                continue;

            // Indices are assigned from the DEDUPED sequence, not from the raw list position.
            // Writing raw positions into a dictionary lets a duplicate id collapse two entries
            // while the surviving index keeps climbing, so Count-1 no longer equals the maximum
            // index. GetTargetMultiplier then computes t > 1 and the boost goes NEGATIVE:
            // ["A","A","B"] yields {A:1,B:2}, Count 2, so B gets t=2 and boost 3-2*2 = -1, which
            // flips a positive siege score to negative. Null and blank ids are skipped rather than
            // used as dictionary keys, which would throw during model registration.
            var cultureIndex = new Dictionary<string, int>(kvp.Value.Count);
            foreach (var target in kvp.Value)
            {
                if (string.IsNullOrWhiteSpace(target)) continue;
                if (cultureIndex.ContainsKey(target)) continue;
                cultureIndex[target] = cultureIndex.Count;
            }

            if (cultureIndex.Count > 0)
                index[kvp.Key] = cultureIndex;
        }

        return index;
    }
}
