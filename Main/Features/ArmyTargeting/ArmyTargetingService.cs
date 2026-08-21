using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingService : IArmyTargetingService
{
    /// <summary>Lowest outer reach radius an MCM slider may impose, in town gaps.</summary>
    private const float MinReachRadius = 1.0f;

    /// <summary>Highest outer reach radius. Vanilla's own distance term saturates at 5 gaps, so anything past this is a no-op by construction.</summary>
    private const float MaxReachRadius = 20.0f;

    private readonly IArmyTargetingSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly ArmyTargetingConfig _config;
    private readonly Dictionary<string, Dictionary<string, int>> _priorityIndex;
    private readonly Dictionary<string, float> _aggressionIndex;
    private readonly Dictionary<string, string[]> _theaterIndex;

    // One-shot breadcrumbs: each path fires per-army-per-tick, so log the first occurrence
    // (with real values) then stay silent. Static = once per game process.
    private static bool _loggedTargetMultiplier;
    private static bool _loggedStrengthMultiplier;
    private static bool _loggedTheaterWeight;
    private static bool _loggedReach;

    /// <summary>Cap on distinct far-candidate pairs recorded, so a long campaign cannot grow the log or the set without bound.</summary>
    private const int MaxLoggedFarCandidates = 200;
    private readonly HashSet<string> _loggedFarCandidates = new HashSet<string>(StringComparer.Ordinal);

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

    public float GetReachMultiplier(float normalizedDistance)
    {
        if (!_settings.EnableArmyStrategicIntelligence) return 1.0f;

        // Positive-requirement polarity per csharp-architecture.md: an unmeasurable distance must
        // FAIL the gate that would suppress. A landless faction, a missing navigation cache, or a
        // zero average town gap all surface here as NaN, and damping every target on garbage would
        // break AI targeting outright. Deferring to vanilla is the safe direction.
        if (!FiniteFloatValidator.IsFinite(normalizedDistance)) return 1.0f;

        float radius = ResolvedReachRadius();
        float inner = ResolvedInnerRadius(radius);
        float floor = ResolvedReachFloor();

        if (normalizedDistance <= inner) return 1.0f;
        if (normalizedDistance >= radius) return floor;

        float span = radius - inner;
        if (span <= 0f) return floor;

        float t = (normalizedDistance - inner) / span;
        float result = 1.0f - t * (1.0f - floor);

        if (!_loggedReach)
        {
            _loggedReach = true;
            _logger.LogDebug($"ArmyTargeting: reach x{result:F3} at {normalizedDistance:F2} town gaps (inner={inner:F2}, radius={radius:F2}, floor={floor:F2})");
        }

        return result;
    }

    public bool IsWithinReach(float normalizedDistance)
    {
        if (!_settings.EnableArmyStrategicIntelligence) return true;

        // Opposite polarity to GetReachMultiplier, and deliberately so. This gate decides whether
        // TAOM may OVERTURN vanilla's "unreachable" verdict. Vanilla already said no; an
        // unmeasurable distance is not grounds to overrule it. Both directions defer to vanilla,
        // which is the invariant, not the boolean.
        if (!FiniteFloatValidator.IsFinite(normalizedDistance)) return false;

        return normalizedDistance < ResolvedReachRadius();
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
            case ArmyTargetingMission.Besieger:
            {
                // Commitment stickiness stays multiplicative against suppression rather than being
                // skipped: 4.0 x 0.35 x 0.05 = 0.07 loses decisively to a legal near target at
                // 1.0 x 1.25 x 1.0 = 1.25, so an in-flight cross-map siege on an existing save
                // re-targets instead of pinning. ArmyTargetingServiceTests pins that arithmetic.
                float priority = GetTargetMultiplier(context.TargetSettlementId, context.CommittedTargetId, context.FactionId);
                float theater = GetTheaterWeight(context.FactionId, context.TargetFactionId);
                float reach = GetReachMultiplier(context.NormalizedDistance);
                LogFarCandidate(context, reach);
                return context.BaseScore * priority * theater * reach;
            }

            // Raider is deliberately absent: vanilla already hard-zeroes raiders past 5 town gaps
            // in GetDistanceScoreForRaiding, so a TAOM term there would buy nothing.
            case ArmyTargetingMission.Defender:
                return context.BaseScore * ResolvedDefenderMultiplier();

            default:
                return context.BaseScore;
        }
    }

    /// <summary>
    /// Phase 0 provenance instrumentation. The established root causes explain Gondor pushing at
    /// Harad and Gundabad reaching for Dale, but NOT a Gondor army in the far north: vanilla's own
    /// topology score rejects that pair before this method is ever reached. This names whichever
    /// faction/target pairs actually survive vanilla's filter at long range, so the next campaign
    /// observation is evidence rather than inference.
    /// </summary>
    private void LogFarCandidate(TargetScoreContext context, float reach)
    {
        if (!FiniteFloatValidator.IsFinite(context.NormalizedDistance)) return;
        if (context.NormalizedDistance < ResolvedReachRadius()) return;
        if (_loggedFarCandidates.Count >= MaxLoggedFarCandidates) return;

        string key = context.FactionId + ">" + context.TargetSettlementId;
        if (!_loggedFarCandidates.Add(key)) return;

        _logger.LogDebug(
            $"ArmyTargeting: FAR CANDIDATE {context.FactionId} -> {context.TargetSettlementId} " +
            $"(owner={context.TargetFactionId}) at {context.NormalizedDistance:F2} town gaps, " +
            $"vanilla score {context.BaseScore:F2}, reach x{reach:F3}, theater x{GetTheaterWeight(context.FactionId, context.TargetFactionId):F2}");
    }

    private float ResolvedReachRadius()
    {
        float radius = _settings.ReachRadiusInTownGaps;
        if (!FiniteFloatValidator.IsFiniteInRange(radius, MinReachRadius, MaxReachRadius))
            radius = _config.ReachRadiusInTownGaps;
        if (!FiniteFloatValidator.IsFiniteInRange(radius, MinReachRadius, MaxReachRadius))
            radius = new ArmyTargetingConfig().ReachRadiusInTownGaps;
        return radius;
    }

    // Centralized clamp rather than duplicate invariants at the JSON and MCM surfaces
    // (csharp-architecture.md "Config Providers MUST Validate", clause 7). Deriving the inner
    // radius from the resolved outer radius makes an inversion unrepresentable.
    private float ResolvedInnerRadius(float radius)
    {
        float inner = _config.ReachInnerRadiusInTownGaps;
        if (!FiniteFloatValidator.IsFiniteAtLeast(inner, 0f)) inner = 0f;
        float ceiling = radius * 0.5f;
        return inner < ceiling ? inner : ceiling;
    }

    private float ResolvedReachFloor()
    {
        float floor = _config.ReachFloor;
        return FiniteFloatValidator.IsFiniteInRange(floor, 0.001f, 1.0f) ? floor : new ArmyTargetingConfig().ReachFloor;
    }

    private float ResolvedDefenderMultiplier()
    {
        float m = _settings.DefenderPriorityMultiplier;
        return FiniteFloatValidator.IsFiniteInRange(m, 1.0f, 5.0f) ? m : 1.0f;
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
            if (kvp.Value > 1.0f)
                index[kvp.Key] = kvp.Value;
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

            var cultureIndex = new Dictionary<string, int>(kvp.Value.Count);
            for (int i = 0; i < kvp.Value.Count; i++)
                cultureIndex[kvp.Value[i]] = i;

            index[kvp.Key] = cultureIndex;
        }

        return index;
    }
}
