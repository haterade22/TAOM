using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerChoiceGroupDefinition
{
    public string Id { get; }
    public string CareerId { get; }
    public int Tier { get; }
    public IReadOnlyList<string> ChoiceIds { get; }

    /// <summary>
    /// Player-facing path name (localized, e.g. "{=key}Path of the Ranger"). Empty when the
    /// XML omits <c>display_name</c>; the screen VM falls back to a humanized id in that case.
    /// </summary>
    public string DisplayName { get; }

    public CareerChoiceGroupDefinition(
        string id,
        string careerId,
        int tier,
        IReadOnlyList<string> choiceIds,
        string displayName = "")
    {
        Id = id;
        CareerId = careerId;
        Tier = tier;
        ChoiceIds = choiceIds ?? new List<string>();
        DisplayName = displayName ?? "";
    }
}
