using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Whole-match stories: towers falling and unlocking, kills paying out, heroes dying and returning, the match ending.</summary>
[TestClass]
public class FordsMatchScenarioTests
{
    private static TowerHit Melee(string towerId, int damage = 100) => new(towerId, false, UnitRole.Infantry, damage);

    [TestMethod]
    public void OuterTowerFalls_TheNextOneUnlocksAndTheBountyIsPaid()
    {
        var match = Create();
        Assert.AreEqual(0, match.AdjustTowerDamage(Melee(FordsTestLayout.EastInner0)));
        Assert.AreEqual(100, match.AdjustTowerDamage(Melee(FordsTestLayout.EastOuter0)));

        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, Key(1));

        Assert.AreEqual(100, match.AdjustTowerDamage(Melee(FordsTestLayout.EastInner0)));
        Assert.AreEqual(1300, match.GetView(WestSlot).Gold);
        var fell = match.Tick(Snap()).Events.Single(e => e.Kind == MatchEventKind.TowerDestroyed);
        Assert.AreEqual(MatchSide.East, fell.Side);
        Assert.AreEqual(FordsTestLayout.EastOuter0, fell.Subject);
        Assert.AreEqual(300, fell.Amount);
    }

    [TestMethod]
    public void OnTowerDestroyed_AfterALethalHit_StillPaysOnce()
    {
        var match = Create();
        match.AdjustTowerDamage(Melee(FordsTestLayout.EastOuter0, 8000));

        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);
        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);

        Assert.AreEqual(1300, match.GetView(WestSlot).Gold);
        Assert.IsTrue(Tower(match.GetView(WestSlot), FordsTestLayout.EastOuter0).Destroyed);
    }

    [TestMethod]
    public void OnTowerDestroyed_SplitsTheBountyAcrossTheDestroyingSidesSlots()
    {
        var slots = new[]
        {
            new CommanderSlot(1, MatchSide.West, true, null, new[] { 0 }),
            new CommanderSlot(3, MatchSide.West, true, null, new[] { 1 }),
            new CommanderSlot(2, MatchSide.East, false, "slow", new[] { 0, 1 }),
        };
        var match = Create(slots: slots);

        match.OnTowerDestroyed(FordsTestLayout.EastOuter1, null);

        Assert.AreEqual(1150, match.GetView(1).Gold);
        Assert.AreEqual(1150, match.GetView(3).Gold);
        Assert.AreEqual(1000, match.GetView(2).Gold);
    }

    [TestMethod]
    public void OnTowerDestroyed_UnknownTower_IsIgnored()
    {
        var match = Create();

        match.OnTowerDestroyed("barricade_17", Key(1));

        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
        Assert.AreEqual(0, match.Tick(Snap()).Events.Count);
    }

    [TestMethod]
    public void AdjustTowerDamage_UnknownTower_ReturnsTheInputUnchanged()
    {
        var match = Create();

        Assert.AreEqual(77, match.AdjustTowerDamage(new TowerHit("barricade_17", true, UnitRole.Hero, 77)));
        Assert.AreEqual(5, match.AdjustTowerDamage(new TowerHit("", false, UnitRole.Infantry, 5)));
    }

    [TestMethod]
    public void AdjustTowerDamage_KnownTower_AppliesTheDamageTable()
    {
        var match = Create();

        Assert.AreEqual(15, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, true, UnitRole.Ranged, 100)));
        Assert.AreEqual(400, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Hero, 100)));
        Assert.AreEqual(300, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Siege, 100)));
        Assert.AreEqual(8000 - 15 - 400 - 300, Tower(match.GetView(WestSlot), FordsTestLayout.EastOuter0).HitPoints);
    }

    [TestMethod]
    public void Keep_UnlocksAfterOneLaneIsCleared()
    {
        var match = Create();
        Assert.AreEqual(0, match.AdjustTowerDamage(Melee(FordsTestLayout.EastKeep, 50)));

        match.OnTowerDestroyed(FordsTestLayout.EastOuter0, null);
        Assert.AreEqual(0, match.AdjustTowerDamage(Melee(FordsTestLayout.EastKeep, 50)));
        match.OnTowerDestroyed(FordsTestLayout.EastInner0, null);

        Assert.AreEqual(50, match.AdjustTowerDamage(Melee(FordsTestLayout.EastKeep, 50)));
        Assert.IsFalse(Tower(match.GetView(WestSlot), FordsTestLayout.EastKeep).Locked);
        Assert.IsTrue(Tower(match.GetView(WestSlot), FordsTestLayout.WestKeep).Locked);
    }

    [TestMethod]
    public void KeepDestroyed_EndsTheMatchAndStopsSpawning()
    {
        var match = Create(Config(WithFreeWaves));
        Assert.IsTrue(match.Tick(Snap(1f)).Spawns.Count > 0);
        match.OnTowerDestroyed(FordsTestLayout.EastKeep, Key(1));

        var ending = match.Tick(Snap(60f));
        var after = match.Tick(Snap(60f));

        Assert.AreEqual(MatchSide.West, ending.Winner);
        Assert.IsTrue(ending.Events.Any(e => e.Kind == MatchEventKind.KeepDestroyed && e.Side == MatchSide.East));
        Assert.AreEqual(0, ending.Spawns.Count);
        Assert.AreEqual(0, after.Spawns.Count);
        Assert.AreEqual(MatchSide.West, after.Winner);
        var view = match.GetView(WestSlot);
        Assert.AreEqual(MatchPhase.Ended, view.Phase);
        Assert.AreEqual(MatchSide.West, view.Winner);
        Assert.AreEqual(CommandResult.NotLive, match.TryBuyPack(WestSlot, 0, "inf"));
    }

    [TestMethod]
    public void OnUnitKilled_PaysTheKillersOwnerTheKillBounty()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 200));

        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1070, match.GetView(WestSlot).Gold);
        Assert.AreEqual(1000, match.GetView(EastSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_CheapVictim_PaysTheMinimumBounty()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 1));

        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1002, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_UnknownKeysOrNoKiller_PayNothingAndDoNotThrow()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 200));

        match.OnUnitKilled(Key(999), Key(10));
        match.OnUnitKilled(Key(20), Key(888));
        match.OnUnitKilled(Key(20), null);
        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_SameSideKill_PaysNothing()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(11, MatchSide.West, WestSlot, value: 200));

        match.OnUnitKilled(Key(11), Key(10));

        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_KilledTwice_PaysOnce()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 200));

        match.OnUnitKilled(Key(20), Key(10));
        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1070, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void BuyHero_QueuesAHeroSpawnAndChargesThePrice()
    {
        var match = Create();

        var result = match.TryBuyHero(WestSlot, 1, "hero_a");
        var effects = match.Tick(Snap());

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(300, match.GetView(WestSlot).Gold);
        var spawn = effects.Spawns.Single();
        Assert.IsTrue(spawn.IsHero);
        Assert.AreEqual("hero_a", spawn.TroopId);
        Assert.AreEqual(1, spawn.Count);
        Assert.AreEqual(1, spawn.Lane);
        Assert.AreEqual(WestSlot, spawn.OwnerSlot);
        Assert.AreEqual(UnitRole.Hero, spawn.Role);
        Assert.IsTrue(effects.Events.Any(e => e.Kind == MatchEventKind.HeroFielded && e.Subject == "hero_a" && e.Side == MatchSide.West));
        Assert.AreEqual(1, match.GetView(WestSlot).HeroSlotsFree);
    }

    [TestMethod]
    public void BuyHero_Refusals_ReturnTheMatchingResult()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 5000 } }));

        Assert.AreEqual(CommandResult.UnknownOffer, match.TryBuyHero(WestSlot, 0, "hero_zzz"));
        Assert.AreEqual(CommandResult.UnknownLane, match.TryBuyHero(WestSlot, 5, "hero_a"));
        Assert.AreEqual(CommandResult.Ok, match.TryBuyHero(WestSlot, 0, "hero_a"));
        Assert.AreEqual(CommandResult.HeroAlreadyFielded, match.TryBuyHero(WestSlot, 0, "hero_a"));
        Assert.AreEqual(CommandResult.Ok, match.TryBuyHero(WestSlot, 0, "hero_b"));
        Assert.AreEqual(3900, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void BuyHero_AtTheAliveLimit_IsRefused()
    {
        var config = Config(c => c with
        {
            Economy = c.Economy with { StartingGold = 5000 },
            Heroes = c.Heroes with { MaxAlivePerSide = 1 },
        });
        var match = Create(config);
        match.TryBuyHero(WestSlot, 0, "hero_a");

        Assert.AreEqual(CommandResult.HeroLimit, match.TryBuyHero(WestSlot, 0, "hero_b"));
        Assert.AreEqual(4300, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void BuyHero_NotEnoughGold_IsRefused()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 500 } }));

        Assert.AreEqual(CommandResult.NotEnoughGold, match.TryBuyHero(WestSlot, 0, "hero_a"));
        Assert.AreEqual(500, match.GetView(WestSlot).Gold);
        Assert.AreEqual(2, match.GetView(WestSlot).HeroSlotsFree);
    }

    [TestMethod]
    public void HeroDies_PaysTheHeroBountyAndRespawnsAfterTheComputedTime()
    {
        var match = Create(slots: Slots("slow"));
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.Tick(Snap(60f));
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot));

        match.OnUnitKilled(Key(10), Key(20));

        Assert.AreEqual(1250, match.GetView(EastSlot).Gold);
        var hero = match.GetView(WestSlot).Heroes.Single();
        Assert.AreEqual(HeroState.Dead, hero.State);
        Assert.AreEqual(66f, hero.RespawnLeft, 1e-3f);
        var died = match.Tick(Snap(1f)).Events.Single(e => e.Kind == MatchEventKind.HeroDied);
        Assert.AreEqual(MatchSide.West, died.Side);
        Assert.AreEqual("hero_a", died.Subject);

        var early = match.Tick(Snap(64f));
        Assert.AreEqual(0, early.Spawns.Count);

        var respawn = match.Tick(Snap(1f));
        var spawn = respawn.Spawns.Single();
        Assert.IsTrue(spawn.IsHero);
        Assert.AreEqual("hero_a", spawn.TroopId);
        Assert.AreEqual(WestSlot, spawn.OwnerSlot);
        Assert.IsTrue(respawn.Events.Any(e => e.Kind == MatchEventKind.HeroRespawned && e.Subject == "hero_a"));
        Assert.AreEqual(HeroState.Alive, match.GetView(WestSlot).Heroes.Single().State);
    }

    [TestMethod]
    public void Buyback_PaysTheScaledPriceAndRespawnsAtOnce()
    {
        var match = Create(slots: Slots("slow"));
        match.TryBuyHero(WestSlot, 0, "hero_b");
        match.Tick(Snap(60f));
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: "hero_b"));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot));
        match.OnUnitKilled(Key(10), Key(20));

        Assert.AreEqual(220, match.GetView(WestSlot).Heroes.Single().BuybackPrice);
        var result = match.TryBuyback(WestSlot, "hero_b");
        var effects = match.Tick(Snap(0.5f));

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(600 - 220, match.GetView(WestSlot).Gold);
        Assert.IsTrue(effects.Spawns.Single().IsHero);
        Assert.AreEqual("hero_b", effects.Spawns.Single().TroopId);
    }

    [TestMethod]
    public void Buyback_Refusals_ReturnTheMatchingResult()
    {
        var match = Create();
        match.TryBuyHero(WestSlot, 0, "hero_a");

        Assert.AreEqual(CommandResult.HeroNotDead, match.TryBuyback(WestSlot, "hero_a"));
        Assert.AreEqual(CommandResult.HeroNotDead, match.TryBuyback(WestSlot, "hero_b"));

        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(10), null);

        Assert.AreEqual(CommandResult.NotEnoughGold, match.TryBuyback(WestSlot, "hero_a"));
        Assert.AreEqual(300, match.GetView(WestSlot).Gold);
        Assert.AreEqual(CommandResult.HeroNotDead, match.TryBuyback(EastSlot, "hero_a"));
    }

    [TestMethod]
    public void Ai_ThreatenedLane_BuysTheCheapestSpearOrInfantryPackThere()
    {
        var match = Create();
        var threat = new[] { Threat(FordsTestLayout.EastOuter1, 5) };

        var deciding = match.Tick(Snap(5f, targets: threat));
        var spawning = match.Tick(Snap(1f));

        Assert.IsTrue(spawning.Spawns.Any(s => s.Side == MatchSide.East && s.Lane == 1 && s.TroopId == "footman"
            && s.OwnerSlot == EastSlot && s.Count > 0));
        Assert.IsTrue(deciding.Events.Any(e => e.Kind == MatchEventKind.PackBought && e.Side == MatchSide.East && e.Subject == "inf"));
        Assert.IsTrue(match.GetView(EastSlot).Gold < 1000);
    }

    [TestMethod]
    public void Ai_UnthreatenedLane_HoldsUntilThresholdThenPushes()
    {
        var match = Create();
        match.Tick(Snap(5f));
        Assert.AreEqual(LaneOrder.Hold, match.GetView(EastSlot).Lanes[0].Order);

        var strong = new[] { Live(MatchSide.East, 0, 45) };
        match.Tick(Snap(25f, strong));

        Assert.AreEqual(LaneOrder.Push, match.GetView(EastSlot).Lanes[0].Order);
    }

    [TestMethod]
    public void Ai_Rich_FieldsTheMostExpensiveAffordableHero()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 3000 } }));

        var deciding = match.Tick(Snap(5f));
        var spawning = match.Tick(Snap(1f));

        Assert.IsTrue(deciding.Events.Any(e => e.Kind == MatchEventKind.HeroFielded && e.Side == MatchSide.East && e.Subject == "hero_a"));
        Assert.IsTrue(spawning.Spawns.Any(s => s.IsHero && s.TroopId == "hero_a" && s.OwnerSlot == EastSlot));
    }

    [TestMethod]
    public void Ai_ModestGold_FieldsOnlyTheHeroItCanAffordWithItsReserve()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 900 } }));

        var deciding = match.Tick(Snap(5f));

        var fielded = deciding.Events.Where(e => e.Kind == MatchEventKind.HeroFielded).ToList();
        Assert.AreEqual(1, fielded.Count);
        Assert.AreEqual("hero_b", fielded[0].Subject);
    }

    [TestMethod]
    public void Ai_PoorForAnyHero_FieldsNone()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 700 } }));

        var deciding = match.Tick(Snap(5f));

        Assert.IsFalse(deciding.Events.Any(e => e.Kind == MatchEventKind.HeroFielded));
    }

    [TestMethod]
    public void Ai_SameSeed_PlaysTheSameMatch()
    {
        var first = Play(seed: 11);
        var second = Play(seed: 11);

        Assert.AreEqual(first, second);
        Assert.IsTrue(first.Length > 200, "the scripted match did nothing worth comparing");
    }

    [TestMethod]
    public void Ai_NeverBuysForAHumanSlot()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 3000 } }), Slots(null));

        for (var i = 0; i < 30; i++)
            match.Tick(Snap(5f));

        Assert.AreEqual(3000, match.GetView(EastSlot).Gold);
        Assert.AreEqual(3000, match.GetView(WestSlot).Gold);
    }

    private static string Play(int seed)
    {
        var config = Config(c => WithFreeWaves(c with { Economy = c.Economy with { IncomePerMinute = 120 } }));
        var slots = new[]
        {
            new CommanderSlot(WestSlot, MatchSide.West, false, "easy", new[] { 0, 1 }),
            new CommanderSlot(EastSlot, MatchSide.East, false, "easy", new[] { 0, 1 }),
        };
        var match = Create(config, slots, seed);
        var log = new System.Text.StringBuilder();
        for (var i = 0; i < 150; i++)
        {
            var lanes = new[]
            {
                Live(MatchSide.West, 0, (i * 7) % 60),
                Live(MatchSide.East, 1, (i * 5) % 50),
            };
            var targets = i % 9 == 0 ? new[] { Threat(FordsTestLayout.WestOuter1, 4), Threat(FordsTestLayout.EastOuter0, 6) } : new TowerTargets[0];
            var effects = match.Tick(Snap(2f, lanes, targets));
            foreach (var s in effects.Spawns)
                log.Append($"S{s.Side}{s.Lane}{s.TroopId}x{s.Count}o{s.OwnerSlot};");
            foreach (var e in effects.Events)
                log.Append($"E{e.Kind}{e.Side}{e.Subject}{e.Amount};");
            foreach (var c in effects.Commands)
                log.Append($"C{c.Side}{c.Lane}{c.Command};");
        }

        log.Append($"G{match.GetView(WestSlot).Gold}/{match.GetView(EastSlot).Gold}");
        return log.ToString();
    }
}
