using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.EliteEmissary;
using TAOM.Features.EliteEmissary.Domain;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.CoopInterop.GuestTrade;

[TestClass]
public class GuestTradeHostTests
{
    private const string Hero = "guest_hero";
    private const string Town = "town_A1";

    private IGuestTradeHeroAdapter _heroes = null!;
    private IGuestTradeShops _shops = null!;
    private IEliteEmissaryService _emissary = null!;
    private ISpecialResourceService _resources = null!;
    private IArmouryPlayerAdapter _player = null!;
    private GuestTradeHost _host = null!;
    private GuestTradeHero _hero = null!;

    [TestInitialize]
    public void Setup()
    {
        _heroes = Substitute.For<IGuestTradeHeroAdapter>();
        _shops = Substitute.For<IGuestTradeShops>();
        _emissary = Substitute.For<IEliteEmissaryService>();
        _resources = Substitute.For<ISpecialResourceService>();
        _player = Substitute.For<IArmouryPlayerAdapter>();
        _player.KingdomId.Returns("gondor");
        _player.CultureId.Returns("vlandia");

        _hero = new GuestTradeHero(Hero, true, Town, false, false, new SettlementOwnerInfo(Town, "rohan", "sturgia"),
            _player, Substitute.For<IPlayerPartyAdapter>());
        _heroes.Find(Hero).Returns(_ => _hero);
        _shops.Emissary(Arg.Any<GuestTradeHero>()).Returns(_emissary);
        _emissary.IsEnabled.Returns(true);
        _emissary.IsKeySettlement(Town).Returns(true);
        _emissary.HasPurchasableOffers("rohan", "sturgia").Returns(true);
        _shops.HasArmoury(Town).Returns(true);

        _resources.ResolveResource("rohan", "sturgia").Returns(Resource("horses", "Horses"));
        _resources.GetCurrentAmount(Hero, "rohan", "sturgia").Returns(30f);
        _resources.ResolveResource("gondor", "vlandia").Returns(Resource("silver", "Silver"));
        _resources.GetCurrentAmount(Hero, "gondor", "vlandia").Returns(12f);

        _host = new GuestTradeHost(_heroes, _shops, _resources, Substitute.For<IModLogger>());
    }

    private static SpecialResource Resource(string id, string name) =>
        new(id, new[] { "k" }, new[] { "c" }, name, "icon", 100f, 0f, 0f, 0f, 0f, 0f, 0f);

    private static GuestTradeRequest Req(GuestTradeAction action, string? subject = null, string? modifier = null,
        string? target = null, int quantity = 0, string id = "r1", string settlement = Town) =>
        new(action, settlement, subject, modifier, target, quantity, id);

    private static UpgradeOffer Offer(string source, string? modifier, string target, UpgradeBlock block = UpgradeBlock.None) =>
        new(source, modifier, target, ArmourClass.Heavy, 100, Array.Empty<UpgradeMaterial>(), 0f, 1, block);

    [TestMethod]
    public void EmissaryBalance_RepliesWithTheOwnersResourceForThisHero()
    {
        var reply = _host.Handle(Hero, Req(GuestTradeAction.EmissaryBalance));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual("r1", reply.RequestId);
        Assert.AreEqual("horses", reply.ResourceId);
        Assert.IsNull(reply.ResourceName, "only a purchase names its currency");
        Assert.AreEqual(30f, reply.Balance);
        Assert.IsNull(reply.ArmouryLevel);
        _emissary.DidNotReceiveWithAnyArgs().Purchase(default!, default!, default!, default!, default);
    }

    [TestMethod]
    public void EmissaryBuy_PaidInGold_KeepsTheGoldName_NotTheResources()
    {
        _emissary.Purchase(Hero, "rohan", "sturgia", "elite", 5)
            .Returns(EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Success, "elite", 5, 500, null));

        var reply = _host.Handle(Hero, Req(GuestTradeAction.EmissaryBuy, "elite", quantity: 5));

