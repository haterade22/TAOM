using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAuras;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.HeroAuras;

[TestClass]
public class HeroAuraLedgerTests
{
    private sealed class Soldier { }

    private readonly HeroAuraLedger<Soldier> _ledger = new HeroAuraLedger<Soldier>();
    private readonly List<Soldier> _expired = new List<Soldier>();

    [TestMethod]
    public void Stamp_NewSoldier_HoldsTheEffects()
    {
        var soldier = new Soldier();
        var effects = new RaceAbilityEffects { DamageReductionPercent = 10f };

        _ledger.Stamp(soldier, "dain@1", effects, until: 4f);

        Assert.AreSame(effects, _ledger.EffectsOf(soldier));
    }

    [TestMethod]
    public void Stamp_OtherAuras_ReplacesTheEffects()
    {
        var soldier = new Soldier();
        var next = new RaceAbilityEffects();
        _ledger.Stamp(soldier, "dain@1", new RaceAbilityEffects(), until: 4f);

        _ledger.Stamp(soldier, "dain+elrond@1", next, until: 5f);

        Assert.AreSame(next, _ledger.EffectsOf(soldier));
    }

    [TestMethod]
    public void TryExtend_SameAuraKey_MovesTheEndAndReturnsTheEffects()
    {
        var soldier = new Soldier();
        var effects = new RaceAbilityEffects();
        _ledger.Stamp(soldier, "dain+elrond@1", effects, until: 4f);

        Assert.AreSame(effects, _ledger.TryExtend(soldier, "dain+elrond@1", until: 6f));
        _ledger.Expire(5f, _expired);
        Assert.AreEqual(0, _expired.Count, "the extend moved the end to 6 s");
        Assert.AreSame(effects, _ledger.EffectsOf(soldier));
    }

    [TestMethod]
    public void TryExtend_OtherAuraKeyOrNewSoldier_ReturnsNull()
    {
        var soldier = new Soldier();
        _ledger.Stamp(soldier, "dain@1", new RaceAbilityEffects(), until: 4f);

        Assert.IsNull(_ledger.TryExtend(soldier, "dain@2", until: 6f));
        Assert.IsNull(_ledger.TryExtend(new Soldier(), "dain@1", until: 6f));
    }

    [TestMethod]
    public void Expire_PastTheEnd_RemovesAndListsTheSoldier()
    {
        var soldier = new Soldier();
        _ledger.Stamp(soldier, "dain", new RaceAbilityEffects(), until: 4f);

        _ledger.Expire(4f, _expired);

        CollectionAssert.AreEqual(new[] { soldier }, _expired);
        Assert.IsNull(_ledger.EffectsOf(soldier));
    }

    [TestMethod]
    public void Remove_DropsTheSoldierWithoutListingHim()
    {
        var soldier = new Soldier();
        _ledger.Stamp(soldier, "dain", new RaceAbilityEffects(), until: 4f);

        _ledger.Remove(soldier);
        _ledger.Expire(10f, _expired);

        Assert.IsNull(_ledger.EffectsOf(soldier));
        Assert.AreEqual(0, _expired.Count);
    }

    [TestMethod]
    public void Reach_CountsEachSoldierOncePerAura()
    {
        var a = new Soldier();
        var b = new Soldier();
        _ledger.RecordReach("dain", a);
        _ledger.RecordReach("dain", a);
        _ledger.RecordReach("dain", b);
        _ledger.RecordReach("elrond", a);

        CollectionAssert.AreEqual(new[] { "dain: 2 soldier(s)", "elrond: 1 soldier(s)" }, _ledger.ReachLines());
        Assert.AreEqual(2, _ledger.ReachTotal, "a soldier under two auras counts once");
    }

    [TestMethod]
    public void Clear_ForgetsSoldiersAndReach()
    {
        var soldier = new Soldier();
        _ledger.Stamp(soldier, "dain", new RaceAbilityEffects(), until: 4f);
        _ledger.RecordReach("dain", soldier);

        _ledger.Clear();

        Assert.IsNull(_ledger.EffectsOf(soldier));
        Assert.AreEqual(0, _ledger.ReachLines().Count);
    }
}
