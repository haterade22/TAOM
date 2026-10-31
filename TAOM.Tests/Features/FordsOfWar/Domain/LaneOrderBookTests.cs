using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Push, Hold and Fall back per lane per side, with a cooldown between changes.</summary>
[TestClass]
public class LaneOrderBookTests
{
    private static readonly FordsLaneConfig Config = FordsMatchConfig.Default.Lanes;

    [TestMethod]
    public void Order_Untouched_IsPush()
    {
        var book = new LaneOrderBook(Config);

        Assert.AreEqual(LaneOrder.Push, book.Order(MatchSide.West, 0));
        Assert.AreEqual(LaneOrder.Push, book.Order(MatchSide.East, 2));
    }

    [TestMethod]
    public void TrySet_FirstChange_AppliesAndStartsTheCooldown()
    {
        var book = new LaneOrderBook(Config);

        var result = book.TrySet(MatchSide.West, 1, LaneOrder.Hold, 100f);

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(LaneOrder.Hold, book.Order(MatchSide.West, 1));
        Assert.AreEqual(Config.OrderCooldownSeconds, book.CooldownLeft(MatchSide.West, 1, 100f), 1e-4f);
    }

    [TestMethod]
    public void TrySet_DuringTheCooldown_IsRefusedAndKeepsTheOrder()
    {
        var book = new LaneOrderBook(Config);
        book.TrySet(MatchSide.West, 1, LaneOrder.Hold, 100f);

        var result = book.TrySet(MatchSide.West, 1, LaneOrder.FallBack, 105f);

        Assert.AreEqual(CommandResult.OnCooldown, result);
        Assert.AreEqual(LaneOrder.Hold, book.Order(MatchSide.West, 1));
    }

    [TestMethod]
    public void TrySet_ExactlyWhenTheCooldownEnds_Applies()
    {
        var book = new LaneOrderBook(Config);
        book.TrySet(MatchSide.West, 1, LaneOrder.Hold, 100f);

        var result = book.TrySet(MatchSide.West, 1, LaneOrder.FallBack, 100f + Config.OrderCooldownSeconds);

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(LaneOrder.FallBack, book.Order(MatchSide.West, 1));
    }

    [TestMethod]
    public void TrySet_SameOrderAgain_IsOkAndDoesNotStartACooldown()
    {
        var book = new LaneOrderBook(Config);

        var result = book.TrySet(MatchSide.West, 0, LaneOrder.Push, 50f);

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(0f, book.CooldownLeft(MatchSide.West, 0, 50f));
        Assert.AreEqual(CommandResult.Ok, book.TrySet(MatchSide.West, 0, LaneOrder.Hold, 51f));
    }

    [TestMethod]
    public void TrySet_OtherLaneOrSide_HasItsOwnCooldown()
    {
        var book = new LaneOrderBook(Config);
        book.TrySet(MatchSide.West, 0, LaneOrder.Hold, 10f);

        Assert.AreEqual(CommandResult.Ok, book.TrySet(MatchSide.West, 1, LaneOrder.Hold, 11f));
        Assert.AreEqual(CommandResult.Ok, book.TrySet(MatchSide.East, 0, LaneOrder.FallBack, 11f));
        Assert.AreEqual(LaneOrder.Push, book.Order(MatchSide.East, 1));
    }

    [TestMethod]
    public void TrySet_ChangingBackToPush_AlsoCostsACooldown()
    {
        var book = new LaneOrderBook(Config);
        book.TrySet(MatchSide.East, 2, LaneOrder.Hold, 0f);
        book.TrySet(MatchSide.East, 2, LaneOrder.Push, Config.OrderCooldownSeconds);

        Assert.AreEqual(LaneOrder.Push, book.Order(MatchSide.East, 2));
        Assert.AreEqual(CommandResult.OnCooldown, book.TrySet(MatchSide.East, 2, LaneOrder.Hold, Config.OrderCooldownSeconds + 1f));
    }

    [TestMethod]
    public void CooldownLeft_ShrinksWithTimeAndFloorsAtZero()
    {
        var book = new LaneOrderBook(Config);
        book.TrySet(MatchSide.West, 0, LaneOrder.Hold, 0f);

        Assert.AreEqual(Config.OrderCooldownSeconds - 5f, book.CooldownLeft(MatchSide.West, 0, 5f), 1e-4f);
        Assert.AreEqual(0f, book.CooldownLeft(MatchSide.West, 0, 1000f));
    }

    [TestMethod]
    public void CooldownLeft_NeverSet_IsZero()
    {
        Assert.AreEqual(0f, new LaneOrderBook(Config).CooldownLeft(MatchSide.East, 0, 0f));
    }
}
