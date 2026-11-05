using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Even sides lose only outer towers and clear no lane, so a match between them did not end. At the time limit the side holding more of the field wins.</summary>
[TestClass]
public class FordsTimeLimitTests
{
    private const float Limit = 40f;
    private const float Minutes = 60f;

    private static FordsMatch Match(float limit = Limit) => Create(Config(c => c with { TimeLimitMinutes = limit }), Slots("slow"));

    private static TowerChain Chain() => new(FordsTestLayout.Create(), FordsMatchConfig.Default.Towers);

    [TestMethod]
    public void Tick_BeforeTheLimit_LeavesTheMatchLive()
    {
        var match = Match();

        var effects = match.Tick(Snap((Limit - 1f) * Minutes));

        Assert.IsNull(effects.Winner);
        Assert.AreEqual(MatchPhase.Live, match.Phase);
    }

    [TestMethod]
    public void Tick_AtTheLimit_EndsTheMatch_AndTheSideWithMoreTowersWins()
    {
        var match = Match();
        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);

        var effects = match.Tick(Snap(Limit * Minutes));

        Assert.AreEqual(MatchSide.West, effects.Winner);
        Assert.AreEqual(MatchPhase.Ended, match.Phase);
        Assert.AreEqual(1, effects.Events.Count(e => e.Kind == MatchEventKind.TimeUp));
    }

    [TestMethod]
    public void AdjustTowerDamage_AfterTheMatchEnds_TakesNothing_SoNoKeepFallsUnderTheResult()
    {
        var match = Match();
        match.Tick(Snap(Limit * Minutes));

        var applied = match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Hero, 500, MatchSide.West), out var reason);

        Assert.AreEqual(0, applied);
        Assert.AreEqual(TowerDamageReason.MatchEnded, reason);
    }

    [TestMethod]
    public void ReasonFor_AfterTheMatchEnds_SaysMatchEnded_AsTheDamageDoes()
    {
        var match = Match();
        match.Tick(Snap(Limit * Minutes));

        Assert.AreEqual(TowerDamageReason.MatchEnded, match.ReasonFor(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 0, MatchSide.East)));
    }

    [TestMethod]
    public void Tick_LimitZero_NeverEndsTheMatch()
    {
        var match = Match(0f);

        Assert.IsNull(match.Tick(Snap(500f * Minutes)).Winner);
        Assert.AreEqual(MatchPhase.Live, match.Phase);
    }

    [TestMethod]
    public void Tick_FiveMinutesBeforeTheLimit_WarnsOnce()
    {
        var match = Match();
        Assert.AreEqual(0, match.Tick(Snap((Limit - 5.5f) * Minutes)).Events.Count(e => e.Kind == MatchEventKind.TimeRunningOut));

        Assert.AreEqual(1, match.Tick(Snap(Minutes)).Events.Count(e => e.Kind == MatchEventKind.TimeRunningOut));
        Assert.AreEqual(0, match.Tick(Snap(Minutes)).Events.Count(e => e.Kind == MatchEventKind.TimeRunningOut));
    }

    [TestMethod]
    public void GetView_SaysAMatchEndedOnTime_AndAFallenKeepDoesNot()
    {
        var timed = Match();
        timed.Tick(Snap(Limit * Minutes));
        Assert.IsTrue(timed.GetView(WestSlot).EndedOnTime);

        var stormed = Match();
        stormed.OnTowerDestroyed(FordsTestLayout.EastKeep, null);
        Assert.IsFalse(stormed.GetView(WestSlot).EndedOnTime);
    }

    [TestMethod]
    public void TimeVictory_EqualTowers_TheSideWithMoreHitPointsLeftWins()
    {
        var chain = Chain();
        chain.ApplyDamage(FordsTestLayout.WestKeep, 100);

        Assert.AreEqual(MatchSide.East, TimeVictory.Winner(chain));
    }

    [TestMethod]
    public void TimeVictory_MoreTowers_BeatsMoreHitPoints()
    {
        var chain = Chain();
        chain.OnDestroyed(FordsTestLayout.WestOuter0);
        chain.ApplyDamage(FordsTestLayout.EastKeep, 9000);
        chain.ApplyDamage(FordsTestLayout.EastOuter0, 7000);

        Assert.AreEqual(MatchSide.East, TimeVictory.Winner(chain));
    }

    [TestMethod]
    public void TimeVictory_AnExactTie_GoesToWest_SoAMatchAlwaysHasAWinner()
    {
        Assert.AreEqual(MatchSide.West, TimeVictory.Winner(Chain()));
    }

    [TestMethod]
    public void Standing_CountsTowersWithoutTheKeep_AndSharesHitPointsWithIt()
    {
        var chain = Chain();
        var whole = chain.Standing(MatchSide.West);
        Assert.AreEqual(1f, whole.HitPointShare, 0.0001f);

        chain.OnDestroyed(FordsTestLayout.WestOuter0);
        var hurt = chain.Standing(MatchSide.West);

        Assert.AreEqual(whole.Towers - 1, hurt.Towers);
        Assert.IsTrue(hurt.HitPointShare < 1f);
    }
}
