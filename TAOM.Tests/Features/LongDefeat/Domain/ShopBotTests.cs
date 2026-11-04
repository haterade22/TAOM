using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class ShopBotTests
{
    private static readonly ShopOffer[] Offers =
    {
        new("militia", "Militia", "m", FormationSlot.Infantry, 12, 180),
        new("yeomen", "Yeomen", "y", FormationSlot.Ranged, 10, 200),
    };

    private static RunView View(int gold, int standing = 100, int pending = 0, RunPhase phase = RunPhase.Preparation,
        ShopOffer[]? offers = null, int foot = 0, int ranged = 0) =>
        new(phase, 0, 0, gold, 60f, standing, pending, 0, 0f, offers ?? Offers, foot, ranged);

    [TestMethod]
    public void Decide_BuysTheFirstOfferTheGoldReaches()
    {
        var action = ShopBot.Decide(View(600), 0f);

        Assert.AreEqual(BotActionKind.Buy, action.Kind);
        Assert.AreEqual("militia", action.OfferId);
    }

    [TestMethod]
    public void Decide_SkipsAnOfferItCannotAfford()
    {
        var dearFirst = new ShopOffer[]
        {
            new("dear", "Dear", "d", FormationSlot.Infantry, 12, 500),
            new("militia", "Militia", "m", FormationSlot.Infantry, 12, 180),
        };

        var action = ShopBot.Decide(View(190, offers: dearFirst), 0f);

        Assert.AreEqual("militia", action.OfferId);
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(100), 0f).Kind);
    }

    [TestMethod]
    public void Decide_KeepsFootAndArchersTwoToOne_WithTheShippedOffers()
    {
        var gold = 600;
        int foot = 0, ranged = 0;
        var bought = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var action = ShopBot.Decide(View(gold, foot: foot, ranged: ranged), 0f);
            Assert.AreEqual(BotActionKind.Buy, action.Kind);
            var offer = Offers.Single(o => o.Id == action.OfferId);
            gold -= offer.Price;
            if (offer.Slot == FormationSlot.Infantry)
                foot++;
            else
                ranged++;
            bought.Add(offer.Id);
        }

        CollectionAssert.AreEqual(new[] { "militia", "militia", "yeomen" }, bought);
    }

    [TestMethod]
    public void Decide_TheClassBehindHasNothingAffordable_BuysTheOther()
    {
        Assert.AreEqual("militia", ShopBot.Decide(View(190, foot: 2, ranged: 0), 0f).OfferId);
        var dearFoot = new ShopOffer[]
        {
            new("militia", "Militia", "m", FormationSlot.Infantry, 12, 400),
            new("yeomen", "Yeomen", "y", FormationSlot.Ranged, 10, 200),
        };
        Assert.AreEqual("yeomen", ShopBot.Decide(View(250, offers: dearFoot), 0f).OfferId, "foot is behind but not affordable");
    }

    [TestMethod]
    public void Decide_StopsBuyingAtOneSeventyDefenders_PendingIncluded()
    {
        Assert.AreEqual(BotActionKind.Buy, ShopBot.Decide(View(600, standing: 150, pending: 19), 0f).Kind);
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(600, standing: 150, pending: 20), 0f).Kind);
    }

    [TestMethod]
    public void Decide_SoundsTheHornOnlyOnceNothingIsLeftToBuyAndThirtySecondsHavePassed()
    {
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(10), 29.9f).Kind);
        Assert.AreEqual(BotActionKind.Horn, ShopBot.Decide(View(10), 30f).Kind);
        Assert.AreEqual(BotActionKind.Buy, ShopBot.Decide(View(600), 45f).Kind, "buying comes before the horn");
    }

    [TestMethod]
    public void Decide_NaNSeconds_NeverSoundsTheHorn()
    {
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(10), float.NaN).Kind);
    }

    [TestMethod]
    public void Decide_ShopClosed_Waits()
    {
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(600, phase: RunPhase.Wave), 100f).Kind);
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(600, phase: RunPhase.Ended), 100f).Kind);
    }
}
