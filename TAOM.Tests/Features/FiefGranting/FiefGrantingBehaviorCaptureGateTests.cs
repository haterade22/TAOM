using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FiefGranting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// #565 review regressions on <see cref="FiefSiegeCaptureRules"/>, the engine-derived rules behind
/// the participation record. Each rule mirrors one engine site; each test stages bare engine
/// objects (uninitialized, fields set directly) so the rule runs without a campaign.
///
/// Deep-review gap 1: the write gate must mirror <c>KingdomManager.SiegeCompleted</c> (Siege,
/// SallyOut, BlockadeSallyOutBattle), not vanilla's wider loot handler; the first cut recorded a
/// SiegeOutside (relief) victory that captures nothing, and nothing ever cleared it.
/// Deep-review gap 2 and Codex F1: only clans that can appear on the ballot may set the top share:
/// not mercenaries, not an allied faction's party that joined the assault.
/// Codex F2: a capture that opens no claim (sole-clan kingdom, clan faction, village) gets no grant
/// and therefore no clearing, so its record must not be kept.
/// </summary>
[TestClass]
public class FiefGrantingBehaviorCaptureGateTests
{
    // ---------------------------------------------------------------- which battles capture

    private static BattleSideEnum SideFor(MapEvent.BattleTypes battleType)
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        var typeField = typeof(MapEvent).GetField("_mapEventType", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(typeField, "MapEvent._mapEventType is gone; the IsSiegeAssault/IsSallyOut flags moved.");
        typeField!.SetValue(mapEvent, battleType);
        return FiefSiegeCaptureRules.SideThatCapturesOnVictory(mapEvent);
    }

    [TestMethod]
    public void SideThatCapturesOnVictory_Assault_IsTheAttacker()
    {
        Assert.AreEqual(BattleSideEnum.Attacker, SideFor(MapEvent.BattleTypes.Siege));
    }

    [DataTestMethod]
    [DataRow(MapEvent.BattleTypes.SallyOut)]
    [DataRow(MapEvent.BattleTypes.BlockadeSallyOutBattle)]
    public void SideThatCapturesOnVictory_SallyOut_IsTheBesiegingDefenderSide(MapEvent.BattleTypes battleType)
    {
        // The garrison sallies as the attacker side; the besiegers are the defender side, and their
        // victory is what KingdomManager.SiegeCompleted turns into a capture.
        Assert.AreEqual(BattleSideEnum.Defender, SideFor(battleType));
    }

    [TestMethod]
    public void SideThatCapturesOnVictory_ReliefBattleOutsideTheWalls_CapturesNothing()
    {
        // KingdomManager.SiegeCompleted returns early for SiegeOutside: beating a relief force does
        // not take the settlement, so no record may be written (the gap 1 phantom record).
        Assert.AreEqual(BattleSideEnum.None, SideFor(MapEvent.BattleTypes.SiegeOutside));
    }

    [DataTestMethod]
    [DataRow(MapEvent.BattleTypes.None)]
    [DataRow(MapEvent.BattleTypes.FieldBattle)]
    [DataRow(MapEvent.BattleTypes.Raid)]
    [DataRow(MapEvent.BattleTypes.IsForcingVolunteers)]
    [DataRow(MapEvent.BattleTypes.IsForcingSupplies)]
    [DataRow(MapEvent.BattleTypes.Hideout)]
    [DataRow(MapEvent.BattleTypes.BlockadeBattle)]
    public void SideThatCapturesOnVictory_AnyOtherBattle_CapturesNothing(MapEvent.BattleTypes battleType)
    {
        Assert.AreEqual(BattleSideEnum.None, SideFor(battleType));
    }

    [TestMethod]
    public void SideThatCapturesOnVictory_NullEvent_CapturesNothing()
    {
        Assert.AreEqual(BattleSideEnum.None, FiefSiegeCaptureRules.SideThatCapturesOnVictory(null!));
    }

    // ---------------------------------------------------------------- which clans may claim

