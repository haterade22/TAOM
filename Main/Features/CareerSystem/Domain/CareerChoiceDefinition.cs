using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerChoiceDefinition
{
    public string Id { get; }
    public string GroupId { get; }
    public ChoiceType Type { get; }
    public string Description { get; }
    public string IconSprite { get; }
    public PassiveEffect Passive { get; }
    public IReadOnlyList<MutationDefinition> Mutations { get; }

    public CareerChoiceDefinition(
        string id,
        string groupId,
        ChoiceType type,
        string description,
        string iconSprite,
        PassiveEffect passive,
        IReadOnlyList<MutationDefinition> mutations)
    {
        Id = id;
        GroupId = groupId ?? "";
        Type = type;
        Description = description;
        IconSprite = iconSprite;
        Passive = passive;
        Mutations = mutations ?? new List<MutationDefinition>();
    }
}
