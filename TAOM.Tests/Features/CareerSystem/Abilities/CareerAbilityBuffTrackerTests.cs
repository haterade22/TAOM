using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

// Issue #377 — the buff entry must retire when its last contribution's restore fires.
// Before the fix, expiry subtracted the deltas but left a zeroed entry in the dictionary
// forever (GetBuff stayed non-null from first activation to mission end), so "a buff
// exists" could not be used to mean "the ability is active".
[TestClass]
public class CareerAbilityBuffTrackerTests
{
    private const string HeroId = "hero_1";
    private const int AllyIndex = 7;

    [TestInitialize]
    public void Setup() => CareerAbilityBuffTracker.ClearAll();

    [TestCleanup]
    public void Teardown() => CareerAbilityBuffTracker.ClearAll();

    // ── hero entry lifecycle ─────────────────────────────────────────────────

    [TestMethod]
    public void AddContribution_NewHero_CreatesEntryWithDeltas()
    {
        CareerAbilityBuffTracker.AddContribution(HeroId, new ActiveBuffs { DamageBonus = 0.10f });

        var buff = CareerAbilityBuffTracker.GetBuff(HeroId);
        Assert.IsNotNull(buff);
        Assert.AreEqual(0.10f, buff.DamageBonus, 0.0001f);
    }

    [TestMethod]
    public void RemoveContribution_LastContribution_RemovesEntry()
    {
        var deltas = new ActiveBuffs { DamageBonus = 0.10f };
        CareerAbilityBuffTracker.AddContribution(HeroId, deltas);

        CareerAbilityBuffTracker.RemoveContribution(HeroId, deltas);

        Assert.IsNull(CareerAbilityBuffTracker.GetBuff(HeroId));
    }

    [TestMethod]
    public void RemoveContribution_OneOfTwoOverlapping_KeepsEntryWithRemainingDeltas()
    {
        var first = new ActiveBuffs { DamageBonus = 0.10f };
        var second = new ActiveBuffs { SpeedMultiplier = 0.15f };
        CareerAbilityBuffTracker.AddContribution(HeroId, first);
        CareerAbilityBuffTracker.AddContribution(HeroId, second);

        CareerAbilityBuffTracker.RemoveContribution(HeroId, first);

        var buff = CareerAbilityBuffTracker.GetBuff(HeroId);
        Assert.IsNotNull(buff, "second contribution still active — entry must survive");
        Assert.AreEqual(0f, buff.DamageBonus, 0.0001f);
        Assert.AreEqual(0.15f, buff.SpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    public void RemoveContribution_BothOverlapping_RemovesEntry()
    {
        var first = new ActiveBuffs { DamageBonus = 0.10f };
        var second = new ActiveBuffs { SpeedMultiplier = 0.15f };
        CareerAbilityBuffTracker.AddContribution(HeroId, first);
        CareerAbilityBuffTracker.AddContribution(HeroId, second);

        CareerAbilityBuffTracker.RemoveContribution(HeroId, first);
        CareerAbilityBuffTracker.RemoveContribution(HeroId, second);

        Assert.IsNull(CareerAbilityBuffTracker.GetBuff(HeroId));
    }

    [TestMethod]
    public void RemoveContribution_EntryAlreadyCleared_NoOps()
    {
        // Main-agent death clears the entry while restores are still pending; the late
        // restore must not throw or resurrect the entry.
        var deltas = new ActiveBuffs { DamageBonus = 0.10f };
        CareerAbilityBuffTracker.AddContribution(HeroId, deltas);
        CareerAbilityBuffTracker.ClearBuff(HeroId);

        CareerAbilityBuffTracker.RemoveContribution(HeroId, deltas);

        Assert.IsNull(CareerAbilityBuffTracker.GetBuff(HeroId));
    }

    // ── ally entry lifecycle (same shape, keyed by agent index) ──────────────

    [TestMethod]
    public void AddAllyContribution_NewAlly_CreatesEntryWithDeltas()
    {
        CareerAbilityBuffTracker.AddAllyContribution(AllyIndex, new ActiveBuffs { DrawSpeedBonus = 0.2f });

        var buff = CareerAbilityBuffTracker.GetAllyBuff(AllyIndex);
        Assert.IsNotNull(buff);
        Assert.AreEqual(0.2f, buff.DrawSpeedBonus, 0.0001f);
    }

    [TestMethod]
    public void RemoveAllyContribution_LastContribution_RemovesEntry()
    {
        var deltas = new ActiveBuffs { DrawSpeedBonus = 0.2f };
        CareerAbilityBuffTracker.AddAllyContribution(AllyIndex, deltas);

        CareerAbilityBuffTracker.RemoveAllyContribution(AllyIndex, deltas);

        Assert.IsNull(CareerAbilityBuffTracker.GetAllyBuff(AllyIndex));
    }

    [TestMethod]
    public void RemoveAllyContribution_OneOfTwoOverlapping_KeepsEntry()
    {
        var first = new ActiveBuffs { DamageBonus = 0.05f };
        var second = new ActiveBuffs { MountSpeedBonus = 0.1f };
        CareerAbilityBuffTracker.AddAllyContribution(AllyIndex, first);
        CareerAbilityBuffTracker.AddAllyContribution(AllyIndex, second);

        CareerAbilityBuffTracker.RemoveAllyContribution(AllyIndex, first);

        var buff = CareerAbilityBuffTracker.GetAllyBuff(AllyIndex);
        Assert.IsNotNull(buff);
        Assert.AreEqual(0.1f, buff.MountSpeedBonus, 0.0001f);
    }

    [TestMethod]
    public void RemoveAllyContribution_AgentAlreadyDeleted_NoOps()
    {
        var deltas = new ActiveBuffs { DamageBonus = 0.05f };
        CareerAbilityBuffTracker.AddAllyContribution(AllyIndex, deltas);
        CareerAbilityBuffTracker.ClearAllyBuff(AllyIndex);

        CareerAbilityBuffTracker.RemoveAllyContribution(AllyIndex, deltas);

        Assert.IsNull(CareerAbilityBuffTracker.GetAllyBuff(AllyIndex));
    }

    // ── deep-review 2026-08-05 — hero-death ally refresh needs the buffed set BEFORE the
    // clear wipes it (the cleared dictionary can't say which agents held baked-in stats).

    [TestMethod]
    public void GetBuffedAllyIndices_ReturnsLiveEntries()
    {
        CareerAbilityBuffTracker.AddAllyContribution(3, new ActiveBuffs { DamageBonus = 0.1f });
        CareerAbilityBuffTracker.AddAllyContribution(9, new ActiveBuffs { SpeedMultiplier = 0.2f });

        var indices = CareerAbilityBuffTracker.GetBuffedAllyIndices();

        CollectionAssert.AreEquivalent(new[] { 3, 9 }, (System.Collections.ICollection)indices);
    }

    [TestMethod]
    public void GetBuffedAllyIndices_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(0, CareerAbilityBuffTracker.GetBuffedAllyIndices().Count);
    }

    [TestMethod]
    public void GetBuffedAllyIndices_SnapshotSurvivesClear()
    {
        CareerAbilityBuffTracker.AddAllyContribution(3, new ActiveBuffs { DamageBonus = 0.1f });

        var indices = CareerAbilityBuffTracker.GetBuffedAllyIndices();
        CareerAbilityBuffTracker.ClearAllAllyBuffs();

        Assert.AreEqual(1, indices.Count, "snapshot must not alias the live dictionary");
    }
}
