using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string PortraitSprite { get; }
    public string AbilityTemplateId { get; }

    /// <summary>
    /// The sprite id under CareerSystem\Abilities that draws this career's ability icon: the
    /// ability_sprite attribute when the XML sets one, else the ability template id. A career
    /// cloned from another sets it to the source's template id and so shows the source's icon.
    /// </summary>
    public string AbilitySprite { get; }
    public int MinClanTier { get; }
    public string RootChoiceId { get; }
    public IReadOnlyList<string> EligibleCultureIds { get; }
    public IReadOnlyList<string> ChoiceGroupIds { get; }

    /// <summary>
    /// Per-tier rank/title names (localized, e.g. "{=key}Captain of Ithilien"). Shown as the
    /// tier headers on the career screen — the career's rank at tiers 1/2/3, escalating in
    /// prestige. Empty when the XML omits them; the screen VM falls back to "Tier N" then.
    /// </summary>
    public string Rank1Name { get; }
    public string Rank2Name { get; }
    public string Rank3Name { get; }

    /// <summary>
    /// Issue #380 — banner-icon id rendered as this career's keystone medallion (banner
    /// icons register as bare-number sprite names, so this doubles as the sprite name).
    /// Empty when unset: no medallion renders — deliberately no culture fallback.
    /// </summary>
    public string KeystoneIcon { get; }

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
        string rank3Name = "",
        string keystoneIcon = "",
        string abilitySprite = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        PortraitSprite = portraitSprite;
        AbilityTemplateId = abilityTemplateId;
        AbilitySprite = string.IsNullOrEmpty(abilitySprite) ? abilityTemplateId : abilitySprite;
        MinClanTier = minClanTier;
        RootChoiceId = rootChoiceId;
        EligibleCultureIds = eligibleCultureIds ?? new List<string>();
        ChoiceGroupIds = choiceGroupIds ?? new List<string>();
        Rank1Name = rank1Name ?? "";
        Rank2Name = rank2Name ?? "";
        Rank3Name = rank3Name ?? "";
        KeystoneIcon = keystoneIcon ?? "";
    }
}
