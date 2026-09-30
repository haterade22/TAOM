using System.Collections.Generic;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The campaign state the realm borders draw from: every town and castle with its villages, whose
/// realm holds each, how each realm stands towards the player, and where the player is. A realm is
/// the owner's map faction: a kingdom's string id, or "clan:" and the clan's id for a clan outside
/// any kingdom (a rebellion, an independent player), so rebels show as a realm of their own.
/// </summary>
public interface IRealmMapAdapter
{
    /// <summary>Every town and castle, in a stable order, with the positions of its bound villages.</summary>
    IReadOnlyList<FiefSite> GetFiefSites();

    /// <summary>The realm holding the fief, or null when the fief is unknown or has no owner.</summary>
    string? RealmOf(string fiefId);

    /// <summary>The player's realm: their kingdom, or their own clan outside one.</summary>
    string? PlayerRealm { get; }

    /// <summary>The realm's standing towards the player, by the settlement nameplates' own rule.</summary>
    RealmRelation RelationToPlayer(string realm);

    /// <summary>The realm's culture id, for the alignment map mode's fallback; null for an unknown realm.</summary>
    string? CultureOfRealm(string realm);

    /// <summary>The realm's name in the player's language, or the key when it is unknown.</summary>
    string RealmName(string realm);

    /// <summary>The player's party on the map; false when there is none or its position is not finite.</summary>
    bool TryGetMainPartyPosition(out float x, out float y);

    /// <summary>Campaign time in hours.</summary>
    double CampaignHours { get; }
}
