using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.CoopInterop.GuestTrade;

/// <summary>
/// The lord's gear ladder for a co-op guest, run by the co-op server (docs/features/coop-interop.md, "Lords' Ladder for co-op
/// players"): the armoury reply carries the guest hero's rung as the server holds it, and a hand-in or a claim runs TAOM's own
/// <see cref="LordsLadderService"/> for that hero, with the checks the armoury window makes for a local player.
/// </summary>
[TestClass]
public class GuestLadderHostTests
{
    private const string Hero = "guest_hero";
    private const string OtherHero = "server_hero";
    private const string Town = "town_A1";

    private static readonly LadderStep Hands = new(LadderSlot.Hands, "q_hands", 10);
    private static readonly LadderStep Head = new(LadderSlot.Head, "q_head", 30);

    private ArmourAcquisitionState _state = null!;
    private IArmourGateService _gate = null!;
    private IArmouryPlayerAdapter _player = null!;
    private IGuestTradeShops _shops = null!;
    private IArmourAcquisitionConfigProvider _config = null!;
    private GuestTradeHost _host = null!;
    private readonly List<InventoryPiece> _carried = new();

    [TestInitialize]
    public void Setup()
    {
        _state = new ArmourAcquisitionState();
        _config = Substitute.For<IArmourAcquisitionConfigProvider>();
        _config.GetConfig().Returns(Config());
        _gate = Substitute.For<IArmourGateService>();
        _gate.GetPieces(Arg.Any<ArmourClass>(), Arg.Any<string?>(), Arg.Any<ArmourSlot?>()).Returns(Array.Empty<string>());
        _gate.GetPieces(ArmourClass.Lord, "gondor", ArmourSlot.Hand).Returns(new[] { "gd_lord_gloves", "gd_lord_gloves_b" });
        _gate.GetRecord("gd_lord_gloves").Returns(new ArmourItemRecord("gd_lord_gloves", ArmourSlot.Hand, 5, false, "gondor", 900));
        _gate.GetRecord("gd_lord_gloves_b").Returns(new ArmourItemRecord("gd_lord_gloves_b", ArmourSlot.Hand, 5, false, "gondor", 800));

        _player = Substitute.For<IArmouryPlayerAdapter>();
        _player.HeroId.Returns(Hero);
        _player.CultureId.Returns("gondor");
        _player.ReadInventory().Returns(_ => _carried.ToArray());
        _player.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _player.AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(true);

        var hero = new GuestTradeHero(Hero, true, Town, false, false, null, _player, Substitute.For<IPlayerPartyAdapter>());
        var heroes = Substitute.For<IGuestTradeHeroAdapter>();
        heroes.Find(Hero).Returns(_ => hero);

        _shops = Substitute.For<IGuestTradeShops>();
        _shops.HasArmoury(Town).Returns(true);
        _shops.ArmouryLevel(Town).Returns(3);
        _shops.LadderOpen.Returns(true);
        _shops.LadderLordLevel.Returns(3);
        var marketplace = Substitute.For<ICultureMarketplaceConfigProvider>();
        _shops.Ladder(Arg.Any<GuestTradeHero>())
            .Returns(c => new LordsLadderService(_state, _config, _gate, c.Arg<GuestTradeHero>().Player, marketplace));

        _host = new GuestTradeHost(heroes, _shops, Substitute.For<ISpecialResourceService>(), Substitute.For<IModLogger>());
    }

    private static ArmourAcquisitionConfig Config()
    {
        var d = ArmourAcquisitionConfig.Default;
        var ladder = new LordsLadderConfig(new[] { Hands, Head }, countsKnockouts: true,
            new Dictionary<string, string> { ["gondor"] = "m_gondor" }, new MaterialDrop(0.1f, 0.01f, 0.6f, 1, 3),
            new Dictionary<string, IReadOnlyList<string>>());
        return new ArmourAcquisitionConfig(d.Enabled, d.HeavyLevel, d.EliteLevel, 3, d.Recipes, d.NamedWeapons,
            d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation,
            d.VisitChancePerDay, d.VisitDurationDays, d.VisitLevelBonus, ladder);
    }

