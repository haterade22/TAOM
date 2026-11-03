using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// Every FreeArrivalSeconds of live time each side's next hero takes the field for nothing, so both sides see their heroes whatever their gold. The cheapest comes first, into the
/// lane where the side is most outnumbered. A side with no slot free or no hero left waits for the next arrival. Buying a hero with gold is a separate path and stays.
/// </summary>
[TestClass]
public class FordsHeroArrivalTests
{
    private const float Arrival = 300f;

    private static FordsMatch Match(float seconds = Arrival, int maxAlive = 2) =>
        Create(Config(c => c with { Heroes = c.Heroes with { FreeArrivalSeconds = seconds, MaxAlivePerSide = maxAlive } }), Slots("slow"));

    private static List<string> Heroes(MatchEffects effects, MatchSide side) =>
        effects.Spawns.Where(r => r.IsHero && r.Side == side).Select(r => r.TroopId).ToList();

    [TestMethod]
    public void BeforeTheFirstArrival_NoHeroComes()
    {
        var effects = Match().Tick(Snap(Arrival - 1f));

        Assert.AreEqual(0, effects.Spawns.Count(r => r.IsHero));
    }

    [TestMethod]
    public void AtTheArrival_EachSideFieldsItsCheapestHero_ForNothing()
    {
        var match = Match();
        match.Tick(Snap(Arrival - 1f));

        var effects = match.Tick(Snap(2f));

        CollectionAssert.AreEqual(new[] { HeroB.HeroId }, Heroes(effects, MatchSide.West));
        CollectionAssert.AreEqual(new[] { HeroB.HeroId }, Heroes(effects, MatchSide.East));
        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
        StringAssert.Contains(match.GoldTotals().Single(g => g.Slot == WestSlot).Breakdown, "heroes=0");
    }

    [TestMethod]
    public void TheNextArrival_BringsTheNextHero()
    {
        var match = Match();
        match.Tick(Snap(Arrival + 1f));

        var effects = match.Tick(Snap(Arrival));

        CollectionAssert.AreEqual(new[] { HeroA.HeroId }, Heroes(effects, MatchSide.West));
        CollectionAssert.AreEqual(new[] { HeroA.HeroId }, Heroes(effects, MatchSide.East));
    }

    [TestMethod]
    public void WithNoHeroLeft_TheArrivalBringsNothing()
    {
        var match = Match();
        match.Tick(Snap(Arrival + 1f));
        match.Tick(Snap(Arrival));

        var effects = match.Tick(Snap(Arrival));

        Assert.AreEqual(0, effects.Spawns.Count(r => r.IsHero));
    }

    [TestMethod]
    public void WithEverySlotFull_TheArrivalSkipsThatSide_AndTheOtherSideStillGetsItsHero()
    {
        var match = Match(maxAlive: 1);
        Assert.AreEqual(CommandResult.Ok, match.TryBuyHero(WestSlot, 0, HeroA.HeroId));
        match.Tick(Snap(1f));

        var effects = match.Tick(Snap(Arrival));

        Assert.AreEqual(0, Heroes(effects, MatchSide.West).Count);
        CollectionAssert.AreEqual(new[] { HeroB.HeroId }, Heroes(effects, MatchSide.East));
    }

    [TestMethod]
    public void AHeroAlreadyBought_DoesNotComeAgain()
    {
        var match = Match();
        Assert.AreEqual(CommandResult.Ok, match.TryBuyHero(WestSlot, 0, HeroB.HeroId));
        match.Tick(Snap(1f));

        var effects = match.Tick(Snap(Arrival));

        CollectionAssert.AreEqual(new[] { HeroA.HeroId }, Heroes(effects, MatchSide.West));
    }

    [TestMethod]
    public void TheHero_GoesWhereItsSideIsMostOutnumbered()
    {
        var lanes = new[] { Live(MatchSide.West, 0, 10), Live(MatchSide.East, 0, 10), Live(MatchSide.West, 1, 2), Live(MatchSide.East, 1, 30) };

        var effects = Match().Tick(Snap(Arrival + 1f, lanes));

        Assert.AreEqual(1, effects.Spawns.Single(r => r.IsHero && r.Side == MatchSide.West).Lane);
        Assert.AreEqual(0, effects.Spawns.Single(r => r.IsHero && r.Side == MatchSide.East).Lane);
    }

    [TestMethod]
    public void AFreeHero_StillHasABuybackPrice()
    {
        var match = Match();
        match.Tick(Snap(Arrival + 1f));

        var hero = match.GetView(WestSlot).Heroes.Single(h => h.Side == MatchSide.West && h.HeroId == HeroB.HeroId);

        Assert.IsTrue(hero.BuybackPrice > 0, "the buyback is priced on the hero, not on what the side paid");
    }

    [TestMethod]
    public void Zero_TurnsTheArrivalsOff()
    {
        var match = Match(seconds: 0f);

        var effects = match.Tick(Snap(Arrival * 3f));

        Assert.AreEqual(0, effects.Spawns.Count(r => r.IsHero));
        Assert.IsNull(match.GetView(WestSlot).NextFreeHeroIn);
    }

    [TestMethod]
    public void TheView_CountsDownToTheNextArrival_AndShowsNothingWhenNoneCanCome()
    {
        var match = Match(maxAlive: 1);
        match.Tick(Snap(100f));

        Assert.AreEqual(200f, match.GetView(WestSlot).NextFreeHeroIn!.Value, 0.01f);

        match.Tick(Snap(Arrival));

        Assert.IsNull(match.GetView(WestSlot).NextFreeHeroIn, "the only slot is taken, so the next arrival would bring nothing");
    }
}
