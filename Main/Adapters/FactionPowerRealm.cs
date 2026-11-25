using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>
/// The key the faction power slider is stored under: the culture id of a kingdom, so one realm gets one value
/// whichever lord, party or fief is asked. A clan with no kingdom, bandits and minor factions have no realm.
/// </summary>
internal static class FactionPowerRealm
{
    internal static string? KeyOf(IFaction? mapFaction)
        => mapFaction?.IsKingdomFaction == true ? mapFaction.Culture?.StringId : null;

    /// <summary>
    /// The clan whose side a party fights or pays for: the fief owner's for a garrison or militia, the party's own clan
    /// otherwise. The player-clan rule asks this clan.
    /// </summary>
    internal static Clan? OwnerClanOf(MobileParty? party)
    {
        if (party == null)
            return null;
        if (party.IsGarrison || party.IsMilitia)
            return (party.HomeSettlement ?? party.CurrentSettlement)?.OwnerClan;
        return party.ActualClan;
    }
}
