using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// A faction's AI buys in its own order, so Mordor can field a horde and Rohan keep some foot soldiers. An entry is an offer id, or a role name that
/// means the dearest offer of that role the gold reaches. Without a cycle the AI buys the dearest offer it can afford.
/// </summary>
[TestClass]
public class AiBuyCycleTests
{
    private static readonly FordsAiProfile Hard = new("hard", 5f, 1.2f, 0f, 100000);

    private static FordsMatch Match(IReadOnlyList<string>? cycle, int gold = 5000)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.East] = factions[MatchSide.East] with { AiBuyCycle = cycle };
        return new FordsMatch(Config(c => c with { Economy = c.Economy with { StartingGold = gold } }), Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static List<string> BoughtOver(FordsMatch match, int decisions)
    {
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));
        var bought = new List<string>();
        for (var i = 1; i <= decisions; i++)
        {
            var view = match.GetView(EastSlot);
            bought.AddRange(ai.Decide(view, i * 10f).Where(a => a.Kind == AiActionKind.BuyPack).Select(a => a.Id));
        }

        return bought;
    }

    private static IReadOnlyList<UnitOffer> Offers => Factions()[MatchSide.East].Units;

    [TestMethod]
    public void Decide_WithACycle_BuysInItsOrder_AndWraps()
    {
        var first = Offers[0].OfferId;
        var second = Offers[1].OfferId;
        var match = Match(new[] { first, second, second });

        CollectionAssert.AreEqual(new[] { first, second, second, first }, BoughtOver(match, 4));
    }

    [TestMethod]
    public void Decide_WithoutACycle_BuysTheDearestItCanAfford()
    {
        var dearest = Offers.OrderByDescending(o => o.PackPrice).First().OfferId;

        Assert.AreEqual(dearest, BoughtOver(Match(null), 1).Single());
    }

    [TestMethod]
    public void Decide_TheNextEntryCostsTooMuch_SavesForIt_AndBuysNothingCheaper()
    {
        var dearest = Offers.OrderByDescending(o => o.PackPrice).First();
        var match = Match(new[] { dearest.OfferId }, gold: dearest.PackPrice - 1);

        CollectionAssert.AreEqual(new string[0], BoughtOver(match, 1));
    }

    [TestMethod]
    public void Decide_ARoleEntry_BuysTheDearestOfThatRoleTheGoldReaches()
    {
        var infantry = Offers.Where(o => o.Role == UnitRole.Infantry).OrderByDescending(o => o.PackPrice).ToList();
        Assert.IsTrue(infantry.Count > 1, "the fixture has two infantry offers");
        var match = Match(new[] { nameof(UnitRole.Infantry) });

        Assert.AreEqual(infantry[0].OfferId, BoughtOver(match, 1).Single());

        var poorer = Match(new[] { nameof(UnitRole.Infantry) }, gold: infantry[0].PackPrice - 1);

        Assert.AreEqual(infantry[1].OfferId, BoughtOver(poorer, 1).Single());
    }

    [TestMethod]
    public void Decide_AnEntryWithNothingOnShow_IsSkipped()
    {
        var first = Offers[0].OfferId;
        var match = Match(new[] { "not_on_show", first });

        Assert.AreEqual(first, BoughtOver(match, 1).Single());
    }

    [TestMethod]
    public void GetView_CarriesTheCycle()
    {
        var cycle = new[] { Offers[0].OfferId };

        CollectionAssert.AreEqual(cycle, Match(cycle).GetView(EastSlot).AiBuyCycle!.ToList());
        Assert.IsNull(Match(null).GetView(EastSlot).AiBuyCycle);
    }
}
