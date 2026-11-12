using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Single-player behaviour of the earning paths, moved out of SpecialResourcesBehavior verbatim (#967 C1).
/// No session, authority, not dedicated, unless a test says otherwise.
/// </summary>
[TestClass]
public class SpecialResourceEarningServiceCharacterizationTests
{
    private static VictorySnapshot Win(int enemy = 30, bool siege = false) => new()
    {
        State = "AttackerVictory",
        IsPlayerMapEvent = true,
        PlayerSide = BattleSideEnum.Attacker,
        WinningSide = BattleSideEnum.Attacker,
        EventType = MapEvent.BattleTypes.FieldBattle,
        IsSiege = siege,
        BeatAFieldParty = true,
        EnemyCount = enemy,
        LocalHero = EarningTestKit.Local(),
    };

    private static RaidOrHideoutSnapshot Raid() => new()
    {
        Side = BattleSideEnum.Attacker,
        IsPlayerMapEvent = true,
        PlayerSide = BattleSideEnum.Attacker,
        VillageLooted = true,
        IsFinalized = true,
        LocalHero = EarningTestKit.Local(),
    };

    private static void AssertNoBattleOrSiegePay(EarningTestKit kit)
    {
        kit.Service.DidNotReceiveWithAnyArgs().EarnFromBattle(default, default, default, default);
        kit.Service.DidNotReceiveWithAnyArgs().EarnFromSiege(default, default, default);
    }

    // --- Victory ---

    [TestMethod]
    public void OnVictory_AttackerWinsFieldBattle_EarnsFromBattleWithEnemyCount()
    {
        var kit = new EarningTestKit();

        // The pay follows the enemy's size alone.
        kit.Sut.OnVictory(Win(enemy: 30));

        kit.Service.Received(1).EarnFromBattle("main", "k", "c", 30f);
        kit.Service.DidNotReceiveWithAnyArgs().EarnFromSiege(default, default, default);
    }

    [TestMethod]
    public void OnVictory_Siege_EarnsFromSiegeNotBattle()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnVictory(Win(siege: true));

        kit.Service.Received(1).EarnFromSiege("main", "k", "c");
        kit.Service.DidNotReceiveWithAnyArgs().EarnFromBattle(default, default, default, default);
    }

    [TestMethod]
    public void OnVictory_PlayerOnTheLosingSide_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Win();
        s.PlayerSide = BattleSideEnum.Defender;

        kit.Sut.OnVictory(s);

        AssertNoBattleOrSiegePay(kit);
    }

    [TestMethod]
    public void OnVictory_PlayerSideNone_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Win();
        s.PlayerSide = BattleSideEnum.None;

        kit.Sut.OnVictory(s);

        AssertNoBattleOrSiegePay(kit);
    }

    [TestMethod]
    public void OnVictory_WinnerUnresolved_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Win();
        s.WinningSide = BattleSideEnum.None;

        kit.Sut.OnVictory(s);

        AssertNoBattleOrSiegePay(kit);
    }

    [TestMethod]
    public void OnVictory_NotAPlayerMapEvent_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Win();
        s.IsPlayerMapEvent = false;

        kit.Sut.OnVictory(s);

        AssertNoBattleOrSiegePay(kit);
    }

    [TestMethod]
    public void OnVictory_RaidEventWithoutAFieldParty_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Win();
        s.EventType = MapEvent.BattleTypes.Raid;
        s.BeatAFieldParty = false;

        kit.Sut.OnVictory(s);

        AssertNoBattleOrSiegePay(kit);
    }

    [TestMethod]
    public void OnVictory_RaidEventThatBeatAFieldParty_Earns()
    {
        var kit = new EarningTestKit();
        var s = Win();
        s.EventType = MapEvent.BattleTypes.Raid;

        kit.Sut.OnVictory(s);

        kit.Service.Received(1).EarnFromBattle("main", "k", "c", Arg.Any<float>());
    }

    [TestMethod]
    public void OnVictory_NoEnemies_PassesZero()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnVictory(Win(enemy: 0));

        kit.Service.Received(1).EarnFromBattle("main", "k", "c", 0f);
    }

    [TestMethod]
    public void OnVictory_DedicatedServer_EarnsNothing()
    {
        var kit = new EarningTestKit(dedicated: true);

        kit.Sut.OnVictory(Win());

        AssertNoBattleOrSiegePay(kit);
    }

    [TestMethod]
    public void OnVictory_DedicatedServerTwice_LogsTheSuppressionOnce()
    {
        var kit = new EarningTestKit(dedicated: true);

        kit.Sut.OnVictory(Win());
        kit.Sut.OnVictory(Win());

        kit.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Dedicated server")));
    }

    // --- Raid ---

    [TestMethod]
    public void OnRaid_LootedVillageAttackerWins_EarnsFromRaid()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnRaid(Raid());

        kit.Service.Received(1).EarnFromRaid("main", "k", "c");
    }

    [TestMethod]
    public void OnRaid_DefenderSideWon_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Raid();
        s.Side = BattleSideEnum.Defender;

        kit.Sut.OnRaid(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromRaid(default, default, default);
    }

    [TestMethod]
    public void OnRaid_NotAPlayerMapEvent_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Raid();
        s.IsPlayerMapEvent = false;

        kit.Sut.OnRaid(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromRaid(default, default, default);
    }

    [TestMethod]
    public void OnRaid_VillageNotLooted_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Raid();
        s.VillageLooted = false;

        kit.Sut.OnRaid(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromRaid(default, default, default);
    }

    [TestMethod]
    public void OnRaid_PlayerWasOnTheOtherSide_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Raid();
        s.PlayerSide = BattleSideEnum.Defender;

        kit.Sut.OnRaid(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromRaid(default, default, default);
    }

    [TestMethod]
    public void OnRaid_DedicatedServer_EarnsNothing()
    {
        var kit = new EarningTestKit(dedicated: true);

        kit.Sut.OnRaid(Raid());

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromRaid(default, default, default);
    }

    // --- Hideout ---

    [TestMethod]
    public void OnHideout_FinalizedAttackerWin_EarnsFromHideout()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnHideout(Raid());

        kit.Service.Received(1).EarnFromHideout("main", "k", "c");
    }

    [TestMethod]
    public void OnHideout_NotYetFinalized_EarnsNothing()
    {
        // The send-troops clear raises the event twice; only the finalized one pays (#790).
        var kit = new EarningTestKit();
        var s = Raid();
        s.IsFinalized = false;

        kit.Sut.OnHideout(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromHideout(default, default, default);
    }

    [TestMethod]
    public void OnHideout_DefenderWon_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var s = Raid();
        s.Side = BattleSideEnum.Defender;

        kit.Sut.OnHideout(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromHideout(default, default, default);
    }

    [TestMethod]
    public void OnHideout_NotPlayerEvent_EarnsNothing()
    {
        var kit = new EarningTestKit();
        var notPlayer = Raid();
        notPlayer.IsPlayerMapEvent = false;

        kit.Sut.OnHideout(notPlayer);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromHideout(default, default, default);
    }

    [TestMethod]
    public void OnHideout_PlayerSideDoesNotMatter_StillEarns()
    {
        // Parity: hideout, unlike raid, never asked which side the player stood on.
        var kit = new EarningTestKit();
        var s = Raid();
        s.PlayerSide = BattleSideEnum.Defender;

        kit.Sut.OnHideout(s);

        kit.Service.Received(1).EarnFromHideout("main", "k", "c");
    }

    [TestMethod]
    public void OnHideout_DedicatedServer_EarnsNothing()
    {
        var kit = new EarningTestKit(dedicated: true);

        kit.Sut.OnHideout(Raid());

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromHideout(default, default, default);
    }

    // --- Tournament ---

    [TestMethod]
    public void OnTournament_LocalWinner_EarnsFromTournament()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnTournament(EarningTestKit.Local());

        kit.Service.Received(1).EarnFromTournament("main", "k", "c");
    }

    [TestMethod]
    public void OnTournament_NoWinnerHero_EarnsNothing()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnTournament(null);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromTournament(default, default, default);
    }

    [TestMethod]
    public void OnTournament_DedicatedServer_EarnsNothing()
    {
        var kit = new EarningTestKit(dedicated: true);

        kit.Sut.OnTournament(EarningTestKit.Local());

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromTournament(default, default, default);
    }

    // --- Prisoners ---

    [TestMethod]
    public void OnPrisoners_ThreeTaken_EarnsFromPrisoners()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnPrisoners(EarningTestKit.Local(), 3);

        kit.Service.Received(1).EarnFromPrisoners("main", "k", "c", 3);
    }

    [TestMethod]
    public void OnPrisoners_NoneTaken_EarnsNothing()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnPrisoners(EarningTestKit.Local(), 0);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromPrisoners(default, default, default, default);
    }

    [TestMethod]
    public void OnPrisoners_DedicatedServer_EarnsNothing()
    {
        var kit = new EarningTestKit(dedicated: true);

        kit.Sut.OnPrisoners(EarningTestKit.Local(), 3);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromPrisoners(default, default, default, default);
    }

    // --- Notices ---

    [TestMethod]
    public void OnTournament_BalanceRose_ReturnsALocalNoticeForTheDifference()
    {
        var kit = new EarningTestKit();
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(5f, 12f);

        var notices = kit.Sut.OnTournament(EarningTestKit.Local());

        Assert.AreEqual(1, notices.Count);
        Assert.AreEqual(7f, notices[0].Earned);
        Assert.AreEqual("War Spoils", notices[0].ResourceName);
        Assert.AreEqual("tournament", notices[0].Source);
        Assert.IsTrue(notices[0].IsLocal);
    }

    [TestMethod]
    public void OnTournament_BalanceUnchanged_ReturnsNoNotice()
    {
        var kit = new EarningTestKit();
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(5f);

        Assert.AreEqual(0, kit.Sut.OnTournament(EarningTestKit.Local()).Count);
    }

    [TestMethod]
    public void OnTournament_BalanceFell_ReturnsNoNotice()
    {
        var kit = new EarningTestKit();
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(9f, 4f);

        Assert.AreEqual(0, kit.Sut.OnTournament(EarningTestKit.Local()).Count);
    }

    [TestMethod]
    public void OnTournament_NaNBalanceAfter_ReturnsNoNotice()
    {
        // Moved gate: earned > 0f is false for NaN, so a poisoned balance stays silent.
        var kit = new EarningTestKit();
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(5f, float.NaN);

        Assert.AreEqual(0, kit.Sut.OnTournament(EarningTestKit.Local()).Count);
    }

    [TestMethod]
    public void OnTournament_HeroMapsToNoResource_ReturnsNoNotice()
    {
        var kit = new EarningTestKit();
        kit.Service.ResolveResource(Arg.Any<string>(), Arg.Any<string>()).Returns((SpecialResource)null);
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(1f, 9f);

        Assert.AreEqual(0, kit.Sut.OnTournament(EarningTestKit.Local()).Count);
    }

    // --- Seeding ---

    [TestMethod]
    public void SeedIfUntracked_UntrackedWithStartingAmount_WritesTheStartingAmount()
    {
        var kit = new EarningTestKit();
        kit.Storage.Contains("main", "war_spoils").Returns(false);

        kit.Sut.SeedIfUntracked("main", "k", "c", "legacy save (Mike)");

        kit.Storage.Received(1).Set("main", "war_spoils", 10f);
    }

    [TestMethod]
    public void SeedIfUntracked_UntrackedWithZeroStart_RecordsAZero()
    {
        var kit = new EarningTestKit();
        kit.Service.ResolveResource("k", "c").Returns(new SpecialResource(
            "gems", new[] { "k" }, new[] { "c" }, "Gems", "icon", 500f, 0f, 1f, 1f, 1f, 1f, 1f));

        kit.Sut.SeedIfUntracked("main", "k", "c", "legacy save (Mike)");

        kit.Storage.Received(1).Set("main", "gems", 0f);
    }

    [TestMethod]
    public void SeedIfUntracked_AlreadyTracked_WritesNothing()
    {
        var kit = new EarningTestKit();
        kit.Storage.Contains("main", "war_spoils").Returns(true);

        kit.Sut.SeedIfUntracked("main", "k", "c", "legacy save (Mike)");

        kit.Storage.DidNotReceiveWithAnyArgs().Set(default, default, default);
    }

    [TestMethod]
    public void SeedIfUntracked_NoResource_WritesNothing()
    {
        var kit = new EarningTestKit();
        kit.Service.ResolveResource(Arg.Any<string>(), Arg.Any<string>()).Returns((SpecialResource)null);

        kit.Sut.SeedIfUntracked("main", "k", "c", "legacy save (Mike)");

        kit.Storage.DidNotReceiveWithAnyArgs().Set(default, default, default);
    }

    // --- Recruit payer ---

    [TestMethod]
    public void ChargeRecruit_SinglePlayer_ChargesLocalAndReturnsANotice()
    {
        var kit = new EarningTestKit();
        kit.Service.ChargeRecruitCost("main", "k", "c", "elephant", 2).Returns(6f);

        var notice = kit.Sut.ChargeRecruit(new RecruitContext(null, null, EarningTestKit.Local()), "elephant", 2);

        Assert.IsNotNull(notice);
        Assert.AreEqual(6f, notice.Charged);
        Assert.AreEqual("War Spoils", notice.ResourceName);
    }

    [TestMethod]
    public void ChargeRecruit_SinglePlayer_SeedsNothing()
    {
        var kit = new EarningTestKit();
        kit.Service.ChargeRecruitCost("main", "k", "c", "elephant", 2).Returns(6f);

        kit.Sut.ChargeRecruit(new RecruitContext(null, null, EarningTestKit.Local()), "elephant", 2);

        kit.Storage.DidNotReceiveWithAnyArgs().Contains(default, default);
        kit.Storage.DidNotReceiveWithAnyArgs().Set(default, default, default);
    }

    [TestMethod]
    public void ChargeRecruit_ChargedNothing_ReturnsNoNotice()
    {
        var kit = new EarningTestKit();
        kit.Service.ChargeRecruitCost("main", "k", "c", "elephant", 2).Returns(0f);

        Assert.IsNull(kit.Sut.ChargeRecruit(new RecruitContext(null, null, EarningTestKit.Local()), "elephant", 2));
    }

    [TestMethod]
    public void ChargeRecruit_NaNCharged_ReturnsNoNotice()
    {
        // Moved gate: charged > 0f is false for NaN, so no toast carries a NaN amount.
        var kit = new EarningTestKit();
        kit.Service.ChargeRecruitCost("main", "k", "c", "elephant", 2).Returns(float.NaN);

        Assert.IsNull(kit.Sut.ChargeRecruit(new RecruitContext(null, null, EarningTestKit.Local()), "elephant", 2));
    }

    [TestMethod]
    public void ChargeRecruit_DedicatedWithoutScope_ChargesNobodyAndLogsOnce()
    {
        var kit = new EarningTestKit(dedicated: true);
        var ctx = new RecruitContext(null, null, EarningTestKit.Local());

        kit.Sut.ChargeRecruit(ctx, "elephant", 1);
        kit.Sut.ChargeRecruit(ctx, "elephant", 1);

        kit.Service.DidNotReceiveWithAnyArgs().ChargeRecruitCost(default, default, default, default, default);
        kit.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Dedicated server")));
    }

    [TestMethod]
    public void ChargeRecruit_ScopedPartyGone_WarnsOnceAndChargesNobody()
    {
        var kit = new EarningTestKit();
        var ctx = new RecruitContext("party_1", null, EarningTestKit.Local());

        kit.Sut.ChargeRecruit(ctx, "elephant", 1);
        kit.Sut.ChargeRecruit(ctx, "elephant", 1);

        kit.Service.DidNotReceiveWithAnyArgs().ChargeRecruitCost(default, default, default, default, default);
        kit.Logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("party_1")));
    }
}
