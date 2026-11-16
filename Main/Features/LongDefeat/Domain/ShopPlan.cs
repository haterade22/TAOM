using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A pack the player can buy: <c>PackSize</c> soldiers of one troop for <c>Price</c> gold.</summary>
internal sealed record ShopOffer(string Id, string Name, string TroopId, FormationSlot Slot, int PackSize, int Price);

/// <summary>
/// One step of a troop line (#1161): a standing soldier of <c>FromTroop</c> becomes a soldier of <c>ToTroop</c> for <c>Price</c> gold. <c>Id</c> is <c>from&gt;to</c>; <c>Name</c> is the
/// raw <c>{=KEY}English</c> label, or the target troop id when shop.json gives none.
/// </summary>
internal sealed record UpgradeStep(string Id, string Name, string FromTroop, string ToTroop, int Price);

/// <summary>
/// A shop: the gold a run starts with, the packs it sells and, when shop.json carries them, its fortifications (null: none) and its Phase 4 places, the armoury and the engineer's yard
/// (null: neither key). <see cref="Upgrades"/> are the steps of its troop lines (#1161), empty when shop.json lists none.
/// </summary>
internal sealed record ShopPlan(int StartingGold, IReadOnlyList<ShopOffer> Offers, FortPlan? Fort = null, LocationsPlan? Locations = null)
{
    public IReadOnlyList<UpgradeStep> Upgrades { get; init; } = System.Array.Empty<UpgradeStep>();

    /// <summary>The named heroes the shop sells (#1325), in shop.json order; empty when it lists none.</summary>
    public IReadOnlyList<HeroOffer> Heroes { get; init; } = System.Array.Empty<HeroOffer>();
}

internal enum BuyResult
{
    Ok,
    NotOpen,
    UnknownOffer,
    NoGold,
    GarrisonFull,
    RunOver,
}

internal enum UpgradeResult
{
    Ok,
    NotOpen,
    UnknownStep,
    NoneStanding,
    NoGold,
    RunOver,
}

internal enum PickResult
{
    Ok,
    NotOpen,
    NoOffer,
    NotOffered,
    RunOver,
}

internal enum RerollResult
{
    Ok,
    NotOpen,
    NoOffer,
    NoReroll,
    RunOver,
}

internal enum StartResult
{
    Ok,
    NotOpen,
    RunOver,
}
