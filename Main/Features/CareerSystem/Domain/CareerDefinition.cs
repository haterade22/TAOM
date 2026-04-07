using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string PortraitSprite { get; }
    public string AbilityTemplateId { get; }
    public ChargeType ChargeType { get; }
    public int MaxCharge { get; }
    public int MinClanTier { get; }
    public string RootChoiceId { get; }
    public IReadOnlyList<string> EligibleCultureIds { get; }
    public IReadOnlyList<string> ChoiceGroupIds { get; }

    public CareerDefinition(
        string id,
        string displayName,
        string description,
        string portraitSprite,
        string abilityTemplateId,
        ChargeType chargeType,
        int maxCharge,
        int minClanTier,
        string rootChoiceId,
        IReadOnlyList<string> eligibleCultureIds,
        IReadOnlyList<string> choiceGroupIds)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        PortraitSprite = portraitSprite;
        AbilityTemplateId = abilityTemplateId;
        ChargeType = chargeType;
        MaxCharge = maxCharge;
        MinClanTier = minClanTier;
        RootChoiceId = rootChoiceId;
        EligibleCultureIds = eligibleCultureIds ?? new List<string>();
        ChoiceGroupIds = choiceGroupIds ?? new List<string>();
    }
}