    /// <summary>An uninitialized clan has no kingdom, so Clan.MapFaction returns the clan itself:
    /// the clan IS its own faction, which is enough to stage same-faction and foreign cases.</summary>
    private static Clan BareClan(bool underMercenaryService = false)
    {
        var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        var setter = typeof(Clan).GetProperty("IsUnderMercenaryService")?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, "Clan.IsUnderMercenaryService lost its setter; the test cannot stage a mercenary clan.");
        setter!.Invoke(clan, new object[] { underMercenaryService });
        return clan;
    }

    [TestMethod]
    public void CanClaimFief_MercenaryClan_IsNotRecorded()
    {
        var clan = BareClan(underMercenaryService: true);
        Assert.IsFalse(FiefSiegeCaptureRules.CanClaimFief(clan, clan.MapFaction));
    }

    [TestMethod]
    public void CanClaimFief_VassalClanOfTheCapturingFaction_IsRecorded()
    {
        var clan = BareClan();
        Assert.IsTrue(FiefSiegeCaptureRules.CanClaimFief(clan, clan.MapFaction));
    }

    [TestMethod]
    public void CanClaimFief_ClanOfAnotherFaction_IsNotRecorded()
    {
        // Codex F1 (#565): SiegeEvent.CanPartyJoinSide lets a party of any faction at war with the
        // defenders and at peace with the besiegers join the assault, but DetermineInitialCandidates
        // enumerates the capturing kingdom's clans only. An allied outsider that out-fought the
        // field must not set the top share the real candidates are measured against.
        var ally = BareClan();
        var capturer = BareClan();
        Assert.IsFalse(FiefSiegeCaptureRules.CanClaimFief(ally, capturer.MapFaction));
    }

    [TestMethod]
    public void CanClaimFief_NoClanOrNoCapturingFaction_IsNotRecorded()
    {
        var clan = BareClan();
        Assert.IsFalse(FiefSiegeCaptureRules.CanClaimFief(null!, clan.MapFaction));
        Assert.IsFalse(FiefSiegeCaptureRules.CanClaimFief(clan, null!));
    }

    // ---------------------------------------------------------------- when vanilla opens a claim

    private static Kingdom BareKingdom(int clanCount)
    {
        var kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
        var clans = new MBList<Clan>();
        for (var i = 0; i < clanCount; i++)
            clans.Add(BareClan());
        var field = typeof(Kingdom).GetField("_clans", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "Kingdom._clans is gone; Kingdom.Clans moved and the test cannot stage a kingdom.");
        field!.SetValue(kingdom, clans);
        return kingdom;
    }

    [TestMethod]
    public void WillOpenAClaim_KingdomWithSeveralClans_OnAFortification_IsTrue()
    {
        Assert.IsTrue(FiefSiegeCaptureRules.WillOpenAClaim(BareKingdom(2), isFortification: true));
    }

    [TestMethod]
    public void WillOpenAClaim_SoleClanKingdom_IsFalse()
    {
        // Codex F2 (#565): SettlementClaimantCampaignBehavior never flags the town unassigned for a
        // one-clan kingdom, so no grant ever clears the record; it must not be kept.
        Assert.IsFalse(FiefSiegeCaptureRules.WillOpenAClaim(BareKingdom(1), isFortification: true));
    }

    [TestMethod]
    public void WillOpenAClaim_ClanFactionWithoutAKingdom_IsFalse()
    {
        Assert.IsFalse(FiefSiegeCaptureRules.WillOpenAClaim(BareClan().MapFaction, isFortification: true));
    }

    [TestMethod]
    public void WillOpenAClaim_NotAFortification_IsFalse()
    {
        Assert.IsFalse(FiefSiegeCaptureRules.WillOpenAClaim(BareKingdom(2), isFortification: false));
    }

    [TestMethod]
    public void WillOpenAClaim_NoFaction_IsFalse()
    {
        Assert.IsFalse(FiefSiegeCaptureRules.WillOpenAClaim(null!, isFortification: true));
    }

    // ---------------------------------------------------------------- clan lookup

    [TestMethod]
    public void ClanOf_NullOrSettlementParty_IsNull()
    {
        // A settlement (non-mobile) party has no MobileParty; the rule must return null without
        // touching PartyBase.Owner, the throwing getter the IL ban test forbids.
        Assert.IsNull(FiefSiegeCaptureRules.ClanOf(null!));
        var party = (MapEventParty)FormatterServices.GetUninitializedObject(typeof(MapEventParty));
        Assert.IsNull(FiefSiegeCaptureRules.ClanOf(party));
    }
}
