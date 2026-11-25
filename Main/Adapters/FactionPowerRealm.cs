using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <summary>
/// The key the faction power slider is stored under: the culture id of a kingdom, so one realm gets one value
/// whichever lord, party or fief is asked. A clan with no kingdom, bandits and minor factions have no realm.
/// </summary>
internal static class FactionPowerRealm
{
    internal static string? KeyOf(IFaction? mapFaction)
        => mapFaction?.IsKingdomFaction == true ? mapFaction.Culture?.StringId : null;
}
