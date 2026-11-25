using System;
using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerDataService : ICareerDataService
{
    private Dictionary<string, HeroCareerData> _heroData = new Dictionary<string, HeroCareerData>();
    private static readonly IReadOnlyList<string> EmptyChoices = new List<string>();

    public event Action<string>? HeroCareerChanged;

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
        if (data.CareerStringId == careerStringId) return;
        data.CareerStringId = careerStringId;
        Changed(heroStringId);
    }

    public bool TryAddChoice(string heroStringId, string choiceStringId, int maxChoicesAllowed)
    {
        var data = GetOrCreateData(heroStringId);
        if (data.GetChoiceCount() >= maxChoicesAllowed) return false;
        if (!data.AddChoice(choiceStringId)) return false;
        Changed(heroStringId);
        return true;
    }

    public void RemoveChoice(string heroStringId, string choiceStringId)
    {
        if (_heroData.TryGetValue(heroStringId, out var data) && data.RemoveChoice(choiceStringId))
            Changed(heroStringId);
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
        if (data.TierUnlocks.Contains(tier)) return;
        data.TierUnlocks.Add(tier);
        Changed(heroStringId);
    }

    public bool IsTierUnlocked(string heroStringId, int tier)
    {
        return _heroData.TryGetValue(heroStringId, out var data)
            && data.TierUnlocks.Contains(tier);
    }

    public void SetFlag(string heroStringId, string flag)
    {
        var data = GetOrCreateData(heroStringId);
        if (data.AddFlag(flag))
            Changed(heroStringId);
    }

    public bool HasFlag(string heroStringId, string flag)
    {
        return _heroData.TryGetValue(heroStringId, out var data) && data.HasFlag(flag);
    }

    public void ClearCareer(string heroStringId)
    {
        if (!_heroData.TryGetValue(heroStringId, out var data)) return;
        var hadAny = data.CareerStringId != null || data.ChoiceIds.Count > 0 || data.TierUnlocks.Count > 0 || data.Flags.Count > 0;
        data.CareerStringId = null;
        data.ChoiceIds.Clear();
        data.TierUnlocks.Clear();
        data.Flags.Clear();
        if (hadAny) Changed(heroStringId);
    }

    public void ResetForNewCampaign()
    {
        // Fresh dictionary rather than Clear(): SyncData's save path hands the live reference out
        // through GetAllData(), so replacing it cannot disturb a reader mid-pass.
        _heroData = new Dictionary<string, HeroCareerData>();
    }

    public Dictionary<string, HeroCareerData> GetAllData()
    {
        return _heroData;
    }

    public void RestoreData(Dictionary<string, HeroCareerData> data)
    {
        _heroData = data ?? new Dictionary<string, HeroCareerData>();
    }

    public void ApplyHeroCareer(HeroCareerData? career)
    {
        if (career == null || string.IsNullOrEmpty(career.HeroStringId)) return;

        var copy = new HeroCareerData(career.HeroStringId) { CareerStringId = career.CareerStringId };
        copy.ChoiceIds.AddRange(career.ChoiceIds ?? new List<string>());
        copy.TierUnlocks.AddRange(career.TierUnlocks ?? new List<int>());
        copy.Flags.AddRange(career.Flags ?? new List<string>());
        _heroData[career.HeroStringId] = copy;
    }

    private void Changed(string heroStringId) => HeroCareerChanged?.Invoke(heroStringId);
}
