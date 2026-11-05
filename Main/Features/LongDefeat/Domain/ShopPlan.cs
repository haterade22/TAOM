using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A pack the player can buy: <c>PackSize</c> soldiers of one troop for <c>Price</c> gold.</summary>
internal sealed record ShopOffer(string Id, string Name, string TroopId, FormationSlot Slot, int PackSize, int Price);

internal sealed record ShopPlan(int StartingGold, IReadOnlyList<ShopOffer> Offers);

internal enum BuyResult
{
    Ok,
    NotOpen,
    UnknownOffer,
    NoGold,
    GarrisonFull,
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
