using System.Collections.Generic;
using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Features.SupplyLines;

/// <summary>One place an order can be sourced from, flattened for the UI. Pure data.</summary>
public sealed class SupplySourceInfo
{
    /// <summary>Settlement StringId, or null for a lord source.</summary>
    public string SettlementId;

    /// <summary>Lord Hero StringId, or null for a settlement source.</summary>
    public string HeroId;

    public string DisplayName;

    /// <summary>Localised relation tag shown beside the name (own / allied / neutral / lord).</summary>
    public string RelationText;

    public float Distance;

    /// <summary>False when visible but not orderable (at war, besieged, no owner); reason set.</summary>
    public bool CanOrder;

    public string DisabledReason;
}

/// <summary>A purchasable line (good or troop) at a source. Pure data.</summary>
public sealed class SupplyLineItem
{
    /// <summary>ItemObject or CharacterObject StringId.</summary>
    public string Id;

    public string Name;

    public int Available;

    /// <summary>Unit price in denars (already tier/war-scaled for troops).</summary>
    public int UnitPrice;
}

/// <summary>
/// Everything the order screen and the order service need to know about sources: who is eligible,
/// what they stock, and the consumption that happens on confirm. This is the boundary service that
/// touches campaign state; the enumerations it returns are pure data.
///
/// <para>Two rules the source module broke, enforced here:
/// goods ARE deducted from the settlement roster on consume (the source conjured them, a dupe);
/// volunteers go through <c>Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero</c>
/// and honour TAOM's alignment gate (-1 = recruit nothing from this notable).</para>
/// </summary>
public interface ISupplySourceService
{
    /// <summary>Eligible settlements (distance-sorted) then friendly lords.</summary>
    IReadOnlyList<SupplySourceInfo> GetSources();

    /// <summary>Food goods in the source's market, best 14 by unit price. Empty for lords.</summary>
    IReadOnlyList<SupplyLineItem> GetGoods(SupplySourceInfo source);

    /// <summary>Recruitable troops: settlement volunteers (alignment-gated) or the lord's roster.</summary>
    IReadOnlyList<SupplyLineItem> GetTroops(SupplySourceInfo source);

    /// <summary>Map distance from the source to the main party.</summary>
    float DistanceToPlayer(SupplySourceInfo source);

    /// <summary>
    /// Takes the purchased goods and troops OUT of the source (settlement roster / notables'
    /// volunteer slots / lord roster). Returns what was actually obtained, which may be less than
    /// requested if stock moved since the screen was built; the caller prices what it got.
    /// </summary>
    SupplyConsumption Consume(SupplySourceInfo source, IReadOnlyDictionary<string, int> goods, IReadOnlyDictionary<string, int> troops);
}

/// <summary>What a consume actually obtained, with the market value it computed while taking it.</summary>
public sealed class SupplyConsumption
{
    public Dictionary<string, int> Goods = new Dictionary<string, int>();
    public Dictionary<string, int> Troops = new Dictionary<string, int>();
    public float GoodsMarketValue;
    public int TroopRecruitCost;
}
