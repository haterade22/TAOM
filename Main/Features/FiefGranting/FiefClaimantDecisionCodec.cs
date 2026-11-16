using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// The engine side of <see cref="FiefClaimantWire"/>: turns a TAOM fief election into Coop's payload on
/// the host and back into one on a client (#1346). TAOM.CoopCompat registers these two methods with
/// Coop's <c>KingdomDecisionTypeRegistry</c>; nothing else calls them. The pure encoding and its checks
/// are in <see cref="FiefClaimantWire"/>, which the tests cover; this class only looks objects up.
/// </summary>
public static class FiefClaimantDecisionCodec
{
    public const string SentCheck = "taom.coopcompat.fief-election.sent";
    public const string ReadCheck = "taom.coopcompat.fief-election.read";

    /// <summary>Host: the ids of the decision.</summary>
    public static byte[] Write(TaomSettlementClaimantDecision decision)
    {
        var settlementId = decision.Settlement?.StringId;
        Outcomes.Pass(SentCheck, "", "settlement=" + settlementId);
        return FiefClaimantWire.Encode(new FiefClaimantWireData(
            settlementId, decision.ProposerClan?.StringId, decision.ClanToExclude?.StringId));
    }

    /// <summary>
    /// Client: the decision. Null when the payload or an id does not resolve; Coop then reports the
    /// decision as unreadable. Coop sets the proposer, kingdom, trigger time and flags on the result.
    /// </summary>
    public static TaomSettlementClaimantDecision Read(byte[] payload)
    {
        if (!FiefClaimantWire.TryDecode(payload, out var data))
        {
            Outcomes.Fail(ReadCheck, "payload", "reason=payload");
            return null;
        }

        var settlement = Settlement.Find(data.SettlementId);
        var proposer = Clan.FindFirst(c => c.StringId == data.ProposerClanId);
        var excluded = data.ClanToExcludeId == null ? null : Clan.FindFirst(c => c.StringId == data.ClanToExcludeId);
        if (settlement == null || proposer == null || (data.ClanToExcludeId != null && excluded == null))
        {
            Outcomes.Fail(ReadCheck, data.SettlementId, "reason=unknown-id settlement=" + data.SettlementId);
            return null;
        }

        Outcomes.Pass(ReadCheck, "", "settlement=" + data.SettlementId);
        return new TaomSettlementClaimantDecision(proposer, settlement, null, excluded);
    }
}
