using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerChoiceDefinition
{
    public string Id { get; }
    public string GroupId { get; }
    public ChoiceType Type { get; }
    public string Description { get; }
    public string IconSprite { get; }
    // Up to CareerConfigProvider.MaxPassivesPerChoice effects, in document order. Never null; empty when
    // the choice grants no passive (a keystone, a mutation-only choice).
    public IReadOnlyList<PassiveEffect> Passives { get; }
    public IReadOnlyList<MutationDefinition> Mutations { get; }

    public CareerChoiceDefinition(
        string id,
        string groupId,
        ChoiceType type,
        string description,
        string iconSprite,
        IReadOnlyList<PassiveEffect> passives,
        IReadOnlyList<MutationDefinition> mutations)
    {
        Id = id;
        GroupId = groupId ?? "";
        Type = type;
        Description = description;
        IconSprite = iconSprite;
        Passives = passives ?? new List<PassiveEffect>();
        Mutations = mutations ?? new List<MutationDefinition>();
    }
}
