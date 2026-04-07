using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerChoiceGroupDefinition
{
    public string Id { get; }
    public string CareerId { get; }
    public int Tier { get; }
    public IReadOnlyList<string> ChoiceIds { get; }

    public CareerChoiceGroupDefinition(
        string id,
        string careerId,
        int tier,
        IReadOnlyList<string> choiceIds)
    {
        Id = id;
        CareerId = careerId;
        Tier = tier;
        ChoiceIds = choiceIds ?? new List<string>();
    }
}
