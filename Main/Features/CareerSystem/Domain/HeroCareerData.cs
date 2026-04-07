using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public class HeroCareerData
{
    public string HeroStringId { get; set; }
    public string CareerStringId { get; set; }
    public List<string> ChoiceIds { get; set; }
    public List<int> TierUnlocks { get; set; }

    public HeroCareerData()
    {
        ChoiceIds = new List<string>();
        TierUnlocks = new List<int>();
    }

    public HeroCareerData(string heroStringId)
    {
        HeroStringId = heroStringId;
        ChoiceIds = new List<string>();
        TierUnlocks = new List<int>();
    }

    public bool HasChoice(string choiceId)
    {
        return ChoiceIds.Contains(choiceId);
    }

    public bool AddChoice(string choiceId)
    {
        if (HasChoice(choiceId)) return false;
        ChoiceIds.Add(choiceId);
        return true;
    }

    public bool RemoveChoice(string choiceId)
    {
        return ChoiceIds.Remove(choiceId);
    }

    public int GetChoiceCount()
    {
        return ChoiceIds.Count;
    }
}