    private static GuestTradeRequest Req(GuestTradeAction action, string? subject = null, string? target = null) =>
        new(action, Town, subject, null, target, 0, "r1");

    private void Carry(int units) => _carried.Add(new InventoryPiece("m_gondor", null, units));

    private void ReadyHands() => _state.LadderReady[Hero] = LadderSlotRules.Bit(LadderSlot.Hands);

    [TestMethod]
    public void TheWireValues_ArePinned()
    {
        Assert.AreEqual(5, (int)GuestTradeAction.LadderHandIn);
        Assert.AreEqual(6, (int)GuestTradeAction.LadderClaim);
        Assert.AreEqual(0, (int)GuestLadderOutcome.Done);
        Assert.AreEqual(1, (int)GuestLadderOutcome.RungChanged);
        Assert.AreEqual(2, (int)GuestLadderOutcome.NotEnough);
        Assert.AreEqual(3, (int)GuestLadderOutcome.ArmouryLevel);
        Assert.AreEqual(4, (int)GuestLadderOutcome.NotAChoice);
        Assert.AreEqual(5, (int)GuestLadderOutcome.Off);
        Assert.AreEqual(6, (int)GuestLadderOutcome.NotDone);
    }

    [TestMethod]
    public void ArmouryBalance_WithTheLadderOn_CarriesTheGuestHerosRung()
    {
        Carry(4);

        var view = _host.Handle(Hero, Req(GuestTradeAction.ArmouryBalance)).Ladder;

        Assert.IsNotNull(view);
        Assert.AreEqual(LadderSlot.Hands, view!.Slot);
        Assert.IsFalse(view.Ready);
        Assert.IsTrue(view.CanClaim);
        Assert.AreEqual(3, view.LordLevel);
        Assert.AreEqual("m_gondor", view.MaterialId);
        Assert.AreEqual(10, view.Needed);
        Assert.AreEqual(4, view.Carried);
        Assert.AreEqual(0, view.Choices.Count, "choices are sent only for a rung that waits to be claimed");
    }

    [TestMethod]
    public void ArmouryBalance_ForAReadyRung_CarriesTheChoicesAndNoHandIn()
    {
        ReadyHands();

        var view = _host.Handle(Hero, Req(GuestTradeAction.ArmouryBalance)).Ladder!;

        Assert.IsTrue(view.Ready);
        Assert.IsNull(view.MaterialId);
        CollectionAssert.AreEqual(new[] { "gd_lord_gloves", "gd_lord_gloves_b" }, view.Choices.ToArray());
    }

    [TestMethod]
    public void ArmouryBalance_WithTheLadderOff_CarriesNoLadder_AndBuildsNoLadder()
    {
        _shops.LadderOpen.Returns(false);

        var reply = _host.Handle(Hero, Req(GuestTradeAction.ArmouryBalance));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.IsNull(reply.Ladder);
        _shops.DidNotReceiveWithAnyArgs().Ladder(default!);
    }

    [TestMethod]
    public void ArmouryBalance_EveryRungClaimed_CarriesNoLadder()
    {
        _state.LadderClaimed[Hero] = LadderSlotRules.Bit(LadderSlot.Hands) | LadderSlotRules.Bit(LadderSlot.Head);

        Assert.IsNull(_host.Handle(Hero, Req(GuestTradeAction.ArmouryBalance)).Ladder);
    }

    [TestMethod]
    public void AViewThatThrows_LeavesTheUpgradeAnswerAlone()
    {
        _shops.Ladder(Arg.Any<GuestTradeHero>()).Returns(_ => throw new InvalidOperationException("boom"));
        _shops.ArmouryOffers(Arg.Any<GuestTradeHero>(), 3).Returns(Array.Empty<UpgradeOffer>());

        var reply = _host.Handle(Hero, Req(GuestTradeAction.ArmouryUpgrade, "mail", "plate"));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual((int)UpgradeBlock.NoLongerCarried, reply.Status);
        Assert.IsNull(reply.Ladder);
    }

