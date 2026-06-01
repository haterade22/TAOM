using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerDataService : ICareerDataService
{
    private Dictionary<string, HeroCareerData> _heroData = new Dictionary<string, HeroCareerData>();
    private static readonly IReadOnlyList<string> EmptyChoices = new List<string>();

    public HeroCareerData GetOrCreateData(string heroStringId)
    {
        if (!_heroData.TryGetValue(heroStringId, out var data))
        {
            data = new HeroCareerData(heroStringId);
            _heroData[heroStringId] = data;
        }
        return data;
    }

    public void SetCareer(string heroStringId, string careerStringId)
    {
        var data = GetOrCreateData(heroStringId);
        data.CareerStringId = careerStringId;
    }

    public bool TryAddChoice(string heroStringId, string choiceStringId, int maxChoicesAllowed)
    {
        var data = GetOrCreateData(heroStringId);
        if (data.GetChoiceCount() >= maxChoicesAllowed) return false;
        return data.AddChoice(choiceStringId);
    }

    public void RemoveChoice(string heroStringId, string choiceStringId)
    {
        if (_heroData.TryGetValue(heroStringId, out var data))
            data.RemoveChoice(choiceStringId);
    }

    public bool HasCareer(string heroStringId)
    {
        return _heroData.TryGetValue(heroStringId, out var data)
            && !string.IsNullOrEmpty(data.CareerStringId);
    }

    public string GetCareerStringId(string heroStringId)
    {
        return _heroData.TryGetValue(heroStringId, out var data) ? data.CareerStringId : null;
    }

    public IReadOnlyList<string> GetChoiceIds(string heroStringId)
    {
        return _heroData.TryGetValue(heroStringId, out var data)
            ? data.ChoiceIds
            : EmptyChoices;
    }

    public int GetChoiceCount(string heroStringId)
    {
        return _heroData.TryGetValue(heroStringId, out var data)
            ? data.GetChoiceCount()
            : 0;
    }

    public void UnlockTier(string heroStringId, int tier)
    {
        var data = GetOrCreateData(heroStringId);
        if (!data.TierUnlocks.Contains(tier))
            data.TierUnlocks.Add(tier);
    }

    public bool IsTierUnlocked(string heroStringId, int tier)
    {
        return _heroData.TryGetValue(heroStringId, out var data)
            && data.TierUnlocks.Contains(tier);
    }

    public void SetFlag(string heroStringId, string flag)
    {
        var data = GetOrCreateData(heroStringId);
        data.AddFlag(flag);
    }

    public bool HasFlag(string heroStringId, string flag)
    {
        return _heroData.TryGetValue(heroStringId, out var data) && data.HasFlag(flag);
    }

    public void ClearCareer(string heroStringId)
    {
        if (!_heroData.TryGetValue(heroStringId, out var data)) return;
        data.CareerStringId = null;
        data.ChoiceIds.Clear();
        data.TierUnlocks.Clear();
        data.Flags.Clear();
    }

    public Dictionary<string, HeroCareerData> GetAllData()
    {
        return _heroData;
    }

    public void RestoreData(Dictionary<string, HeroCareerData> data)
    {
        _heroData = data ?? new Dictionary<string, HeroCareerData>();
    }
}
