using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.CoopInterop.GuestTrade;

/// <summary>
/// Lord's materials for co-op players (docs/features/coop-interop.md, "Lords' Ladder for co-op players"): after a battle
/// won, the co-op server rolls TAOM's own find for each co-op player's hero on the winning side (base chance: the kills
/// are counted on the player's PC), keeps it in its save, and gives it to that hero's party once the party is free and out
/// of battle. A co-op player marked as the local main hero (Coop's hideout finalize) is rolled too.
/// </summary>
[TestClass]
public class GuestLadderFindServiceTests
{
    private const string Guest = "guest_hero";

    private ArmourAcquisitionState _state = null!;
    private ICoopSessionProvider _coop = null!;
    private IGuestTradeShops _shops = null!;
    private IGuestTradeHeroAdapter _heroes = null!;
    private IArmouryPlayerAdapter _guestPlayer = null!;
    private IModLogger _logger = null!;
    private GuestTradeHero? _guest;
    private GuestLadderFindService _finds = null!;

    private sealed class FixedRandom : Random
    {
        private readonly double _roll;
        public FixedRandom(double roll) { _roll = roll; }
        public override double NextDouble() => _roll;
        public override int Next(int minValue, int maxValue) => maxValue - 1;
    }

    private static readonly Random Hit = new FixedRandom(0.0);

    [TestInitialize]
    public void Setup()
    {
        _state = new ArmourAcquisitionState();
        var d = ArmourAcquisitionConfig.Default;
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(new ArmourAcquisitionConfig(d.Enabled, d.HeavyLevel, d.EliteLevel, d.LordLevel, d.Recipes, d.NamedWeapons,
            d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation, d.VisitChancePerDay, d.VisitDurationDays, d.VisitLevelBonus,
            new LordsLadderConfig(new[] { new LadderStep(LadderSlot.Hands, "q_hands", 10) }, true,
                new Dictionary<string, string> { ["gondor"] = "m_gondor" }, new MaterialDrop(0.1f, 0.01f, 0.6f, 1, 3),
                new Dictionary<string, IReadOnlyList<string>>())));
        var mainPlayer = Substitute.For<IArmouryPlayerAdapter>();
        mainPlayer.HeroId.Returns("server_hero");
        var ladder = new LordsLadderService(_state, config, Substitute.For<IArmourGateService>(), mainPlayer,
            Substitute.For<ICultureMarketplaceConfigProvider>());

        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsSessionActive.Returns(true);
        _coop.IsAuthority.Returns(true);
        _shops = Substitute.For<IGuestTradeShops>();
        _shops.LadderOpen.Returns(true);
        _guestPlayer = Substitute.For<IArmouryPlayerAdapter>();
        _guestPlayer.HeroId.Returns(Guest);
        _guestPlayer.CultureId.Returns("gondor");
        _guestPlayer.AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(true);
        _guest = Hero(isFreeLeader: true, inBattle: false);
        _heroes = Substitute.For<IGuestTradeHeroAdapter>();
        _heroes.Find(Guest).Returns(_ => _guest);
        _logger = Substitute.For<IModLogger>();
        _finds = new GuestLadderFindService(ladder, _state, _heroes, _shops, _coop, _logger);
    }

    private GuestTradeHero Hero(bool isFreeLeader, bool inBattle) =>
        new(Guest, isFreeLeader, null, inBattle, false, null, _guestPlayer, Substitute.For<IPlayerPartyAdapter>());

    private static EarnerRef Winner(string heroId = Guest, bool local = false, PlayerControl control = PlayerControl.Player) =>
        new(heroId, "gondor_k", "gondor", local, control);

    // --- Rolling after a battle won ---

    [TestMethod]
    public void OnBattleWon_ACoopPlayerOnTheWinningSide_KeepsTheFindForThatHero()
    {
        _finds.OnBattleWon(new[] { Winner() }, Hit);

        Assert.AreEqual(3, _state.PendingFinds[Guest]);
        _guestPlayer.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
    }

    [TestMethod]
    public void OnBattleWon_ACoopPlayerMarkedAsTheLocalHero_IsStillRolled()
    {
        _finds.OnBattleWon(new[] { Winner(local: true) }, Hit);

        Assert.AreEqual(3, _state.PendingFinds[Guest]);
    }

    [TestMethod]
    public void OnBattleWon_AnAiLeaderOrAnUnknownOne_IsNotRolled()
    {
        _finds.OnBattleWon(new[] { Winner("lord_1", control: PlayerControl.NotPlayer), Winner("lord_2", control: PlayerControl.Unknown), null! }, Hit);

        Assert.AreEqual(0, _state.PendingFinds.Count);
    }

    [TestMethod]
    public void OnBattleWon_TheSameHeroListedTwice_RollsOnce()
    {
        _finds.OnBattleWon(new[] { Winner(), Winner() }, Hit);

        Assert.AreEqual(3, _state.PendingFinds[Guest]);
    }

    [TestMethod]
    public void OnBattleWon_AMissedRoll_KeepsNothing()
    {
        _finds.OnBattleWon(new[] { Winner() }, new FixedRandom(0.99));

        Assert.AreEqual(0, _state.PendingFinds.Count);
    }

    [TestMethod]
    public void OnBattleWon_FindsAddUp_ToAtMost999Units()
    {
        _state.PendingFinds[Guest] = 998;

        _finds.OnBattleWon(new[] { Winner() }, Hit);

        Assert.AreEqual(ArmourAcquisitionState.MaxPendingFindUnits, _state.PendingFinds[Guest]);
    }

