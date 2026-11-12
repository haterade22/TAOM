using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The co-op authority earns per player (#967 C1). During a session the authority's own MainHero earns,
/// pays and counts as nobody's (Mike's decision on the #967 design thread, "the host does not play"); a
/// client writes nothing; prisoner income pays nobody until the event names an actor. The player registry
/// decides who is a player, never a comparison with Hero.MainHero: on the co-op server MainHero is a
/// guest's for a while (barter and hideout-finalize scopes).
/// </summary>
[TestClass]
public class SpecialResourceEarningServiceCoopTests
{
    private static VictorySnapshot Win(params EarnerRef[] winners) => new()
    {
        State = "AttackerVictory",
        IsPlayerMapEvent = false,
        PlayerSide = BattleSideEnum.None,
        WinningSide = BattleSideEnum.Attacker,
        EventType = MapEvent.BattleTypes.FieldBattle,
        BeatAFieldParty = true,
        EnemyCount = 30,
        LocalHero = EarningTestKit.Local(),
        WinnerCandidates = winners,
    };

    private static RaidOrHideoutSnapshot Raid(params EarnerRef[] leaders) => new()
    {
        Side = BattleSideEnum.Attacker,
        IsPlayerMapEvent = false,
        VillageLooted = true,
        IsFinalized = true,
        LocalHero = EarningTestKit.Local(),
        AttackerLeaders = leaders,
    };

    // --- Victory ---

    [TestMethod]
    public void OnVictory_TwoWinningPlayers_EachEarnsFromTheSameEnemyCount()
    {
        var kit = new EarningTestKit(session: true);

        // The pay no longer divides by each player's own men, so a small party gets no bonus.
        kit.Sut.OnVictory(Win(EarningTestKit.Remote("g1", 10), EarningTestKit.Remote("g2", 5)));

        kit.Service.Received(1).EarnFromBattle("g1", "k", "c", 30f);
        kit.Service.Received(1).EarnFromBattle("g2", "k", "c", 30f);
        kit.Service.DidNotReceive().EarnFromBattle("main", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnVictory_SessionIgnoresHostPlayerMapEventFlag_RemotePlayerStillEarns()
    {
        var kit = new EarningTestKit(session: true);
        var s = Win(EarningTestKit.Remote("g1"));
        s.IsPlayerMapEvent = false;
        s.PlayerSide = BattleSideEnum.None;

        kit.Sut.OnVictory(s);

        kit.Service.Received(1).EarnFromBattle("g1", "k", "c", Arg.Any<float>());
    }

    [TestMethod]
    public void OnVictory_LocalMainHeroAmongTheWinners_IsExcluded()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnVictory(Win(EarningTestKit.Local(control: PlayerControl.NotPlayer), EarningTestKit.Remote("g1")));

        kit.Service.DidNotReceive().EarnFromBattle("main", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        kit.Service.Received(1).EarnFromBattle("g1", "k", "c", Arg.Any<float>());
    }

    [DataTestMethod]
    [DataRow(PlayerControl.NotPlayer)]
    [DataRow(PlayerControl.Unknown)]
    public void OnVictory_WinnerNotRegisteredAsAPlayer_IsExcluded(PlayerControl control)
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnVictory(Win(EarningTestKit.Remote("lord_1", 10, control)));

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromBattle(default, default, default, default);
    }

    [TestMethod]
    public void OnVictory_WinnerUnresolved_EarnsNothing()
    {
        var kit = new EarningTestKit(session: true);
        var s = Win(EarningTestKit.Remote("g1"));
        s.WinningSide = BattleSideEnum.None;

        kit.Sut.OnVictory(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromBattle(default, default, default, default);
    }

    [TestMethod]
    public void OnVictory_SessionSiege_EachWinningPlayerEarnsFromSiege()
    {
        var kit = new EarningTestKit(session: true);
        var s = Win(EarningTestKit.Remote("g1"), EarningTestKit.Remote("g2"));
        s.IsSiege = true;

        kit.Sut.OnVictory(s);

        kit.Service.Received(1).EarnFromSiege("g1", "k", "c");
        kit.Service.Received(1).EarnFromSiege("g2", "k", "c");
    }

    [TestMethod]
    public void OnVictory_SessionRaidEventWithoutAFieldParty_EarnsNothing()
    {
        var kit = new EarningTestKit(session: true);
        var s = Win(EarningTestKit.Remote("g1"));
        s.EventType = MapEvent.BattleTypes.Raid;
        s.BeatAFieldParty = false;

        kit.Sut.OnVictory(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromBattle(default, default, default, default);
    }

    [TestMethod]
    public void OnVictory_SessionPlayerWithNoRoster_EarnsFromTheEnemyCount()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnVictory(Win(EarningTestKit.Remote("g1", null)));

        kit.Service.Received(1).EarnFromBattle("g1", "k", "c", 30f);
    }

    [TestMethod]
    public void OnVictory_SessionWinner_IsSeededBeforeTheyEarn()
    {
        var kit = new EarningTestKit(session: true);
        kit.Storage.Contains("g1", "war_spoils").Returns(false);

        kit.Sut.OnVictory(Win(EarningTestKit.Remote("g1")));

        Received.InOrder(() =>
        {
            kit.Storage.Set("g1", "war_spoils", 10f);
            kit.Service.EarnFromBattle("g1", "k", "c", Arg.Any<float>());
        });
    }

    [TestMethod]
    public void OnVictory_SessionEarning_NeverProducesALocalNotice()
    {
        var kit = new EarningTestKit(session: true);
        kit.Service.GetCurrentAmount("g1", "k", "c").Returns(1f, 9f);

        var notices = kit.Sut.OnVictory(Win(EarningTestKit.Remote("g1")));

        Assert.AreEqual(1, notices.Count);
        Assert.AreEqual("g1", notices[0].HeroId);
        Assert.IsFalse(notices[0].IsLocal);
    }

    [TestMethod]
    public void OnVictory_SessionGuestSwappedInAsMainHero_EarnsWithoutALocalNotice()
    {
        // Inside a scope that swaps a guest in, Hero.MainHero IS the guest: the registry says Player, the
        // reference says local. The registry wins, and no toast is shown on a server.
        var kit = new EarningTestKit(session: true);
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(1f, 9f);

        var notices = kit.Sut.OnVictory(Win(EarningTestKit.Local()));

        kit.Service.Received(1).EarnFromBattle("main", "k", "c", Arg.Any<float>());
        Assert.AreEqual(1, notices.Count);
        Assert.IsFalse(notices[0].IsLocal);
    }

    [TestMethod]
    public void OnVictory_SessionEarn_LogsTheEarningHeroId()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnVictory(Win(EarningTestKit.Remote("g1")));

        kit.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("victory") && m.Contains("g1")));
    }

    [TestMethod]
    public void OnTournament_SinglePlayerEarn_AddsNoLogLine()
    {
        var kit = new EarningTestKit();

        kit.Sut.OnTournament(EarningTestKit.Local());

        kit.Logger.DidNotReceiveWithAnyArgs().LogInfo(default);
    }

    [TestMethod]
    public void OnVictory_SessionClient_WritesNothing()
    {
        var kit = new EarningTestKit(session: true, authority: false);

        kit.Sut.OnVictory(Win(EarningTestKit.Remote("g1")));

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromBattle(default, default, default, default);
        kit.Storage.DidNotReceiveWithAnyArgs().Set(default, default, default);
    }

    // --- Raid, hideout ---

    [TestMethod]
    public void OnRaid_SessionAttackerLeaders_OnlyRegisteredRemotePlayersEarn()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnRaid(Raid(
            EarningTestKit.Remote("g1"),
            EarningTestKit.Remote("lord_1", 10, PlayerControl.NotPlayer),
            EarningTestKit.Local(control: PlayerControl.NotPlayer)));

        kit.Service.Received(1).EarnFromRaid("g1", "k", "c");
        kit.Service.Received(1).EarnFromRaid(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnRaid_SessionVillageNotLooted_EarnsNothing()
    {
        var kit = new EarningTestKit(session: true);
        var s = Raid(EarningTestKit.Remote("g1"));
        s.VillageLooted = false;

        kit.Sut.OnRaid(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromRaid(default, default, default);
    }

    [TestMethod]
    public void OnRaid_SessionClient_WritesNothing()
    {
        var kit = new EarningTestKit(session: true, authority: false);

        kit.Sut.OnRaid(Raid(EarningTestKit.Remote("g1")));

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromRaid(default, default, default);
    }

    [TestMethod]
    public void OnHideout_SessionFinalized_EachAttackerPlayerEarnsOnce()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnHideout(Raid(EarningTestKit.Remote("g1"), EarningTestKit.Remote("g2")));

        kit.Service.Received(1).EarnFromHideout("g1", "k", "c");
        kit.Service.Received(1).EarnFromHideout("g2", "k", "c");
    }

    [TestMethod]
    public void OnHideout_SessionGuestSwappedInAsMainHero_EarnsWithoutALocalNotice()
    {
        // The hideout finalize runs inside BarterPlayerContext, so the attacking guest is Hero.MainHero.
        var kit = new EarningTestKit(session: true);
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(1f, 9f);

        var notices = kit.Sut.OnHideout(Raid(EarningTestKit.Local()));

        kit.Service.Received(1).EarnFromHideout("main", "k", "c");
        Assert.AreEqual(1, notices.Count);
        Assert.IsFalse(notices[0].IsLocal);
    }

    [TestMethod]
    public void OnHideout_SessionNotYetFinalized_EarnsNothing()
    {
        var kit = new EarningTestKit(session: true);
        var s = Raid(EarningTestKit.Remote("g1"));
        s.IsFinalized = false;

        kit.Sut.OnHideout(s);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromHideout(default, default, default);
    }

    [TestMethod]
    public void OnHideout_SessionClient_WritesNothing()
    {
        var kit = new EarningTestKit(session: true, authority: false);

        kit.Sut.OnHideout(Raid(EarningTestKit.Remote("g1")));

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromHideout(default, default, default);
    }

    // --- Tournament ---

    [TestMethod]
    public void OnTournament_SessionRemotePlayerWins_Earns()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnTournament(EarningTestKit.Remote("g1"));

        kit.Service.Received(1).EarnFromTournament("g1", "k", "c");
    }

    [DataTestMethod]
    [DataRow(PlayerControl.NotPlayer)]
    [DataRow(PlayerControl.Unknown)]
    public void OnTournament_SessionAiWinner_EarnsNothing(PlayerControl control)
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnTournament(EarningTestKit.Remote("lord_1", 10, control));

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromTournament(default, default, default);
    }

    [TestMethod]
    public void OnTournament_SessionHostMainHeroWins_EarnsNothing()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnTournament(EarningTestKit.Local(control: PlayerControl.NotPlayer));

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromTournament(default, default, default);
    }

    [TestMethod]
    public void OnTournament_SessionClient_WritesNothing()
    {
        var kit = new EarningTestKit(session: true, authority: false);

        kit.Sut.OnTournament(EarningTestKit.Remote("g1"));

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromTournament(default, default, default);
    }

    // --- Prisoners ---

    [TestMethod]
    public void OnPrisoners_Session_PaysNobodyAndLogsOnce()
    {
        var kit = new EarningTestKit(session: true);

        kit.Sut.OnPrisoners(EarningTestKit.Local(), 3);
        kit.Sut.OnPrisoners(EarningTestKit.Local(), 4);

        kit.Service.DidNotReceiveWithAnyArgs().EarnFromPrisoners(default, default, default, default);
        kit.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Prisoner")));
    }

    // --- Recruit payer ---

    [TestMethod]
    public void ChargeRecruit_SessionUnscoped_ChargesNobody()
    {
        var kit = new EarningTestKit(session: true);

        var notice = kit.Sut.ChargeRecruit(new RecruitContext(null, null, EarningTestKit.Local()), "elephant", 1);

        Assert.IsNull(notice);
        kit.Service.DidNotReceiveWithAnyArgs().ChargeRecruitCost(default, default, default, default, default);
    }

    [TestMethod]
    public void ChargeRecruit_SessionScopedGuest_ChargesTheGuestWithoutALocalNotice()
    {
        var kit = new EarningTestKit(session: true);
        kit.Service.ChargeRecruitCost("g1", "k", "c", "elephant", 1).Returns(3f);

        var notice = kit.Sut.ChargeRecruit(
            new RecruitContext("party_g1", EarningTestKit.Remote("g1"), EarningTestKit.Local()), "elephant", 1);

        Assert.IsNull(notice);
        kit.Service.Received(1).ChargeRecruitCost("g1", "k", "c", "elephant", 1);
    }

    [TestMethod]
    public void ChargeRecruit_SessionUntrackedGuest_IsSeededBeforeTheCharge()
    {
        var kit = new EarningTestKit(session: true);
        kit.Storage.Contains("g1", "war_spoils").Returns(false);

        kit.Sut.ChargeRecruit(
            new RecruitContext("party_g1", EarningTestKit.Remote("g1"), EarningTestKit.Local()), "elephant", 1);

        Received.InOrder(() =>
        {
            kit.Storage.Set("g1", "war_spoils", 10f);
            kit.Service.ChargeRecruitCost("g1", "k", "c", "elephant", 1);
        });
    }

    [TestMethod]
    public void ChargeRecruit_SessionGuestSwappedInAsMainHero_ChargesWithoutALocalNotice()
    {
        var kit = new EarningTestKit(session: true);
        kit.Service.ChargeRecruitCost("main", "k", "c", "elephant", 1).Returns(3f);

        var notice = kit.Sut.ChargeRecruit(
            new RecruitContext("party_g1", EarningTestKit.Local(), EarningTestKit.Local()), "elephant", 1);

        Assert.IsNull(notice);
        kit.Service.Received(1).ChargeRecruitCost("main", "k", "c", "elephant", 1);
    }

    [TestMethod]
    public void ChargeRecruit_SessionClient_ChargesNobody()
    {
        var kit = new EarningTestKit(session: true, authority: false);

        kit.Sut.ChargeRecruit(
            new RecruitContext("party_g1", EarningTestKit.Remote("g1"), EarningTestKit.Local()), "elephant", 1);

        kit.Service.DidNotReceiveWithAnyArgs().ChargeRecruitCost(default, default, default, default, default);
    }

    // --- Upgrades (#967 C2) ---

    private static CoopUpgradeLine[] Upgrades(params CoopUpgradeLine[] lines) => lines;

    [TestMethod]
    public void ChargeCoopUpgrades_Server_ChargesThePlayerForEachLineAndReturnsWhatLeft()
    {
        var kit = new EarningTestKit(session: true, dedicated: true);
        kit.Service.GetCurrentAmount("g1", "k", "c").Returns(20f, 13f);

        var charged = kit.Sut.ChargeCoopUpgrades(
            EarningTestKit.Remote("g1"), Upgrades(new CoopUpgradeLine("elite_a", 2), new CoopUpgradeLine("elite_b", 1)));

        Assert.AreEqual(7f, charged, 0.001f);
        kit.Service.Received(1).SpendForUpgrade("g1", "k", "c", "elite_a", 2);
        kit.Service.Received(1).SpendForUpgrade("g1", "k", "c", "elite_b", 1);
    }

    [TestMethod]
    public void ChargeCoopUpgrades_UntrackedPlayer_IsSeededBeforeTheCharge()
    {
        var kit = new EarningTestKit(session: true, dedicated: true);
        kit.Storage.Contains("g1", "war_spoils").Returns(false);

        kit.Sut.ChargeCoopUpgrades(EarningTestKit.Remote("g1"), Upgrades(new CoopUpgradeLine("elite_a", 2)));

        Received.InOrder(() =>
        {
            kit.Storage.Set("g1", "war_spoils", 10f);
            kit.Service.SpendForUpgrade("g1", "k", "c", "elite_a", 2);
        });
    }

    [TestMethod]
    public void ChargeCoopUpgrades_Client_ChargesAndSeedsNobody()
    {
        var kit = new EarningTestKit(session: true, authority: false);

        var charged = kit.Sut.ChargeCoopUpgrades(EarningTestKit.Remote("g1"), Upgrades(new CoopUpgradeLine("elite_a", 2)));

        Assert.AreEqual(0f, charged);
        kit.Service.DidNotReceiveWithAnyArgs().SpendForUpgrade(default, default, default, default, default);
        kit.Storage.DidNotReceiveWithAnyArgs().Set(default, default, default);
    }

    [TestMethod]
    public void ChargeCoopUpgrades_UpgraderGone_ChargesNobody()
    {
        var kit = new EarningTestKit(session: true, dedicated: true);

        var charged = kit.Sut.ChargeCoopUpgrades(null, Upgrades(new CoopUpgradeLine("elite_a", 2)));

        Assert.AreEqual(0f, charged);
        kit.Service.DidNotReceiveWithAnyArgs().SpendForUpgrade(default, default, default, default, default);
    }
}
