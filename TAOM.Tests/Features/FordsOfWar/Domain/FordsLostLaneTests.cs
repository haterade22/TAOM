using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>A lane whose towers have all fallen sends no more free waves. Fresh waves at the base used to stop attackers far short of an open keep, so a match did not end. Bought packs still come, so a comeback stays possible.</summary>
[TestClass]
public class FordsLostLaneTests
{
    private const float PastAWave = 41f;

    private static FordsMatch Match() => Create(Config(WithFreeWaves), Slots("slow"));

    private static int Units(MatchEffects effects, MatchSide side, int lane) =>
        effects.Spawns.Where(r => r.Side == side && r.Lane == lane && !r.IsHero).Sum(r => r.Count);

    [TestMethod]
    public void ALaneWithATowerStanding_KeepsItsFreeWaves()
    {
        var match = Match();
        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);

        Assert.IsTrue(Units(match.Tick(Snap(PastAWave)), MatchSide.East, 0) > 0);
    }

    [TestMethod]
    public void ALaneWithNoTowerLeft_SendsNoFreeWave_AndNoOtherLaneOrSideChanges()
    {
        var match = Match();
        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);
        match.OnTowerDestroyed(FordsTestLayout.EastInner0, null);

        var effects = match.Tick(Snap(PastAWave));

        Assert.AreEqual(0, Units(effects, MatchSide.East, 0));
        Assert.IsTrue(Units(effects, MatchSide.East, 1) > 0, "the side's other lane still has its towers");
        Assert.IsTrue(Units(effects, MatchSide.West, 0) > 0, "the attackers' own waves are untouched");
    }

    [TestMethod]
    public void ALostLane_StillTakesABoughtPack()
    {
        var match = Match();
        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);
        match.OnTowerDestroyed(FordsTestLayout.EastInner0, null);

        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(EastSlot, 0, Footmen.OfferId));

        Assert.AreEqual(Footmen.PackSize, Units(match.Tick(Snap(1f)), MatchSide.East, 0));
    }

    [TestMethod]
    public void LaneLost_ReadsOnlyThatSidesTowersInThatLane()
    {
        var chain = new TowerChain(FordsTestLayout.Create(), FordsMatchConfig.Default.Towers);
        Assert.IsFalse(chain.LaneLost(MatchSide.East, 0));

        chain.OnDestroyed(FordsTestLayout.EastOuter0);
        Assert.IsFalse(chain.LaneLost(MatchSide.East, 0));

        chain.OnDestroyed(FordsTestLayout.EastInner0);
        Assert.IsTrue(chain.LaneLost(MatchSide.East, 0));
        Assert.IsFalse(chain.LaneLost(MatchSide.East, 1));
        Assert.IsFalse(chain.LaneLost(MatchSide.West, 0));
    }
}
