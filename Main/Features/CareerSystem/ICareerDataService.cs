using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerDataService
{
    HeroCareerData GetOrCreateData(string heroStringId);
    void SetCareer(string heroStringId, string careerStringId);
    bool TryAddChoice(string heroStringId, string choiceStringId, int maxChoicesAllowed);
    void RemoveChoice(string heroStringId, string choiceStringId);
    bool HasCareer(string heroStringId);
    string GetCareerStringId(string heroStringId);
    IReadOnlyList<string> GetChoiceIds(string heroStringId);
    int GetChoiceCount(string heroStringId);
    void UnlockTier(string heroStringId, int tier);
    bool IsTierUnlocked(string heroStringId, int tier);
    void SetFlag(string heroStringId, string flag);
    bool HasFlag(string heroStringId, string flag);
    void ClearCareer(string heroStringId);
    Dictionary<string, HeroCareerData> GetAllData();
    void RestoreData(Dictionary<string, HeroCareerData> data);
}