    [TestMethod]
    public void LadderHandIn_TakesTheGuestsMaterials_AndReadiesOnlyTheGuestsRung()
    {
        Carry(12);
        _state.LadderReady[OtherHero] = 0;

        var reply = _host.Handle(Hero, Req(GuestTradeAction.LadderHandIn, "Hands"));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual((int)GuestLadderOutcome.Done, reply.Status);
        _player.Received(1).RemoveItem("m_gondor", 10);
        Assert.AreEqual(LadderSlotRules.Bit(LadderSlot.Hands), _state.LadderReady[Hero]);
        Assert.AreEqual(0, _state.LadderReady[OtherHero]);
        Assert.IsTrue(reply.Ladder!.Ready, "the reply shows the rung after the hand-in");
    }

    [TestMethod]
    public void LadderHandIn_ForAStaleSlot_IsRungChanged_AndTakesNothing()
    {
        Carry(40);

        var reply = _host.Handle(Hero, Req(GuestTradeAction.LadderHandIn, "Head"));

        Assert.AreEqual((int)GuestLadderOutcome.RungChanged, reply.Status);
        _player.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void LadderHandIn_ForARungAlreadyDone_IsRungChanged()
    {
        Carry(40);
        ReadyHands();

        Assert.AreEqual((int)GuestLadderOutcome.RungChanged, _host.Handle(Hero, Req(GuestTradeAction.LadderHandIn, "Hands")).Status);
        _player.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void LadderHandIn_WhenShort_IsNotEnough()
    {
        Carry(9);

        Assert.AreEqual((int)GuestLadderOutcome.NotEnough, _host.Handle(Hero, Req(GuestTradeAction.LadderHandIn, "Hands")).Status);
        Assert.IsFalse(_state.LadderReady.ContainsKey(Hero));
    }

    [TestMethod]
    public void LadderHandIn_WhenTheMaterialsCannotBeTaken_IsNotDone()
    {
        Carry(12);
        _player.RemoveItem("m_gondor", 10).Returns(false);

        Assert.AreEqual((int)GuestLadderOutcome.NotDone, _host.Handle(Hero, Req(GuestTradeAction.LadderHandIn, "Hands")).Status);
        Assert.IsFalse(_state.LadderReady.ContainsKey(Hero));
    }

    [TestMethod]
    public void LadderClaim_FitsTheGuestHero_AndMarksTheSlotClaimed()
    {
        ReadyHands();

        var reply = _host.Handle(Hero, Req(GuestTradeAction.LadderClaim, "Hands", "gd_lord_gloves_b"));

        Assert.AreEqual((int)GuestLadderOutcome.Done, reply.Status);
        _player.Received(1).AddPiece("gd_lord_gloves_b", null, 1);
        Assert.AreEqual(LadderSlotRules.Bit(LadderSlot.Hands), _state.LadderClaimed[Hero]);
        Assert.AreEqual(LadderSlot.Head, reply.Ladder!.Slot);
    }

    [TestMethod]
    public void LadderClaim_WhileTheRungIsNotDone_IsRungChanged()
    {
        Assert.AreEqual((int)GuestLadderOutcome.RungChanged,
            _host.Handle(Hero, Req(GuestTradeAction.LadderClaim, "Hands", "gd_lord_gloves")).Status);
        _player.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
    }

    [TestMethod]
    public void LadderClaim_BelowTheLordLevel_IsArmouryLevel_AndGivesNothing()
    {
        ReadyHands();
        _shops.ArmouryLevel(Town).Returns(2);

        Assert.AreEqual((int)GuestLadderOutcome.ArmouryLevel,
            _host.Handle(Hero, Req(GuestTradeAction.LadderClaim, "Hands", "gd_lord_gloves")).Status);
        _player.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
        Assert.IsFalse(_state.LadderClaimed.ContainsKey(Hero));
    }

    [TestMethod]
    public void LadderClaim_ForAPieceNotOffered_IsNotAChoice()
    {
        ReadyHands();

        Assert.AreEqual((int)GuestLadderOutcome.NotAChoice,
            _host.Handle(Hero, Req(GuestTradeAction.LadderClaim, "Hands", "anduril")).Status);
        _player.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
    }

    [TestMethod]
    public void LadderClaim_WhenThePieceCannotBeGiven_IsNotDone()
    {
        ReadyHands();
        _player.AddPiece("gd_lord_gloves", null, 1).Returns(false);

        Assert.AreEqual((int)GuestLadderOutcome.NotDone,
            _host.Handle(Hero, Req(GuestTradeAction.LadderClaim, "Hands", "gd_lord_gloves")).Status);
        Assert.IsFalse(_state.LadderClaimed.ContainsKey(Hero));
    }

    [TestMethod]
    public void LadderActions_WithTheLadderOff_AreOff_AndBuildNoLadder()
    {
        Carry(12);
        ReadyHands();
        _shops.LadderOpen.Returns(false);

        Assert.AreEqual((int)GuestLadderOutcome.Off, _host.Handle(Hero, Req(GuestTradeAction.LadderHandIn, "Hands")).Status);
        Assert.AreEqual((int)GuestLadderOutcome.Off,
            _host.Handle(Hero, Req(GuestTradeAction.LadderClaim, "Hands", "gd_lord_gloves")).Status);
        _shops.DidNotReceiveWithAnyArgs().Ladder(default!);
    }

    [TestMethod]
    public void LadderActions_ElsewhereOrWithoutAnArmoury_AreRefused()
    {
        ReadyHands();

        var elsewhere = _host.Handle(Hero, new GuestTradeRequest(GuestTradeAction.LadderClaim, "town_B2", "Hands", null, "gd_lord_gloves", 0, "r1"));
        _shops.HasArmoury(Town).Returns(false);
        var noArmoury = _host.Handle(Hero, Req(GuestTradeAction.LadderClaim, "Hands", "gd_lord_gloves"));

        Assert.AreEqual(GuestTradeRefusal.NotInSettlement, elsewhere.Refusal);
        Assert.AreEqual(GuestTradeRefusal.NoArmoury, noArmoury.Refusal);
        _player.DidNotReceiveWithAnyArgs().AddPiece(default!, default, default);
    }

    [TestMethod]
    public void LadderRequests_WithoutAReadableSlotOrPiece_AreBadRequests()
    {
        var bad = new[]
        {
            Req(GuestTradeAction.LadderHandIn),
            Req(GuestTradeAction.LadderHandIn, "3"),
            Req(GuestTradeAction.LadderHandIn, "hands,head"),
            Req(GuestTradeAction.LadderClaim, "Hands"),
            Req(GuestTradeAction.LadderClaim, null, "gd_lord_gloves"),
            Req(GuestTradeAction.LadderClaim, "Hands", new string('x', 129)),
        };
        foreach (var request in bad)
            Assert.AreEqual(GuestTradeRefusal.BadRequest, _host.Handle(Hero, request).Refusal, request.ToString());
    }

    [TestMethod]
    public void TheView_SendsAtMost64Choices_AndNoOverlongId()
    {
        ReadyHands();
        var many = Enumerable.Range(0, 70).Select(i => "piece_" + i).Concat(new[] { new string('y', 129) }).ToArray();
        _gate.GetPieces(ArmourClass.Lord, "gondor", ArmourSlot.Hand).Returns(many);

        var view = _host.Handle(Hero, Req(GuestTradeAction.ArmouryBalance)).Ladder!;

        Assert.AreEqual(GuestLadderView.MaxChoices, view.Choices.Count);
        Assert.IsTrue(view.Choices.All(id => id.Length <= GuestLadderView.MaxIdLength));
    }
}
