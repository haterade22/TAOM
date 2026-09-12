using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// The engine-derived rules behind the siege participation record (#565), kept out of the campaign
/// behavior so it stays a thin entry point (ADR-002) and so each rule is testable on bare engine
/// objects. Boundary conversion over sealed types, not policy: the same split as
/// <see cref="FiefGrantFactsBuilder"/>. Each rule mirrors one engine site and says which.
/// </summary>
internal static class FiefSiegeCaptureRules
{
    /// <summary>
    /// The side whose victory captures the settlement, mirroring <c>KingdomManager.SiegeCompleted</c>'s
    /// battle-type gate (v1.4.8 KingdomManager.cs:238-240): an attacker victory in an assault, a
    /// defender (besieger) victory in a sally-out. <c>None</c> for anything that transfers no
    /// ownership, including <c>SiegeOutside</c>, a relief battle that captures nothing; vanilla's loot
    /// handler counts that one too, and mirroring it once wrote a record nothing ever cleared.
    /// </summary>
    public static BattleSideEnum SideThatCapturesOnVictory(MapEvent mapEvent)
    {
        if (mapEvent == null) return BattleSideEnum.None;
        if (mapEvent.IsSiegeAssault) return BattleSideEnum.Attacker;
        if (mapEvent.IsSallyOut || mapEvent.IsBlockadeSallyOut) return BattleSideEnum.Defender;
        return BattleSideEnum.None;
    }

    /// <summary>
    /// Mirrors the ballot, <c>SettlementClaimantDecision.DetermineInitialCandidates</c> (:231-243): a
    /// clan of the capturing faction that is not under mercenary service. Both halves matter because
    /// every share is measured against the top recorded clan: <c>SiegeEvent.CanPartyJoinSide</c> lets
    /// an allied faction's party join the assault, and neither an ally nor a mercenary company can be
    /// granted the fief, so neither may set the bar the real candidates are measured against.
    /// </summary>
    public static bool CanClaimFief(Clan clan, IFaction capturingFaction) =>
        clan != null && capturingFaction != null && !clan.IsUnderMercenaryService
        && clan.MapFaction == capturingFaction;

    /// <summary>
    /// Mirrors <c>SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged</c> (:39-42): vanilla
    /// opens a claim only for a fortification taken by a kingdom with more than one clan. No claim
    /// means no grant, and only the grant clears the record, so a capture that opens no claim must
    /// not leave a record behind for a relinquish or annexation election to read years later.
    /// </summary>
    public static bool WillOpenAClaim(IFaction newOwnerFaction, bool isFortification) =>
        isFortification && newOwnerFaction is Kingdom kingdom && kingdom.Clans.Count > 1;

    /// <summary>
    /// The clan a battle party fights for: <c>ActualClan</c>, else the party component's owner's clan.
    /// Never <c>PartyBase.Owner</c>, a throwing computed getter that an IL test bans repo-wide;
    /// <c>MobileParty.Owner</c> is a null-safe component read.
    /// </summary>
    public static Clan ClanOf(MapEventParty party)
    {
        var mobile = party?.Party?.MobileParty;
        return mobile?.ActualClan ?? mobile?.Owner?.Clan;
    }
}
