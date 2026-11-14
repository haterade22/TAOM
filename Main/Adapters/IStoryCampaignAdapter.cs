using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>The campaign reads and the one campaign write of a story offer (#1105 slice 4), behind ADR-007's boundary.</summary>
public interface IStoryCampaignAdapter
{
    /// <summary>True when the player's hero is a prisoner: no offer then.</summary>
    bool IsMainHeroPrisoner { get; }

    /// <summary>True when the main party is in a map event: the offer opens a menu, and only the battle menu may close that event.</summary>
    bool IsMainPartyInMapEvent { get; }

    /// <summary>The string id of the player's map faction (<c>Hero.MainHero.MapFaction</c>): a kingdom, or the player's clan outside one.</summary>
    string? PlayerKingdomId { get; }

    /// <summary>The campaign's <c>UniqueGameId</c>, empty with no campaign: it keys the check lines per campaign.</summary>
    string CampaignId { get; }

    /// <summary>
    /// A lost story mission: gives the settlement to the ruler of the first listed kingdom that exists and has a ruler, by
    /// <c>ChangeOwnerOfSettlementAction.ApplyByDefault</c>. The old owner's garrison is emptied and <c>LastCapturedBy</c> set to the
    /// taker. False with <paramref name="result"/> as the reason when nothing changed; true with the taker's kingdom id.
    /// </summary>
    bool GiveSettlement(string settlementId, IReadOnlyList<string> kingdomIds, out string result);
}
