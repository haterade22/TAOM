using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Adapters;

/// <summary>The campaign reads and the one campaign write of a story offer (#1105 slice 4), behind ADR-007's boundary.</summary>
public interface IStoryCampaignAdapter
{
    /// <summary>True when the player's hero is a prisoner: no offer then.</summary>
    bool IsMainHeroPrisoner { get; }

    /// <summary>True when the main party is in a map event: the offer opens a menu, and only the battle menu may close that event.</summary>
    bool IsMainPartyInMapEvent { get; }

    /// <summary>
    /// The player's clan map faction (his kingdom, or his clan outside one) with that faction's culture (<c>FactionSides.OfPlayer</c>):
    /// its id is matched against a story window's kingdoms, and the alignment table resolves the pair to his side of the war (#1326).
    /// <see cref="SideIds.None"/> with no campaign.
    /// </summary>
    SideIds PlayerSide { get; }

    /// <summary>The campaign's <c>UniqueGameId</c>, empty with no campaign: it keys the check lines per campaign.</summary>
    string CampaignId { get; }

    /// <summary>
    /// Fallen walls (a lost defence, or a won attack): gives the settlement to the ruler of the first listed kingdom that exists and has a ruler, by
    /// <c>ChangeOwnerOfSettlementAction.ApplyByDefault</c>. The old owner's garrison is emptied and <c>LastCapturedBy</c> set to the
    /// taker. False with <paramref name="result"/> as the reason when nothing changed; true with the taker's kingdom id.
    /// </summary>
    bool GiveSettlement(string settlementId, IReadOnlyList<string> kingdomIds, out string result);
}
