using System.Collections.Generic;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingService : IArmyTargetingService
{
    private readonly IArmyTargetingSettingsProvider _settings;
    private readonly Dictionary<string, Dictionary<string, int>> _priorityIndex;

    public ArmyTargetingService(IArmyTargetingSettingsProvider settings, IArmyTargetingConfigProvider configProvider)
    {
        _settings = settings;
        _priorityIndex = BuildPriorityIndex(configProvider.GetConfig());
    }

    public float GetTargetMultiplier(string candidateId, string committedTargetId, string cultureId)
    {
        if (!_settings.EnableArmyStrategicIntelligence)
            return 1.0f;

        float multiplier = 1.0f;

        if (committedTargetId != null && candidateId == committedTargetId)
            multiplier *= _settings.CommitmentMultiplier;

        if (cultureId != null && _priorityIndex.TryGetValue(cultureId, out var cultureIndex))
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
