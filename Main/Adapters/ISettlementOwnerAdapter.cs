using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>TAOM-owned snapshot of a settlement's current owner faction (ADR-007 — keeps sealed
/// <see cref="Settlement"/>/<c>Clan</c>/<c>Kingdom</c> out of services).</summary>
public sealed class SettlementOwnerInfo
{
    public string SettlementId { get; }

    /// <summary>Owner kingdom StringId, or null for rebel/minor-faction/unowned settlements.</summary>
    public string OwnerKingdomId { get; }

    /// <summary>Owner culture StringId (owner clan's culture, falling back to the settlement's
    /// intrinsic culture for unowned/transient state), or null if unresolvable.</summary>
    public string OwnerCultureId { get; }

    public SettlementOwnerInfo(string settlementId, string ownerKingdomId, string ownerCultureId)
    {
        SettlementId = settlementId;
        OwnerKingdomId = ownerKingdomId;
        OwnerCultureId = ownerCultureId;
    }
}

/// <summary>Resolves a settlement's current owner faction. <see cref="Settlement.OwnerClan"/> already
/// hops village→bound-town, so a village key-settlement is priced by its bound town's owner.</summary>
public interface ISettlementOwnerAdapter
{
    SettlementOwnerInfo GetOwnerInfo(Settlement settlement);
}
