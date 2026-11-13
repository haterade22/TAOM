using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// The day's tribute (docs/features/realms.md): a share of each vassal clan's fief income, split among its liege clans,
/// with paying and receiving set apart in the mod options.
/// </summary>
[TestClass]
public class TributeSheetTests
{
    private static readonly Dictionary<string, int> Income = new() { ["count"] = 2000, ["duke"] = 6000, ["poor"] = 1000, ["empty"] = 0 };
    private static readonly Dictionary<string, int> Gold = new() { ["count"] = 50000, ["duke"] = 50000, ["poor"] = 10, ["empty"] = 0 };

    private static TributeSheet Build(int receive, int pay, params (string, string)[] bonds) =>
        TributeSheet.Build(bonds, id => Income[id], id => Gold[id], receive, pay);

    [TestMethod]
    public void A_vassal_pays_its_share_and_the_liege_receives_it()
    {
        var sheet = Build(5, 5, ("count", "duke"), ("duke", "king"));

        Assert.AreEqual(100, sheet.PaidBy("count"));
        Assert.AreEqual(100, sheet.From("duke").Single().Received);
        Assert.AreEqual(300, sheet.PaidBy("duke"));
        Assert.AreEqual(300, sheet.ReceivedBy("king"));
        Assert.AreEqual(0, sheet.PaidBy("king"));
    }

    [TestMethod]
    public void Tribute_received_is_not_part_of_the_base_so_it_never_passes_up()
    {
        var sheet = Build(5, 5, ("count", "duke"), ("duke", "king"));

        Assert.AreEqual(300, sheet.PaidBy("duke"), "5% of the duke's fiefs, not of fiefs plus the count's tribute");
    }

    [TestMethod]
    public void A_clan_with_two_lieges_splits_its_share()
    {
        var sheet = Build(5, 5, ("duke", "king"), ("duke", "queen"), ("duke", "king"));

        Assert.AreEqual(2, sheet.To("duke").Count, "a repeated bond counts once");
        Assert.AreEqual(150, sheet.ReceivedBy("king"));
        Assert.AreEqual(150, sheet.ReceivedBy("queen"));
        Assert.AreEqual(300, sheet.PaidBy("duke"));
    }

    [TestMethod]
    public void Paying_and_receiving_are_set_apart()
    {
        var receiveOnly = Build(10, 0, ("count", "duke"));
        Assert.AreEqual(0, receiveOnly.PaidBy("count"));
        Assert.AreEqual(200, receiveOnly.ReceivedBy("duke"));

        var payOnly = Build(0, 3, ("count", "duke"));
        Assert.AreEqual(60, payOnly.PaidBy("count"));
        Assert.AreEqual(0, payOnly.ReceivedBy("duke"));

        Assert.AreSame(TributeSheet.Empty, Build(0, 0, ("count", "duke")));
    }

    [TestMethod]
    public void A_clan_that_cannot_pay_owes_nothing_that_day()
    {
        var sheet = Build(5, 5, ("poor", "duke"));
        Assert.AreEqual(0, sheet.PaidBy("poor"));
        Assert.AreEqual(0, sheet.ReceivedBy("duke"), "the liege is not paid what the vassal could not pay");

        Assert.AreEqual(50, Build(5, 0, ("poor", "duke")).ReceivedBy("duke"), "with paying off, nothing is taken, so gold does not matter");
    }

    [TestMethod]
    public void No_fief_income_and_kin_owe_nothing()
    {
        var sheet = Build(5, 5, ("empty", "duke"), ("duke", "duke"));

        Assert.AreEqual(0, sheet.Lines.Count);
        Assert.AreEqual(0, sheet.To("unknown").Count);
    }
}
