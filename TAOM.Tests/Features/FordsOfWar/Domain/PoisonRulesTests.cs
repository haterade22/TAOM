using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Poisoned arrows: a dose lasts a while, ignores armour, and another hit refreshes it but never stacks it.</summary>
[TestClass]
public class PoisonRulesTests
{
    private static readonly AgentKey Victim = Key(1);
    private static readonly AgentKey Archer = Key(2);

    [TestMethod]
    public void Tick_PaysWholePoints_AndCarriesTheFraction()
    {
        var rules = new PoisonRules();
        rules.Apply(Victim, Archer, MatchSide.West, 3f, 8f);

        Assert.AreEqual(0, rules.Tick(0.25f).Count, "0.75 points so far");
        var tick = rules.Tick(0.25f).Single();

        Assert.AreEqual(Victim, tick.Victim);
        Assert.AreEqual(Archer, tick.Attacker);
        Assert.AreEqual(1, tick.Damage);
    }

    [TestMethod]
    public void Tick_StopsWhenTheDoseRunsOut_AfterItsFullDamage()
    {
        var rules = new PoisonRules();
        rules.Apply(Victim, Archer, MatchSide.West, 3f, 8f);

        var total = 0;
        for (var i = 0; i < 20; i++)
            total += rules.Tick(1f).Sum(t => t.Damage);

        Assert.AreEqual(24, total);
        Assert.AreEqual(0, rules.Tick(1f).Count);
    }

    [TestMethod]
    public void Apply_AgainWhilePoisoned_RefreshesTheTime_AndDoesNotStack()
    {
        var rules = new PoisonRules();
        rules.Apply(Victim, Archer, MatchSide.West, 3f, 8f);
        rules.Tick(4f);

        rules.Apply(Victim, Key(3), MatchSide.West, 3f, 8f);

        Assert.AreEqual(3, rules.Tick(1f).Single().Damage, "one dose, not two");
        var total = 0;
        for (var i = 0; i < 20; i++)
            total += rules.Tick(1f).Sum(t => t.Damage);
        Assert.AreEqual(21, total, "the other 7 seconds of the refreshed dose");
    }

    [TestMethod]
    public void Apply_AWeakerDose_DoesNotReplaceAStrongerOne_ButAStrongerDoes()
    {
        var rules = new PoisonRules();
        rules.Apply(Victim, Archer, MatchSide.West, 3f, 8f);
        rules.Apply(Victim, Key(3), MatchSide.East, 2f, 6f);

        Assert.AreEqual(3, rules.Tick(1f).Single().Damage);

        rules.Apply(Victim, Key(3), MatchSide.East, 5f, 6f);

        Assert.AreEqual(5, rules.Tick(1f).Single().Damage);
    }

    [TestMethod]
    public void Forget_EndsTheDose()
    {
        var rules = new PoisonRules();
        rules.Apply(Victim, Archer, MatchSide.West, 3f, 8f);

        rules.Forget(Victim);

        Assert.AreEqual(0, rules.Tick(1f).Count);
    }

    [TestMethod]
    public void Totals_CountDosesAndDamagePerSide()
    {
        var rules = new PoisonRules();
        rules.Apply(Victim, Archer, MatchSide.West, 3f, 8f);
        rules.Tick(2f);

        Assert.AreEqual((1, 6L), rules.Totals(MatchSide.West));
        Assert.AreEqual((0, 0L), rules.Totals(MatchSide.East));
    }

    private static FordsMatch MatchWith(FactionFeats west)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { Feats = west };
        var match = new FordsMatch(Config(), Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot));
        match.RegisterUnit(3, Unit(11, MatchSide.West, WestSlot));
        return match;
    }

    private static readonly FactionFeats Poisoned = new() { PoisonPerSecond = 3f, PoisonSeconds = 8f };

    [TestMethod]
    public void OnArrowHit_AFactionWithPoison_PoisonsTheEnemy()
    {
        var match = MatchWith(Poisoned);

        match.OnArrowHit(Key(20), Key(10));

        var tick = match.Tick(Snap(1f)).PoisonTicks.Single();
        Assert.AreEqual(Key(20), tick.Victim);
        Assert.AreEqual(Key(10), tick.Attacker);
        Assert.AreEqual(3, tick.Damage);
    }

    [TestMethod]
    public void OnArrowHit_NoPoisonFeat_OrASideOfItsOwn_OrAnUnknownAgent_DoesNothing()
    {
        var plain = MatchWith(new FactionFeats());
        plain.OnArrowHit(Key(20), Key(10));
        Assert.AreEqual(0, plain.Tick(Snap(1f)).PoisonTicks.Count, "no feat");

        var poisoned = MatchWith(Poisoned);
        poisoned.OnArrowHit(Key(11), Key(10));
        Assert.AreEqual(0, poisoned.Tick(Snap(1f)).PoisonTicks.Count, "its own side");

        poisoned.OnArrowHit(Key(20), Key(99));
        poisoned.OnArrowHit(Key(99), Key(10));
        Assert.AreEqual(0, poisoned.Tick(Snap(1f)).PoisonTicks.Count, "a gunner or a mount is not a registered unit");
    }

    [TestMethod]
    public void OnUnitKilled_EndsThePoison()
    {
        var match = MatchWith(Poisoned);
        match.OnArrowHit(Key(20), Key(10));

        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(0, match.Tick(Snap(1f)).PoisonTicks.Count);
    }
}
