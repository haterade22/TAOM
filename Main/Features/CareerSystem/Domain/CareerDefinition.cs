using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string PortraitSprite { get; }
    public string AbilityTemplateId { get; }
    public int MinClanTier { get; }
    public string RootChoiceId { get; }
    public IReadOnlyList<string> EligibleCultureIds { get; }
    public IReadOnlyList<string> ChoiceGroupIds { get; }

    /// <summary>
    /// Per-tier rank/title names (localized, e.g. "{=key}Captain of Ithilien"). Shown as the
    /// tier headers on the career screen — the career's rank at tiers 1/2/3, escalating in
    /// prestige. Empty when the XML omits them; the screen VM falls back to "Tier N" then.
    /// (Adopted from TOR_Core's tor_career_rank{1,2,3}_name convention.)
    /// </summary>
    public string Rank1Name { get; }
    public string Rank2Name { get; }
    public string Rank3Name { get; }

    public CareerDefinition(
        string id,
        string displayName,
        string description,
        string portraitSprite,
        string abilityTemplateId,
        int minClanTier,
        string rootChoiceId,
        IReadOnlyList<string> eligibleCultureIds,
        IReadOnlyList<string> choiceGroupIds,
        string rank1Name = "",
        string rank2Name = "",
        string rank3Name = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        PortraitSprite = portraitSprite;
        AbilityTemplateId = abilityTemplateId;
        MinClanTier = minClanTier;
        RootChoiceId = rootChoiceId;
        EligibleCultureIds = eligibleCultureIds ?? new List<string>();
        ChoiceGroupIds = choiceGroupIds ?? new List<string>();
        Rank1Name = rank1Name ?? "";
        Rank2Name = rank2Name ?? "";
        Rank3Name = rank3Name ?? "";
    }
}
