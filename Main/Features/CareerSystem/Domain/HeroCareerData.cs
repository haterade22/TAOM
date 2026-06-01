using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public class HeroCareerData
{
    public string HeroStringId { get; set; }
    public string CareerStringId { get; set; }
    public List<string> ChoiceIds { get; set; }
    public List<int> TierUnlocks { get; set; }

    /// <summary>
    /// Career-quest attribute flags (TAOM's equivalent of TOR's <c>Hero.AddAttribute</c>): string
    /// markers granted by quest completion (GrantAttributeFlag reward) and read by downstream
    /// dialog/content gating. Persisted alongside the rest of the hero's career data.
    /// </summary>
    public List<string> Flags { get; set; }

    public HeroCareerData()
    {
        ChoiceIds = new List<string>();
        TierUnlocks = new List<int>();
        Flags = new List<string>();
    }

    public HeroCareerData(string heroStringId)
    {
        HeroStringId = heroStringId;
        ChoiceIds = new List<string>();
        TierUnlocks = new List<int>();
        Flags = new List<string>();
    }

    public bool HasFlag(string flag) => Flags.Contains(flag);

    public bool AddFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag) || Flags.Contains(flag)) return false;
        Flags.Add(flag);
        return true;
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
