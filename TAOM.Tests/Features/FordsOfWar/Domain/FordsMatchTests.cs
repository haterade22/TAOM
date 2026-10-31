using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The aggregate's clock, income, waves, purchases, lane orders and lane commands.</summary>
[TestClass]
public class FordsMatchTests
{
    [TestMethod]
    public void Tick_Countdown_OnlyCountsDown()
    {
        var match = Create(Config(c => c with { CountdownSeconds = 10f, Waves = c.Waves with { FreeWaveIntervalSeconds = 40f }, Economy = c.Economy with { IncomePerMinute = 120 } }));

        var effects = match.Tick(Snap(4f));

        var view = match.GetView(WestSlot);
        Assert.AreEqual(MatchPhase.Countdown, view.Phase);
        Assert.AreEqual(0f, view.Elapsed);
        Assert.AreEqual(1000, view.Gold);
        Assert.AreEqual(0, effects.Spawns.Count);
        Assert.AreEqual(0, effects.Commands.Count);
    }

    [TestMethod]
    public void Tick_CountdownEnds_GoesLiveAndKeepsTheLeftover()
    {
        var match = Create(Config(c => c with { CountdownSeconds = 10f }));
        match.Tick(Snap(4f));

        match.Tick(Snap(7f));

        var view = match.GetView(WestSlot);
        Assert.AreEqual(MatchPhase.Live, view.Phase);
        Assert.AreEqual(1f, view.Elapsed, 1e-4f);
    }