    [TestMethod]
    public void OnBattleWon_EveryRungSettled_RollsNothing()
    {
        _state.LadderReady[Guest] = LadderSlotRules.Bit(LadderSlot.Hands);

        _finds.OnBattleWon(new[] { Winner() }, Hit);

        Assert.AreEqual(0, _state.PendingFinds.Count);
    }

    [TestMethod]
    public void RollsFinds_OnlyOnTheServerOfASession_WithTheLadderOpen()
    {
        Assert.IsTrue(_finds.RollsFinds);

        _shops.LadderOpen.Returns(false);
        Assert.IsFalse(_finds.RollsFinds);
        _finds.OnBattleWon(new[] { Winner() }, Hit);

        _shops.LadderOpen.Returns(true);
        _coop.IsAuthority.Returns(false);
        Assert.IsFalse(_finds.RollsFinds);
        _finds.OnBattleWon(new[] { Winner() }, Hit);

        _coop.IsAuthority.Returns(true);
        _coop.IsSessionActive.Returns(false);
        Assert.IsFalse(_finds.RollsFinds, "single player: LordsLadderBehavior rolls the main hero");
        _finds.OnBattleWon(new[] { Winner() }, Hit);

        Assert.AreEqual(0, _state.PendingFinds.Count);
    }

    // --- Handing a find over ---

    [TestMethod]
    public void HandOver_AFreeHeroOutOfBattle_GetsTheMaterials_AndTheFindIsGone()
    {
        _state.PendingFinds[Guest] = 3;

        _finds.HandOver();

        _guestPlayer.Received(1).AddPiece("m_gondor", null, 3);
        Assert.IsFalse(_state.PendingFinds.ContainsKey(Guest));
    }

    [TestMethod]
    public void HandOver_InBattleOrNotAFreeLeader_Waits()
    {
        _state.PendingFinds[Guest] = 3;

        _guest = Hero(isFreeLeader: true, inBattle: true);
        _finds.HandOver();
        _guest = Hero(isFreeLeader: false, inBattle: false);
        _finds.HandOver();

        _guestPlayer.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
        Assert.AreEqual(3, _state.PendingFinds[Guest]);
    }

    [TestMethod]
    public void HandOver_AThrowForOneHero_StillServesTheOthers_AndWarnsOnce()
    {
        var otherPlayer = Substitute.For<IArmouryPlayerAdapter>();
        otherPlayer.CultureId.Returns("gondor");
        otherPlayer.AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(true);
        _heroes.Find("other_hero").Returns(new GuestTradeHero("other_hero", true, null, false, false, null, otherPlayer,
            Substitute.For<IPlayerPartyAdapter>()));
        _guestPlayer.AddPiece("m_gondor", null, 3).Returns(_ => throw new InvalidOperationException("roster busy"));
        _state.PendingFinds[Guest] = 3;
        _state.PendingFinds["other_hero"] = 2;

        Exception? thrown = null;
        try
        {
            _finds.HandOver();
            _finds.HandOver();
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.IsNull(thrown, "one hero's failure must not stop the hand-over");
        otherPlayer.Received(1).AddPiece("m_gondor", null, 2);
        Assert.AreEqual(3, _state.PendingFinds[Guest], "the find waits for a later hour");
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void HandOver_ADeadHero_DropsTheFind()
    {
        _state.PendingFinds[Guest] = 3;
        _guest = new GuestTradeHero(Guest, false, null, false, false, null, _guestPlayer, Substitute.For<IPlayerPartyAdapter>(), IsAlive: false);

        _finds.HandOver();

        _guestPlayer.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
        Assert.IsFalse(_state.PendingFinds.ContainsKey(Guest), "a dead hero's find must not wait in the save forever");
    }

    [TestMethod]
    public void HandOver_AHeroTheServerNoLongerHas_DropsTheFind()
    {
        _state.PendingFinds[Guest] = 3;
        _guest = null;

        _finds.HandOver();

        Assert.IsFalse(_state.PendingFinds.ContainsKey(Guest));
    }

    [TestMethod]
    public void HandOver_AMaterialThatCannotBeGiven_DropsTheFind_AndLogs()
    {
        _state.PendingFinds[Guest] = 3;
        _guestPlayer.AddPiece("m_gondor", null, 3).Returns(false);

        _finds.HandOver();

        Assert.IsFalse(_state.PendingFinds.ContainsKey(Guest));
        _logger.ReceivedWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void HandOver_ACultureWithNoMaterial_DropsTheFind()
    {
        _state.PendingFinds[Guest] = 3;
        _guestPlayer.CultureId.Returns("khand");

        _finds.HandOver();

        _guestPlayer.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
        Assert.IsFalse(_state.PendingFinds.ContainsKey(Guest));
    }

    [TestMethod]
    public void HandOver_OffTheServerOrOutsideASession_ChangesNothing()
    {
        _state.PendingFinds[Guest] = 3;

        _coop.IsAuthority.Returns(false);
        _finds.HandOver();
        _coop.IsAuthority.Returns(true);
        _coop.IsSessionActive.Returns(false);
        _finds.HandOver();

        _guestPlayer.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
        Assert.AreEqual(3, _state.PendingFinds[Guest]);
    }

    [TestMethod]
    public void HandOver_WithTheLadderSwitchedOff_StillGivesWhatWasFound()
    {
        _state.PendingFinds[Guest] = 3;
        _shops.LadderOpen.Returns(false);

        _finds.HandOver();

        _guestPlayer.Received(1).AddPiece("m_gondor", null, 3);
    }
}
