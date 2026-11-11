namespace TAOM.Features.Realms.Domain.Ledger;

public enum ClaimKind
{
    /// <summary>Took the settlement by force while someone else held the title.</summary>
    Conquest,

    /// <summary>Manufactured by the claimant: bought with their people's resource (the player) or gold (the AI).</summary>
    Fabricated,

    /// <summary>Descends from a former holder. Reserved for a later milestone.</summary>
    Ancestral,
}

public sealed class TitleClaim
{
    public TitleClaim(string titleId, string heroId, ClaimKind kind, double campaignDay)
    {
        TitleId = titleId;
        HeroId = heroId;
        Kind = kind;
        CampaignDay = campaignDay;
    }

    public string TitleId { get; }
    public string HeroId { get; }
    public ClaimKind Kind { get; }

    /// <summary>Campaign day the claim was gained, so later milestones can let claims age out.</summary>
    public double CampaignDay { get; }
}