    [TestMethod]
    public void TryBuyPack_DuringCountdown_IsNotLive()
    {
        var match = Create(Config(c => c with { CountdownSeconds = 10f }));

        Assert.AreEqual(CommandResult.NotLive, match.TryBuyPack(WestSlot, 0, "inf"));
        Assert.AreEqual(CommandResult.NotLive, match.TryBuyHero(WestSlot, 0, "hero_a"));
        Assert.AreEqual(CommandResult.NotLive, match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Hold));
        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void Tick_Live_PaysPassiveIncomeAndTheAiMultiplier()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { IncomePerMinute = 120 } }), Slots("slow"));

        match.Tick(Snap(60f));

        Assert.AreEqual(1120, match.GetView(WestSlot).Gold);
        Assert.AreEqual(1180, match.GetView(EastSlot).Gold);
    }

    [TestMethod]
    public void Tick_ShortTicks_DoNotLosePassiveIncome()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { IncomePerMinute = 120 } }), Slots(null));

        for (var i = 0; i < 100; i++)
            match.Tick(Snap(0.1f));

        Assert.AreEqual(1020, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void Tick_CapturedPoint_RaisesTheEventAndPaysCaptureIncome()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { IncomePerMinute = 120 } }), Slots("slow"));

        var effects = match.Tick(Snap(20f, capture: new[] { new CaptureCounts("point_0", 3, 0) }));

        var captured = effects.Events.Single(e => e.Kind == MatchEventKind.PointCaptured);
        Assert.AreEqual(MatchSide.West, captured.Side);
        Assert.AreEqual("point_0", captured.Subject);
        Assert.AreEqual(MatchSide.West, match.GetView(WestSlot).CapturePoints.Single().Owner);
        Assert.AreEqual(1000 + 40 + 20, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void Tick_FreeWaves_SpawnForTheLaneOwners()
    {
        var match = Create(Config(WithFreeWaves));

        var effects = match.Tick(Snap(1f));

        Assert.AreEqual(4, effects.Spawns.Count);
        Assert.IsTrue(effects.Spawns.Where(s => s.Side == MatchSide.West).All(s => s.OwnerSlot == WestSlot && s.TroopId == "footman" && s.Count == 5));
        Assert.IsTrue(effects.Spawns.Where(s => s.Side == MatchSide.East).All(s => s.OwnerSlot == EastSlot));
        Assert.AreEqual(4, effects.Spawns.Select(s => s.RequestId).Distinct().Count());
    }

    [TestMethod]
    public void Tick_LaneAtItsLiveCap_GetsNoFreeWave()
    {
        var match = Create(Config(WithFreeWaves));
        var full = new[] { Live(MatchSide.West, 0, 90), Live(MatchSide.West, 1, 90) };

        var effects = match.Tick(Snap(1f, full));

        Assert.IsFalse(effects.Spawns.Any(s => s.Side == MatchSide.West));
        Assert.IsTrue(effects.Spawns.Any(s => s.Side == MatchSide.East));
    }

    [TestMethod]
    public void TryBuyPack_NotEnoughGold_FailsAndKeepsTheGold()
    {
        var match = Create();

        var result = match.TryBuyPack(WestSlot, 0, "elite");

        Assert.AreEqual(CommandResult.NotEnoughGold, result);
        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
        Assert.AreEqual(0, match.Tick(Snap()).Spawns.Count);
    }

    [TestMethod]
    public void TryBuyPack_EnoughGold_ChargesAndQueuesTheSpawn()
    {
        var match = Create();

        var result = match.TryBuyPack(WestSlot, 1, "spear");
        var effects = match.Tick(Snap());

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(880, match.GetView(WestSlot).Gold);
        var spawn = effects.Spawns.Single();
        Assert.AreEqual(MatchSide.West, spawn.Side);
        Assert.AreEqual(1, spawn.Lane);
        Assert.AreEqual("spearman", spawn.TroopId);
        Assert.AreEqual(8, spawn.Count);
        Assert.AreEqual(WestSlot, spawn.OwnerSlot);
        Assert.AreEqual(UnitRole.Spear, spawn.Role);
        Assert.IsFalse(spawn.IsHero);
        var bought = effects.Events.Single(e => e.Kind == MatchEventKind.PackBought);
        Assert.AreEqual(MatchSide.West, bought.Side);
        Assert.AreEqual("spear", bought.Subject);
        Assert.AreEqual(120, bought.Amount);
    }

    [TestMethod]
    public void TryBuyPack_BadRequests_ReturnTheMatchingResult()
    {
        var match = Create();

        Assert.AreEqual(CommandResult.UnknownOffer, match.TryBuyPack(WestSlot, 0, "nope"));
        Assert.AreEqual(CommandResult.UnknownLane, match.TryBuyPack(WestSlot, 9, "inf"));
        Assert.AreEqual(CommandResult.UnknownLane, match.TryBuyPack(WestSlot, -1, "inf"));
        Assert.AreEqual(CommandResult.LaneNotOwned, match.TryBuyPack(99, 0, "inf"));
        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void TryBuyPack_LaneTheSlotDoesNotOwn_IsRefused()
    {
        var slots = new[]
        {
            new CommanderSlot(1, MatchSide.West, true, null, new[] { 0 }),
            new CommanderSlot(3, MatchSide.West, true, null, new[] { 1 }),
            new CommanderSlot(2, MatchSide.East, false, "slow", new[] { 0, 1 }),
        };
        var match = Create(slots: slots);

        Assert.AreEqual(CommandResult.LaneNotOwned, match.TryBuyPack(1, 1, "inf"));
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(3, 1, "inf"));
        Assert.AreEqual(1000, match.GetView(1).Gold);
        Assert.AreEqual(900, match.GetView(3).Gold);
    }

    [TestMethod]
    public void TryBuyPack_QueueFull_FailsWithoutCharging()
    {
        var match = Create();
        for (var i = 0; i < 3; i++)
            Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 0, "inf"));

        var result = match.TryBuyPack(WestSlot, 0, "inf");

        Assert.AreEqual(CommandResult.QueueFull, result);
        Assert.AreEqual(700, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void TrySetLaneOrder_OnCooldown_IsRefusedUntilItEnds()
    {
        var match = Create();

        Assert.AreEqual(CommandResult.Ok, match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Hold));
        Assert.AreEqual(CommandResult.OnCooldown, match.TrySetLaneOrder(WestSlot, 0, LaneOrder.FallBack));
        Assert.AreEqual(20f, match.GetView(WestSlot).Lanes[0].CooldownLeft, 1e-3f);
        Assert.AreEqual(CommandResult.Ok, match.TrySetLaneOrder(WestSlot, 1, LaneOrder.FallBack));

        match.Tick(Snap(20f));

        Assert.AreEqual(CommandResult.Ok, match.TrySetLaneOrder(WestSlot, 0, LaneOrder.FallBack));
        Assert.AreEqual(LaneOrder.FallBack, match.GetView(WestSlot).Lanes[0].Order);
    }

    [TestMethod]
    public void TrySetLaneOrder_BadLaneOrSlot_ReturnsTheMatchingResult()
    {
        var match = Create();

        Assert.AreEqual(CommandResult.UnknownLane, match.TrySetLaneOrder(WestSlot, 7, LaneOrder.Hold));
        Assert.AreEqual(CommandResult.LaneNotOwned, match.TrySetLaneOrder(42, 0, LaneOrder.Hold));
    }

    [TestMethod]
    public void Tick_LaneWithUnits_MarchesToTheNextWaypoint()
    {
        var match = Create();
        var lanes = new[] { Live(MatchSide.West, 0, 10, new Vec3f(100f, 0f, 0f)) };

        var effects = match.Tick(Snap(1f, lanes));

        var command = effects.Commands.Single();
        Assert.AreEqual(MatchSide.West, command.Side);
        Assert.AreEqual(0, command.Lane);
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command.Command);
    }

    [TestMethod]
    public void Tick_EnemyInEngageRange_Charges()
    {
        var match = Create();
        var lanes = new[] { Live(MatchSide.West, 0, 10, new Vec3f(100f, 0f, 0f), nearestEnemy: 20f) };

        var effects = match.Tick(Snap(1f, lanes));

        Assert.AreEqual(LaneCommand.Charge, effects.Commands.Single().Command);
    }

    [TestMethod]
    public void Tick_NextEnemyTowerInReach_AttacksIt()
    {
        var match = Create();
        var lanes = new[] { Live(MatchSide.West, 0, 10, new Vec3f(637f, 0f, 0f)) };

        var effects = match.Tick(Snap(1f, lanes));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), effects.Commands.Single().Command);
    }

    [TestMethod]
    public void Tick_OuterTowerFallen_TargetsTheInnerTower()
    {
        var match = Create();
        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);
        var lanes = new[] { Live(MatchSide.West, 0, 10, new Vec3f(797f, 0f, 0f)) };

        var effects = match.Tick(Snap(1f, lanes));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastInner0), effects.Commands.Single().Command);
    }

    [TestMethod]
    public void Tick_HoldOrder_HoldsBeforeTheTower()
    {
        var match = Create();
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Hold);
        var lanes = new[] { Live(MatchSide.West, 0, 10, new Vec3f(100f, 0f, 0f)) };

        var effects = match.Tick(Snap(1f, lanes));

        var command = effects.Commands.Single().Command;
        Assert.AreEqual(LaneCommandKind.HoldAt, command.Kind);
        Assert.AreEqual(580f, command.Target.X, 1e-2f);
    }

    [TestMethod]
    public void Tick_LaneWithoutUnits_GetsNoCommand()
    {
        var match = Create();
        var lanes = new[] { Live(MatchSide.West, 0, 0) };

        var effects = match.Tick(Snap(1f, lanes));

        Assert.AreEqual(0, effects.Commands.Count);
    }

    [TestMethod]
    public void Tick_EnemyKeepOnceItsLaneIsCleared_BecomesTheTarget()
    {
        var match = Create();
        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);
        match.OnTowerDestroyed(FordsTestLayout.EastInner0, null);
        var lanes = new[] { Live(MatchSide.West, 0, 10, new Vec3f(955f, 25f, 0f)) };

        var effects = match.Tick(Snap(1f, lanes));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastKeep), effects.Commands.Single().Command);
    }

    [TestMethod]
    public void Tick_TowerWithATargetInRange_FiresAVolleyThenReloads()
    {
        var match = Create();
        var targets = new[] { Threat(FordsTestLayout.EastOuter0, 2, 30f) };

        var first = match.Tick(Snap(1f, targets: targets));
        var second = match.Tick(Snap(1f, targets: targets));
        var later = match.Tick(Snap(4f, targets: targets));

        var volley = first.Volleys.Single();
        Assert.AreEqual(FordsTestLayout.EastOuter0, volley.TowerId);
        Assert.AreEqual(3, volley.Missiles);
        Assert.AreEqual(0, second.Volleys.Count);
        Assert.AreEqual(1, later.Volleys.Count);
    }

    [TestMethod]
    public void Tick_DamagedTower_RegeneratesAfterTheDelayAndReportsTheHeal()
    {
        var match = Create();
        match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 100));

        var quiet = match.Tick(Snap(15f));
        var effects = match.Tick(Snap(10f));

        Assert.AreEqual(0, quiet.Heals.Count);
        var heal = effects.Heals.Single();
        Assert.AreEqual(FordsTestLayout.EastOuter0, heal.TowerId);
        Assert.AreEqual(75, heal.Amount);
        Assert.AreEqual(7975, Tower(match.GetView(WestSlot), FordsTestLayout.EastOuter0).HitPoints);
    }

    [TestMethod]
    public void AdjustTowerDamage_HitFromTheTowersOwnSide_ChangesNothingAndLeavesTheRegenClockAlone()
    {
        var match = Create();
        match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 100));
        match.Tick(Snap(15f));

        var own = match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, true, UnitRole.Ranged, 200, MatchSide.East));
        var effects = match.Tick(Snap(10f));

        Assert.AreEqual(0, own);
        Assert.AreEqual(75, effects.Heals.Single().Amount, "an own-side blow must not restart the quiet timer");
        Assert.AreEqual(7975, Tower(match.GetView(WestSlot), FordsTestLayout.EastOuter0).HitPoints);
    }

    [TestMethod]
    public void AdjustTowerDamage_HitFromTheOtherSide_StillTakesHitPoints()
    {
        var match = Create();

        var effective = match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 100, MatchSide.West));

        Assert.AreEqual(100, effective);
        Assert.AreEqual(7900, Tower(match.GetView(WestSlot), FordsTestLayout.EastOuter0).HitPoints);
    }

    [TestMethod]
    public void GetView_ListsOffersWithAffordability()
    {
        var match = Create();

        var view = match.GetView(WestSlot);

        Assert.AreEqual(MatchSide.West, view.Side);
        Assert.AreEqual(1000, view.Gold);
        Assert.IsTrue(view.UnitOffers.Single(o => o.OfferId == "inf").CanAfford);
        Assert.IsFalse(view.UnitOffers.Single(o => o.OfferId == "elite").CanAfford);
        Assert.AreEqual(150, view.UnitOffers.Single(o => o.OfferId == "archer").Price);
        Assert.IsTrue(view.HeroOffers.Single(o => o.HeroId == "hero_a").CanAfford);
        Assert.AreEqual(2, view.HeroSlotsFree);
        CollectionAssert.AreEqual(new[] { 0, 1 }, view.OwnedLanes.ToArray());
    }

    [TestMethod]
    public void GetView_Towers_ShowLockStateAndKeeps()
    {
        var match = Create();

        var view = match.GetView(WestSlot);

        Assert.IsFalse(Tower(view, FordsTestLayout.EastOuter0).Locked);
        Assert.IsTrue(Tower(view, FordsTestLayout.EastInner0).Locked);
        Assert.IsTrue(Tower(view, FordsTestLayout.EastKeep).Locked);
        Assert.AreEqual(18000, Tower(view, FordsTestLayout.EastKeep).MaxHitPoints);
        Assert.AreEqual(2, view.Keeps.Count);
        Assert.AreEqual(4, view.Lanes[0].Towers.Count);
    }

    [TestMethod]
    public void GetView_LiveCounts_ComeFromTheLastSnapshot()
    {
        var match = Create();
        var lanes = new[] { Live(MatchSide.West, 0, 12), Live(MatchSide.East, 0, 7) };
        match.Tick(Snap(1f, lanes));

        var view = match.GetView(WestSlot);

        Assert.AreEqual(12, view.Lanes[0].OwnLive);
        Assert.AreEqual(7, view.Lanes[0].EnemyLive);
        Assert.AreEqual(0, view.Lanes[1].OwnLive);
    }

    [TestMethod]
    public void GetView_UnknownSlot_ReturnsAnEmptyOwnerlessView()
    {
        var match = Create();

        var view = match.GetView(99);

        Assert.AreEqual(0, view.Gold);
        Assert.AreEqual(0, view.OwnedLanes.Count);
        Assert.AreEqual(0, view.UnitOffers.Count);
    }

    [TestMethod]
    public void IsTower_KnownAndUnknownIds()
    {
        var match = Create();

        Assert.IsTrue(match.IsTower(FordsTestLayout.WestOuter0));
        Assert.IsTrue(match.IsTower(FordsTestLayout.EastKeep));
        Assert.IsFalse(match.IsTower("barricade_17"));
    }

    [TestMethod]
    public void AdjustTowerDamage_WithReason_ReturnsWhatTheOldOverloadReturns()
    {
        var a = Create();
        var b = Create();
        var hit = new TowerHit(FordsTestLayout.EastOuter0, true, UnitRole.Ranged, 100);

        var withReason = a.AdjustTowerDamage(hit, out var reason);

        Assert.AreEqual(b.AdjustTowerDamage(hit), withReason);
        Assert.AreEqual(TowerDamageReason.Applied, reason);
    }

    [TestMethod]
    public void AdjustTowerDamage_UnknownTower_ReturnsRawWithUnknownTowerReason()
    {
        var match = Create();

        var damage = match.AdjustTowerDamage(new TowerHit("barricade_17", true, UnitRole.Hero, 77), out var reason);
        match.AdjustTowerDamage(null!, out var nullReason);

        Assert.AreEqual(77, damage);
        Assert.AreEqual(TowerDamageReason.UnknownTower, reason);
        Assert.AreEqual(TowerDamageReason.RawZero, nullReason);
    }

    [TestMethod]
    public void Phase_FollowsTheClock()
    {
        var match = Create(Config(c => c with { CountdownSeconds = 10f }));
        Assert.AreEqual(MatchPhase.Countdown, match.Phase);

        match.Tick(Snap(11f));

        Assert.AreEqual(MatchPhase.Live, match.Phase);
    }
}
