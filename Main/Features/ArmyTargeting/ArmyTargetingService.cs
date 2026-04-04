using System.Collections.Generic;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingService : IArmyTargetingService
{
    private readonly IArmyTargetingSettingsProvider _settings;
    private readonly Dictionary<string, Dictionary<string, int>> _priorityIndex;
    private readonly Dictionary<string, float> _aggressionIndex;
    private readonly Dictionary<string, float> _distanceRangeIndex;

    public ArmyTargetingService(IArmyTargetingSettingsProvider settings, IArmyTargetingConfigProvider configProvider)
    {
        _settings = settings;
        var config = configProvider.GetConfig();
        _priorityIndex = BuildPriorityIndex(config);
        _aggressionIndex = BuildFloatIndex(config.FactionAggressionMultipliers);
        _distanceRangeIndex = BuildFloatIndex(config.FactionDistanceRangeMultipliers);
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

        return multiplier;
    }

    public float GetStrengthMultiplier(string factionId)
    {
        if (!_settings.EnableArmyStrategicIntelligence) return 1.0f;
        if (factionId == null) return 1.0f;
        if (_aggressionIndex.TryGetValue(factionId, out float m))
            return m * _settings.EvilAggressionScale;
        return 1.0f;
    }

    public float GetDistanceCompensation(string factionId, string targetId)
    {
        if (!_settings.EnableArmyStrategicIntelligence) return 1.0f;
        if (factionId == null) return 1.0f;
        if (!_priorityIndex.TryGetValue(factionId, out var targets)) return 1.0f;
        if (!targets.TryGetValue(targetId, out _)) return 1.0f;
        if (!_distanceRangeIndex.TryGetValue(factionId, out float scale)) return 1.0f;
        return scale * _settings.LongRangePriorityBoostScale;
    }

    public bool IsInPriorityList(string factionId, string settlementId)
    {
        if (factionId == null || settlementId == null) return false;
        return _priorityIndex.TryGetValue(factionId, out var targets) && targets.ContainsKey(settlementId);
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
