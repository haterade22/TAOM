using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>One daily offer roll's result — at most one of the two is set. Neither set = nothing eligible today.</summary>
public sealed class DutyOfferSelection
{
    public DutyDefinition FieldDuty { get; set; }

    public InteractiveDutyDefinition InteractiveDuty { get; set; }

    public bool HasOffer => FieldDuty != null || InteractiveDuty != null;

    public static DutyOfferSelection None { get; } = new DutyOfferSelection();
}