        Assert.AreEqual(500, reply.TotalCost);
        Assert.IsNull(reply.ResourceName);
    }

    [TestMethod]
    public void AConnectionThatControlsNoHero_IsAnswered_NotFreeLeader()
    {
        var reply = _host.Handle("", Req(GuestTradeAction.EmissaryBalance));

        Assert.AreEqual(GuestTradeRefusal.NotFreeLeader, reply.Refusal);
        Assert.AreEqual("r1", reply.RequestId, "the guest's waiting screen must match the answer");
    }

    [TestMethod]
    public void EmissaryBuy_RunsTaomsPurchaseForTheGuestsHero()
    {
        _emissary.Purchase(Hero, "rohan", "sturgia", "elite", 5)
            .Returns(EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Success, "elite", 5, 50, "Horses"));

        var reply = _host.Handle(Hero, Req(GuestTradeAction.EmissaryBuy, "elite", quantity: 5));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual((int)EmissaryPurchaseStatus.Success, reply.Status);
        Assert.AreEqual(5, reply.Quantity);
        Assert.AreEqual(50, reply.TotalCost);
        Assert.AreEqual(30f, reply.Balance);
        _shops.Received().Emissary(_hero);
    }



    [TestMethod]
    public void AHeroThatIsNotAFreeLeader_IsRefused()
    {
        _hero = _hero with { IsFreeLeader = false };
        Assert.AreEqual(GuestTradeRefusal.NotFreeLeader, _host.Handle(Hero, Req(GuestTradeAction.EmissaryBalance)).Refusal);

        Assert.AreEqual(GuestTradeRefusal.NotFreeLeader, _host.Handle("nobody", Req(GuestTradeAction.EmissaryBalance, id: "r2")).Refusal);
    }

    [TestMethod]
    public void AHeroElsewhereOrInBattle_IsRefused()
    {
        Assert.AreEqual(GuestTradeRefusal.NotInSettlement,
            _host.Handle(Hero, Req(GuestTradeAction.EmissaryBalance, settlement: "town_B2")).Refusal);

        _hero = _hero with { InBattle = true };
        Assert.AreEqual(GuestTradeRefusal.NotInSettlement, _host.Handle(Hero, Req(GuestTradeAction.EmissaryBalance, id: "r2")).Refusal);
    }

    [TestMethod]
    public void NoEmissaryTrade_WhenAtWarOffOrNotAKeySettlementOrNoOffers()
    {
        var cases = new Action[]
        {
            () => _hero = _hero with { AtWarWithSettlement = true },
            () => _hero = _hero with { Owner = null },
            () => _emissary.IsEnabled.Returns(false),
            () => _emissary.IsKeySettlement(Town).Returns(false),
            () => _emissary.HasPurchasableOffers("rohan", "sturgia").Returns(false),
        };
        for (var i = 0; i < cases.Length; i++)
        {
            Setup();
            cases[i]();
            var reply = _host.Handle(Hero, Req(GuestTradeAction.EmissaryBuy, "elite", quantity: 1, id: "c" + i));
            Assert.AreEqual(GuestTradeRefusal.NoEmissaryTrade, reply.Refusal, "case " + i);
            _emissary.DidNotReceiveWithAnyArgs().Purchase(default!, default!, default!, default!, default);
        }
    }

    [TestMethod]
    public void ArmouryBalance_RepliesWithTheHerosOwnKingdomResource()
    {
        _shops.ArmouryLevel(Town).Returns(2);

        var reply = _host.Handle(Hero, Req(GuestTradeAction.ArmouryBalance));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual("silver", reply.ResourceId);
        Assert.AreEqual(12f, reply.Balance);
        Assert.AreEqual(2, reply.ArmouryLevel, "the guest builds its list at the host's level, visiting armourer included");
    }

    [TestMethod]
    public void ArmouryUpgrade_RunsTheMatchingOfferForTheGuestsHero()
    {
        var wanted = Offer("mail", "fine", "plate");
        _shops.ArmouryOffers(_hero, Arg.Any<int>()).Returns(new[] { Offer("mail", null, "plate"), wanted });
        _shops.Upgrade(_hero, wanted).Returns(UpgradeBlock.None);

        var reply = _host.Handle(Hero, Req(GuestTradeAction.ArmouryUpgrade, "mail", "fine", "plate"));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual((int)UpgradeBlock.None, reply.Status);
        _shops.Received(1).Upgrade(_hero, wanted);
    }

    [TestMethod]
    public void ArmouryUpgrade_WithAnEmptyModifier_MatchesThePlainPiece()
    {
        var plain = Offer("mail", null, "plate");
        _shops.ArmouryOffers(_hero, Arg.Any<int>()).Returns(new[] { plain });

        _host.Handle(Hero, Req(GuestTradeAction.ArmouryUpgrade, "mail", "", "plate"));

        _shops.Received(1).Upgrade(_hero, plain);
    }

    [TestMethod]
    public void ArmouryUpgrade_ForAPieceNoLongerOffered_IsNoLongerCarried()
    {
        _shops.ArmouryOffers(_hero, Arg.Any<int>()).Returns(new[] { Offer("mail", null, "plate") });

        var reply = _host.Handle(Hero, Req(GuestTradeAction.ArmouryUpgrade, "helm", null, "great_helm"));

        Assert.AreEqual((int)UpgradeBlock.NoLongerCarried, reply.Status);
        _shops.DidNotReceiveWithAnyArgs().Upgrade(default!, default!);
    }

    [TestMethod]
    public void ArmouryUpgrade_ForABlockedOffer_RepliesTheBlockAndChangesNothing()
    {
        _shops.ArmouryOffers(_hero, Arg.Any<int>()).Returns(new[] { Offer("mail", null, "plate", UpgradeBlock.NotEnoughGold) });

        var reply = _host.Handle(Hero, Req(GuestTradeAction.ArmouryUpgrade, "mail", null, "plate"));

        Assert.AreEqual((int)UpgradeBlock.NotEnoughGold, reply.Status);
        _shops.DidNotReceiveWithAnyArgs().Upgrade(default!, default!);
    }

    [TestMethod]
    public void NoArmoury_IsRefused()
    {
        _shops.HasArmoury(Town).Returns(false);

        Assert.AreEqual(GuestTradeRefusal.NoArmoury, _host.Handle(Hero, Req(GuestTradeAction.ArmouryBalance)).Refusal);
    }

    [TestMethod]
    public void BadRequests_AreRefusedBeforeAnyHeroIsRead()
    {
        var longText = new string('x', 129);
        var bad = new[]
        {
            Req(GuestTradeAction.None),
            Req((GuestTradeAction)99),
            Req(GuestTradeAction.EmissaryBalance, id: ""),
            Req(GuestTradeAction.EmissaryBalance, id: longText),
            Req(GuestTradeAction.EmissaryBalance, settlement: ""),
            Req(GuestTradeAction.EmissaryBalance, subject: longText),
            Req(GuestTradeAction.EmissaryBuy, quantity: 1),
            Req(GuestTradeAction.EmissaryBuy, "elite", quantity: 0),
            Req(GuestTradeAction.EmissaryBuy, "elite", quantity: 1001),
            Req(GuestTradeAction.ArmouryUpgrade, "mail"),
            Req(GuestTradeAction.ArmouryUpgrade, target: "plate"),
        };
        foreach (var request in bad)
            Assert.AreEqual(GuestTradeRefusal.BadRequest, _host.Handle(Hero, request).Refusal, request.ToString());

        _heroes.DidNotReceiveWithAnyArgs().Find(default!);
    }

    [TestMethod]
    public void AHostException_IsHostFailed()
    {
        _emissary.Purchase(default!, default!, default!, default!, default).ReturnsForAnyArgs(_ => throw new InvalidOperationException("boom"));

        var reply = _host.Handle(Hero, Req(GuestTradeAction.EmissaryBuy, "elite", quantity: 1));

        Assert.AreEqual(GuestTradeRefusal.HostFailed, reply.Refusal);
    }

    [TestMethod]
    public void ASettlementWithNoSpecialResource_RepliesWithNoBalance()
    {
        _resources.ResolveResource("rohan", "sturgia").Returns((SpecialResource?)null);

        var reply = _host.Handle(Hero, Req(GuestTradeAction.EmissaryBalance));

        Assert.AreEqual(GuestTradeRefusal.None, reply.Refusal);
        Assert.IsNull(reply.ResourceId);
        Assert.IsNull(reply.Balance);
    }

}
