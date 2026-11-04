using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Even sides fight mid-lane and rarely reach a tower, so a match between them would not end. From a set minute every wall weakens with each minute, so a long match still finishes.</summary>
[TestClass]
public class TowerWearTests
{
    private const float Minutes = 60f;

    [TestMethod]
    public void Factor_UpToTheStart_IsOne()
    {
        var wear = new TowerWear(15f, 0.15f);

        Assert.AreEqual(1f, wear.Factor(0f));
        Assert.AreEqual(1f, wear.Factor(15f));
    }

    [TestMethod]
    public void Factor_AfterTheStart_RisesEachMinute()
    {
        var wear = new TowerWear(15f, 0.15f);

        Assert.AreEqual(1.15f, wear.Factor(16f), 0.0001f);
        Assert.AreEqual(3.25f, wear.Factor(30f), 0.0001f);
        Assert.AreEqual(5.5f, wear.Factor(45f), 0.0001f);
    }

    [TestMethod]
    public void Factor_AtARateOfZero_StaysOne()
    {
        Assert.AreEqual(1f, new TowerWear(15f, 0f).Factor(100f));
    }

    [TestMethod]
    public void HasBegun_OnlyAfterTheStart_AndNeverAtARateOfZero()
    {
        Assert.IsFalse(new TowerWear(15f, 0.15f).HasBegun(15f));
        Assert.IsTrue(new TowerWear(15f, 0.15f).HasBegun(15.5f));
        Assert.IsFalse(new TowerWear(15f, 0f).HasBegun(100f));
    }

    private static FordsMatch Match(float limit = 0f) =>
        Create(Config(c => c with { Towers = c.Towers with { WearStartMinutes = 15f, WearPerMinute = 0.15f, AssaultDamagePerSecond = limit } }), Slots("slow"));

    private static TowerHit Blow(int raw = 100) => new(FordsTestLayout.WestOuter0, false, UnitRole.Infantry, raw, MatchSide.East);

    [TestMethod]
    public void AdjustTowerDamage_BeforeTheStart_IsUnchanged()
    {
        var match = Match();
        match.Tick(Snap(14f * Minutes));

        Assert.AreEqual(100, match.AdjustTowerDamage(Blow(), out _));
    }

    [TestMethod]
    public void AdjustTowerDamage_LateInTheMatch_IsRaised()
    {
        var match = Match();
        match.Tick(Snap(30f * Minutes));

        Assert.AreEqual(325, match.AdjustTowerDamage(Blow(), out var reason));
        Assert.AreEqual(TowerDamageReason.Applied, reason);
    }

    [TestMethod]
    public void TheLimit_RisesWithTheWear_SoItDoesNotHoldTheWeakenedWallsBack()
    {
        var match = Match(limit: 50f);
        match.Tick(Snap(30f * Minutes));

        Assert.AreEqual(162, match.AdjustTowerDamage(Blow(500), out _), "50 a second at 3.25 times");
    }

    [TestMethod]
    public void ALockedTower_StaysLocked_HoweverLateTheMatch()
    {
        var match = Match();
        match.Tick(Snap(30f * Minutes));

        var hit = new TowerHit(FordsTestLayout.WestInner0, false, UnitRole.Infantry, 100, MatchSide.East);
        Assert.AreEqual(0, match.AdjustTowerDamage(hit, out var reason));
        Assert.AreEqual(TowerDamageReason.Locked, reason);
    }

    [TestMethod]
    public void TheFeed_SaysSoOnce_WhenTheWallsBeginToWeaken()
    {
        var match = Match();

        Assert.AreEqual(0, match.Tick(Snap(15f * Minutes)).Events.Count(e => e.Kind == MatchEventKind.WallsWeaken));
        Assert.AreEqual(1, match.Tick(Snap(1f)).Events.Count(e => e.Kind == MatchEventKind.WallsWeaken));
        Assert.AreEqual(0, match.Tick(Snap(60f)).Events.Count(e => e.Kind == MatchEventKind.WallsWeaken));
    }
}
