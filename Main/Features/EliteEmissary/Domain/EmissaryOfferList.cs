using System.Collections.Generic;

namespace TAOM.Features.EliteEmissary.Domain;

/// <summary>The full set of elite troops a faction's emissary offers at a settlement, plus the
/// resource context (name/icon/balance) needed to render the purchase list. Built fresh per menu
/// open against the SETTLEMENT OWNER's faction so conquest flips the offerings automatically.</summary>
public sealed class EmissaryOfferList
{
    /// <summary>True when the owner faction maps to no special resource — nothing can be sold/priced.</summary>
    public bool NoResource { get; }

    public string ResourceId { get; }
    public string ResourceDisplayName { get; }
    public string IconSpriteName { get; }

    /// <summary>The player's current balance of the owner faction's resource (0 when NoResource).</summary>
    public float PlayerBalance { get; }

    public IReadOnlyList<EmissaryTroopOffer> Offers { get; }

    /// <summary>True in gold mode (#1109): <see cref="PlayerBalance"/> is the player's gold and every
    /// offer's price is in gold. The resource name and icon are null.</summary>
    public bool PaidInGold { get; }

    public bool HasOffers => Offers != null && Offers.Count > 0;

    private EmissaryOfferList(bool noResource, string resourceId, string resourceDisplayName,
        string iconSpriteName, float playerBalance, IReadOnlyList<EmissaryTroopOffer> offers, bool paidInGold = false)
    {
        PaidInGold = paidInGold;
        NoResource = noResource;
        ResourceId = resourceId;
        ResourceDisplayName = resourceDisplayName;
        IconSpriteName = iconSpriteName;
        PlayerBalance = playerBalance;
        Offers = offers ?? new List<EmissaryTroopOffer>();
    }

    public static EmissaryOfferList ForResource(string resourceId, string resourceDisplayName,
        string iconSpriteName, float playerBalance, IReadOnlyList<EmissaryTroopOffer> offers) =>
        new(false, resourceId, resourceDisplayName, iconSpriteName, playerBalance, offers);

    /// <summary>Gold mode: priced and funded in gold, so no resource context is carried.</summary>
    public static EmissaryOfferList ForGold(float playerGold, IReadOnlyList<EmissaryTroopOffer> offers) =>
        new(false, null, null, null, playerGold, offers, paidInGold: true);

    /// <summary>Owner faction has no special resource — the menu option hides (or shows disabled).</summary>
    public static readonly EmissaryOfferList NoResourceAvailable =
        new(true, null, null, null, 0f, new List<EmissaryTroopOffer>());
}
