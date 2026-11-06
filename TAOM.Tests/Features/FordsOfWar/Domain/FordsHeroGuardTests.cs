using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// Mike, 2026-11-05: a hero is summoned with elite troops of its culture that guard it. Every way a hero takes the field (bought, free arrival,
/// respawn, buyback) queues its faction's guard into the hero's lane for the hero's owner, at no extra price. The guard waits in the wave
/// scheduler like a pack, so it never takes the lane or the side past its caps (Codex, 2026-11-05).
/// </summary>
[TestClass]
public class FordsHeroGuardTests
{
    private static readonly HeroGuard Guard = new("elite_guard", 4, UnitRole.Infantry);

    private static FordsMatch Match(float arrival = 0f, int laneCap = 90, IReadOnlyDictionary<MatchSide, FactionCatalog>? factions = null)
    {
        factions ??= Factions().ToDictionary(p => p.Key, p => p.Value with { HeroGuard = Guard });
        var config = Config(c => c with
        {
            Heroes = c.Heroes with { FreeArrivalSeconds = arrival },
            Waves = c.Waves with { MaxLivePerLanePerSide = laneCap },
        });
        return new FordsMatch(config, Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static List<SpawnRequest> Guards(MatchEffects effects, MatchSide side) =>
        effects.Spawns.Where(r => r.IsGuard && r.Side == side).ToList();

    [TestMethod]
    public void ABoughtHero_ComesWithItsGuard_InItsLane_ForItsOwner()
    {
        var match = Match();
        Assert.AreEqual(CommandResult.Ok, match.TryBuyHero(WestSlot, 1, HeroA.HeroId));

        var effects = match.Tick(Snap(1f));

        var hero = effects.Spawns.Single(r => r.IsHero);
        var guard = Guards(effects, MatchSide.West).Single();
        Assert.AreEqual(Guard.TroopId, guard.TroopId);
        Assert.AreEqual(4, guard.Count);
        Assert.AreEqual(hero.Lane, guard.Lane);
        Assert.AreEqual(WestSlot, guard.OwnerSlot);
        Assert.AreEqual(UnitRole.Infantry, guard.Role);
        Assert.IsFalse(guard.IsHero);
        Assert.AreEqual(1000 - HeroA.Price, match.GetView(WestSlot).Gold, "the guard costs nothing on top of the hero");
    }

    [TestMethod]
    public void AFreeHero_ComesWithItsGuard()
    {
        var match = Match(arrival: 300f);

        var arrival = match.Tick(Snap(301f));
        var next = match.Tick(Snap(1f));

        Assert.AreEqual(1, Guards(arrival, MatchSide.West).Count + Guards(next, MatchSide.West).Count);
        Assert.AreEqual(1, Guards(arrival, MatchSide.East).Count + Guards(next, MatchSide.East).Count);
    }

    [TestMethod]
    public void ARespawnedHero_ComesWithANewGuard()
    {
        var match = Match();
        match.TryBuyHero(WestSlot, 0, HeroA.HeroId);
        match.Tick(Snap(60f));
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: HeroA.HeroId));
        match.OnUnitKilled(Key(10), null);
        match.Tick(Snap(1f));

        var respawn = match.Tick(Snap(120f));
        var next = match.Tick(Snap(1f));

        Assert.IsTrue(respawn.Spawns.Any(r => r.IsHero && r.TroopId == HeroA.HeroId));
        Assert.AreEqual(1, Guards(respawn, MatchSide.West).Count + Guards(next, MatchSide.West).Count);
    }

    [TestMethod]
    public void ABoughtBackHero_ComesWithANewGuard()
    {
        var match = Match();
        match.TryBuyHero(WestSlot, 0, HeroB.HeroId);
        match.Tick(Snap(1f));
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: HeroB.HeroId));
        match.OnUnitKilled(Key(10), null);
        match.Tick(Snap(1f));
        Assert.AreEqual(CommandResult.Ok, match.TryBuyback(WestSlot, HeroB.HeroId));

        var back = match.Tick(Snap(1f));
        var next = match.Tick(Snap(1f));

        Assert.IsTrue(back.Spawns.Any(r => r.IsHero && r.TroopId == HeroB.HeroId));
        Assert.AreEqual(1, Guards(back, MatchSide.West).Count + Guards(next, MatchSide.West).Count);
    }

    [TestMethod]
    public void AGuard_NeverTakesTheLanePastItsCap_AndComesWhenRoomOpens()
    {
        var match = Match(laneCap: 2);
        match.TryBuyHero(WestSlot, 0, HeroA.HeroId);

        var first = match.Tick(Snap(1f));

        Assert.AreEqual(2, Guards(first, MatchSide.West).Sum(r => r.Count), "only the room the lane has");
        var sent = 0;
        foreach (var request in Guards(first, MatchSide.West))
        {
            for (var i = 0; i < request.Count; i++)
            {
                match.RegisterUnit(request.RequestId, Unit(100 + sent, MatchSide.West, WestSlot));
                match.OnUnitKilled(Key(100 + sent), null);
                sent++;
            }
        }

        var later = match.Tick(Snap(1f));
        Assert.AreEqual(2, Guards(later, MatchSide.West).Sum(r => r.Count), "the rest once room opens");
    }

    [TestMethod]
    public void AFactionWithNoGuard_FieldsTheHeroAlone()
    {
        var match = Match(factions: Factions());
        match.TryBuyHero(WestSlot, 0, HeroA.HeroId);

        var effects = match.Tick(Snap(1f));

        Assert.AreEqual(0, effects.Spawns.Count(r => r.IsGuard));
    }

    [TestMethod]
    public void AGuardThatFailsToSpawn_RefundsNothing_AndLeavesTheHeroFielded()
    {
        var match = Match();
        match.TryBuyHero(WestSlot, 0, HeroA.HeroId);
        var guard = Guards(match.Tick(Snap(1f)), MatchSide.West).Single();

        match.OnSpawnFailed(guard.RequestId, guard.Count);

        Assert.AreEqual(1000 - HeroA.Price, match.GetView(WestSlot).Gold);
        Assert.AreEqual(HeroState.Alive, match.GetView(WestSlot).Heroes.Single(h => h.HeroId == HeroA.HeroId).State);
    }
}
