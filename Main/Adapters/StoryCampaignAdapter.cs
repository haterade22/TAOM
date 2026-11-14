using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// Production <see cref="IStoryCampaignAdapter"/> (#1105 slice 4). The transfer is <c>ApplyByDefault</c>, never <c>ApplyBySiege</c>:
/// BySiege destroys the garrison through <c>capturerHero.PartyBelongedTo.Party</c> (ChangeOwnerOfSettlementAction.cs:34-37, v1.5.7),
/// which throws for a ruler with no party, and it opens a fief claim. Default keeps the old garrison party (:38-41), so its roster is
/// emptied here and the militia set to 0, as vanilla's rebellion transfer does: the defenders fell with the story battle. The
/// owner-change listeners this call reaches, TAOM's and vanilla's, are listed in docs/features/story-missions.md "The story offer".
/// </summary>
public sealed class StoryCampaignAdapter : IStoryCampaignAdapter
{
    public bool IsMainHeroPrisoner => Hero.MainHero?.IsPrisoner ?? false;

    public bool IsMainPartyInMapEvent => MobileParty.MainParty?.MapEvent != null;

    public string? PlayerKingdomId => Hero.MainHero?.MapFaction?.StringId;

    public string CampaignId => Campaign.Current?.UniqueGameId ?? string.Empty;

    public bool GiveSettlement(string settlementId, IReadOnlyList<string> kingdomIds, out string result)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement?.Town == null)
        {
            result = $"{settlementId} is not a town or castle";
            return false;
        }

        foreach (var kingdomId in kingdomIds)
        {
            var kingdom = Kingdom.All.FirstOrDefault(k => string.Equals(k.StringId, kingdomId, StringComparison.Ordinal));
            var ruler = kingdom?.RulingClan?.Leader;
            if (kingdom == null || kingdom.IsEliminated || ruler == null || !ruler.IsAlive)
                continue;

            // The engine's "who holds this" (lessons, campaign-mechanics.md: a fief's political owner is MapFaction).
            if (settlement.MapFaction == kingdom)
            {
                result = kingdomId;
                return true;
            }

            ChangeOwnerOfSettlementAction.ApplyByDefault(ruler, settlement);
            settlement.Town.GarrisonParty?.MemberRoster.Clear();
            settlement.Militia = 0f;
            settlement.Town.LastCapturedBy = ruler.Clan;
            result = kingdomId;
            return true;
        }

        result = $"none of {string.Join(", ", kingdomIds)} exists with a living ruler";
        return false;
    }
}
