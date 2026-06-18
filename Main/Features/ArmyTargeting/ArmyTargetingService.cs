using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingService : IArmyTargetingService
{
    private readonly IArmyTargetingSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly Dictionary<string, Dictionary<string, int>> _priorityIndex;
    private readonly Dictionary<string, float> _aggressionIndex;
    private readonly Dictionary<string, float> _distanceRangeIndex;

    // One-shot breadcrumbs: each path fires per-army-per-tick, so log the first occurrence
    // (with real values) then stay silent. Static = once per game process.
    private static bool _loggedTargetMultiplier;
    private static bool _loggedStrengthMultiplier;
    private static bool _loggedDistanceCompensation;

    public ArmyTargetingService(IArmyTargetingSettingsProvider settings, IArmyTargetingConfigProvider configProvider, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
        var config = configProvider.GetConfig();
        _priorityIndex = BuildPriorityIndex(config);
        _aggressionIndex = BuildFloatIndex(config.FactionAggressionMultipliers);
        _distanceRangeIndex = BuildFloatIndex(config.FactionDistanceRangeMultipliers);
        _logger.LogInfo($"ArmyTargeting: loaded {_priorityIndex.Count} priority factions, {_aggressionIndex.Count} aggression entries, {_distanceRangeIndex.Count} distance entries");
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
            _logger.LogDebug($"ArmyTargeting: {factionId}→{candidateId} target ×{multiplier:F2} (committed={committedTargetId})");
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
                _logger.LogDebug($"ArmyTargeting: {factionId} strength ×{result:F2}");
            }
            return result;
        }
        return 1.0f;
    }

    public float GetDistanceCompensation(string factionId, string targetId)
    {
        if (!_settings.EnableArmyStrategicIntelligence) return 1.0f;
        if (factionId == null) return 1.0f;
        if (!_priorityIndex.TryGetValue(factionId, out var targets)) return 1.0f;
        if (!targets.TryGetValue(targetId, out _)) return 1.0f;
        if (!_distanceRangeIndex.TryGetValue(factionId, out float scale)) return 1.0f;
        float compensation = scale * _settings.LongRangePriorityBoostScale;
        if (!_loggedDistanceCompensation)
        {
            _loggedDistanceCompensation = true;
            _logger.LogDebug($"ArmyTargeting: distance compensation ×{compensation:F2} for {factionId}→{targetId}");
        }
        return compensation;
    }

    public bool IsInPriorityList(string factionId, string settlementId)
    {
        if (factionId == null || settlementId == null) return false;
        return _priorityIndex.TryGetValue(factionId, out var targets) && targets.ContainsKey(settlementId);
    }

    // Phase 9b #138 — orchestration methods moved out of TaomTargetScoreModel override body.
    public float GetEffectiveStrength(string factionId, bool isBesieger, float ourStrength)
    {
        if (!isBesieger) return ourStrength;
        return ourStrength * GetStrengthMultiplier(factionId);
    }

    public float ApplyTargetScoreModifiers(float baseScore, bool isBesieger, string factionId, string targetSettlementId, string committedTargetId)
    {
        // Only Besieger armies receive priority + distance modifiers — Raider re-selects freely
        // each tick, Defender stays reactive to incoming threats. Vanilla rejection (baseScore <= 0)
        // also passes through unchanged.
        if (baseScore <= 0f || !isBesieger) return baseScore;

        float targetMultiplier = GetTargetMultiplier(targetSettlementId, committedTargetId, factionId);
        float distanceCompensation = GetDistanceCompensation(factionId, targetSettlementId);
        return baseScore * targetMultiplier * distanceCompensation;
    }

    private static Dictionary<string, float> BuildFloatIndex(Dictionary<string, float> source)
    {
        var index = new Dictionary<string, float>();
        foreach (var kvp in source)
            if (kvp.Value > 1.0f)
                index[kvp.Key] = kvp.Value;
        return index;
    }

    private static Dictionary<string, Dictionary<string, int>> BuildPriorityIndex(ArmyTargetingConfig config)
    {
        var index = new Dictionary<string, Dictionary<string, int>>();

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
