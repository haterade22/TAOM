using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms.Domain.Ledger;

/// <summary>One change of holder in a title's or an office's history: when, why, to whom and from whom.</summary>
public sealed class ChronicleEntry
{
    public ChronicleEntry(double campaignDay, TitleEventReason reason, string? heroId, string? previousHolderId)
    {
        CampaignDay = campaignDay;
        Reason = reason;
        HeroId = heroId;
        PreviousHolderId = previousHolderId;
    }

    public double CampaignDay { get; }
    public TitleEventReason Reason { get; }

    /// <summary>The new holder; null when the title or office fell vacant.</summary>
    public string? HeroId { get; }

    public string? PreviousHolderId { get; }
}
