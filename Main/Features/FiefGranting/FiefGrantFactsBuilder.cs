using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// Boundary conversion for the fief-grant election (#458): sealed TaleWorlds types in, primitives
/// out. Extracted from <see cref="TaomSettlementClaimantDecision"/> so that class stays a thin entry
/// point (ADR-002).
///
/// This deliberately does NOT live on <see cref="IFiefGrantPolicyService"/>. Moving the counting into
/// the service would mean passing <c>Clan</c> and <c>Settlement</c> across the service boundary,
/// which ADR-007 forbids outright. Counting is conversion, not policy, so it belongs here at the
/// boundary, and the service keeps taking primitives only.
/// </summary>
internal static class FiefGrantFactsBuilder
{
    /// <summary>
    /// Snapshot one candidate clan for <paramref name="contested"/>, the settlement under vote.
    /// </summary>
    public static FiefGrantCandidateFacts Build(
        Clan clan, Settlement contested, IFiefSiegeParticipationService participation)
    {
        if (clan == null) return default;

        var kingdom = clan.Kingdom;

        var isRulingClan = kingdom?.Leader != null
                           && clan.Leader != null
                           && clan.Leader == kingdom.Leader;

        // Participation comes from TAOM's own record of the winning assault (#565), never from
        // Town.LastCapturedBy: that stamp names the assault leader's clan only, is never cleared,
        // and vanilla's merit already pays it a flat +30 that this override leaves in place.
        var settlementId = contested?.StringId;
        var recorded = participation != null && participation.HasRecord(settlementId);
        var share = recorded ? participation.GetContributionShare(settlementId, clan.StringId) : 0f;

        var settlementCulture = contested?.Culture;
        var isCultureMatch = settlementCulture != null
                             && clan.Culture != null
                             && clan.Culture == settlementCulture;

        return new FiefGrantCandidateFacts(
            CountFortifications(clan.Settlements, contested),
            isRulingClan,
            share,
            recorded,
            isCultureMatch,
            clan == Clan.PlayerClan);
    }

    /// <summary>Fortifications a clan holds, excluding the contested one.</summary>
    public static int CountClanFortifications(Clan clan, Settlement contested) =>
        CountFortifications(clan?.Settlements, contested);

    /// <summary>Fortifications a kingdom holds, excluding the contested one.</summary>
    public static int CountKingdomFortifications(Kingdom kingdom, Settlement contested) =>
        CountFortifications(kingdom?.Settlements, contested);

    /// <summary>
    /// Both <c>Clan.Settlements</c> and <c>Kingdom.Settlements</c> include bound villages, so the
    /// <c>IsFortification</c> filter is load-bearing rather than defensive. The contested settlement
    /// is excluded on every call: vanilla's own balance divisor excludes it (<c>Settlement == item</c>),
    /// and after <c>ApplyBySiege</c> the king already holds it, so counting it would penalise him for
    /// a fief nobody has been granted yet. Excluding it on both sides of the King's Vote share keeps
    /// that ratio measuring fiefs already settled.
    /// </summary>
    private static int CountFortifications(
        MBReadOnlyList<Settlement> settlements, Settlement contested)
    {
        if (settlements == null) return 0;

        var count = 0;
        for (var i = 0; i < settlements.Count; i++)
        {
            var settlement = settlements[i];
            if (settlement != null && settlement.IsFortification && settlement != contested)
                count++;
        }

        return count;
    }
}
