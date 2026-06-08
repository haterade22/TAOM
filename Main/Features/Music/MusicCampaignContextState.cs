namespace TAOM.Features.Music;

public sealed class MusicCampaignContextState
{
    public MusicCampaignContextState(
        bool isActive,
        bool siege,
        bool battle,
        bool inSettlement,
        bool inTavern,
        string stableCultureId,
        string settlementCultureId,
        string settlementId,
        string reason = null)
    {
        IsActive = isActive;
        Siege = siege;
        Battle = battle;
        InSettlement = inSettlement;
        InTavern = inTavern;
        StableCultureId = Normalize(stableCultureId);
        SettlementCultureId = Normalize(settlementCultureId);
        SettlementId = Normalize(settlementId);
        Reason = Normalize(reason);
    }

    public static MusicCampaignContextState Inactive { get; } =
        new MusicCampaignContextState(false, false, false, false, false, null, null, null, "inactive");

    public bool IsActive { get; }

    public bool Siege { get; }

    public bool Battle { get; }

    public bool InSettlement { get; }

    public bool InTavern { get; }

    public string StableCultureId { get; }

    public string SettlementCultureId { get; }

    public string SettlementId { get; }

    public string Reason { get; }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
