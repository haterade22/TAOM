using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

// Regression tests for Codex P2 finding: "merge ally aura buffs instead of overwriting the prior entry".
// These tests mechanically enforce that the accumulate-on-apply / subtract-on-expire pattern is
// algebraically correct — that overlapping auras compose and fully decay to zero when all their
// restores have fired. A regression of the P2 class would fail these tests deterministically.
[TestClass]
public class ActiveBuffsAlgebraTests
{
    [TestMethod]
    public void Accumulate_IntoEmptyTarget_CopiesFieldsAdditively()
    {
        var target = new ActiveBuffs();
        var deltas = SampleDeltas();

        ActiveBuffsAlgebra.Accumulate(target, deltas);

        AssertFieldsEqual(target, deltas);
    }

    [TestMethod]
    public void Accumulate_TwoCalls_StacksFields()
    {
        var target = new ActiveBuffs();
        var deltas = SampleDeltas();

        ActiveBuffsAlgebra.Accumulate(target, deltas);
        ActiveBuffsAlgebra.Accumulate(target, deltas);

        Assert.AreEqual(deltas.DamageBonus * 2f, target.DamageBonus, 0.0001f);
        Assert.AreEqual(deltas.SpeedMultiplier * 2f, target.SpeedMultiplier, 0.0001f);
        Assert.AreEqual(deltas.MountSpeedBonus * 2f, target.MountSpeedBonus, 0.0001f);
    }

    [TestMethod]
    public void Subtract_AfterAccumulate_RestoresZero()
    {
        // The core guarantee of P2 fix: accumulate then subtract is identity.
        var target = new ActiveBuffs();
        var deltas = SampleDeltas();

        ActiveBuffsAlgebra.Accumulate(target, deltas);
        ActiveBuffsAlgebra.Subtract(target, deltas);

        AssertAllFieldsZero(target);
    }

    [TestMethod]
    public void ApplyTwoAuras_RestoreFirst_SecondAuraFullyRemains()
    {
        // This is the exact scenario Codex flagged in P2:
        // Infantry aura applies damage_reduction, then Ranged aura applies speed+damage+draw.
        // When Infantry's restore fires first, the Ranged aura must survive intact.
        var target = new ActiveBuffs();
        var infantry = new ActiveBuffs
        {
            DamageBonus = 0.15f,
            DamageReductionBonus = 0.10f,
        };
        var ranged = new ActiveBuffs
        {
            SpeedMultiplier = 0.15f,
            CombatSpeedMultiplier = 0.15f,
            DamageBonus = 0.20f,
            DrawSpeedBonus = 0.20f,
        };

        ActiveBuffsAlgebra.Accumulate(target, infantry);
        ActiveBuffsAlgebra.Accumulate(target, ranged);
        ActiveBuffsAlgebra.Subtract(target, infantry);

        // Only ranged's fields should remain; infantry's contributions are gone.
        Assert.AreEqual(0f, target.DamageReductionBonus, 0.0001f, "Infantry damage reduction should be subtracted");
        Assert.AreEqual(ranged.DamageBonus, target.DamageBonus, 0.0001f, "Ranged damage should survive");
        Assert.AreEqual(ranged.SpeedMultiplier, target.SpeedMultiplier, 0.0001f, "Ranged speed should survive");
        Assert.AreEqual(ranged.DrawSpeedBonus, target.DrawSpeedBonus, 0.0001f, "Ranged draw speed should survive");
    }

    [TestMethod]
    public void ApplyTwoAuras_RestoreBoth_EverythingDecaysToZero()
    {
        var target = new ActiveBuffs();
        var a = SampleDeltas();
        var b = new ActiveBuffs
        {
            DamageBonus = 0.5f,
            MountSpeedBonus = 0.3f,
            ChargeDamageBonus = 0.25f,
        };

        ActiveBuffsAlgebra.Accumulate(target, a);
        ActiveBuffsAlgebra.Accumulate(target, b);
        ActiveBuffsAlgebra.Subtract(target, a);
        ActiveBuffsAlgebra.Subtract(target, b);

        AssertAllFieldsZero(target);
    }

    [TestMethod]
    public void Subtract_OrderIndependent_ReachesSameEndState()
    {
        // Restores from different auras may fire in any order (different expiries).
        // Verify subtract order doesn't affect the final state.
        var a = SampleDeltas();
        var b = new ActiveBuffs { DamageBonus = 0.3f, SpeedMultiplier = 0.1f };

        var target1 = new ActiveBuffs();
        ActiveBuffsAlgebra.Accumulate(target1, a);
        ActiveBuffsAlgebra.Accumulate(target1, b);
        ActiveBuffsAlgebra.Subtract(target1, a);
        ActiveBuffsAlgebra.Subtract(target1, b);

        var target2 = new ActiveBuffs();
        ActiveBuffsAlgebra.Accumulate(target2, a);
        ActiveBuffsAlgebra.Accumulate(target2, b);
        ActiveBuffsAlgebra.Subtract(target2, b); // swapped order
        ActiveBuffsAlgebra.Subtract(target2, a);

        Assert.AreEqual(target1.DamageBonus, target2.DamageBonus, 0.0001f);
        Assert.AreEqual(target1.SpeedMultiplier, target2.SpeedMultiplier, 0.0001f);
        Assert.AreEqual(target1.MountSpeedBonus, target2.MountSpeedBonus, 0.0001f);
    }

    [TestMethod]
    public void Clone_ProducesIndependentCopy_MutatingSourceDoesNotAffectClone()
    {
        var source = SampleDeltas();
        var clone = ActiveBuffsAlgebra.Clone(source);

        source.DamageBonus = 99f;

        Assert.AreNotEqual(99f, clone.DamageBonus, "Clone must be independent of source mutations");
    }

    [TestMethod]
    public void Clone_CopiesAllFields()
    {
        var source = SampleDeltas();

        var clone = ActiveBuffsAlgebra.Clone(source);

        AssertFieldsEqual(clone, source);
    }

    private static ActiveBuffs SampleDeltas() => new ActiveBuffs
    {
        SpeedMultiplier = 0.15f,
        CombatSpeedMultiplier = 0.15f,
        DamageBonus = 0.20f,
        ArmorReduction = 0.05f,
        DrawSpeedBonus = 0.20f,
        MountSpeedBonus = 0.10f,
        ChargeDamageBonus = 0.25f,
        DamageReductionBonus = 0.10f,
    };

    private static void AssertFieldsEqual(ActiveBuffs a, ActiveBuffs b)
    {
        Assert.AreEqual(b.SpeedMultiplier, a.SpeedMultiplier, 0.0001f);
        Assert.AreEqual(b.CombatSpeedMultiplier, a.CombatSpeedMultiplier, 0.0001f);
        Assert.AreEqual(b.DamageBonus, a.DamageBonus, 0.0001f);
        Assert.AreEqual(b.ArmorReduction, a.ArmorReduction, 0.0001f);
        Assert.AreEqual(b.DrawSpeedBonus, a.DrawSpeedBonus, 0.0001f);
        Assert.AreEqual(b.MountSpeedBonus, a.MountSpeedBonus, 0.0001f);
        Assert.AreEqual(b.ChargeDamageBonus, a.ChargeDamageBonus, 0.0001f);
        Assert.AreEqual(b.DamageReductionBonus, a.DamageReductionBonus, 0.0001f);
    }

    private static void AssertAllFieldsZero(ActiveBuffs target)
    {
        Assert.AreEqual(0f, target.SpeedMultiplier, 0.0001f);
        Assert.AreEqual(0f, target.CombatSpeedMultiplier, 0.0001f);
        Assert.AreEqual(0f, target.DamageBonus, 0.0001f);
        Assert.AreEqual(0f, target.ArmorReduction, 0.0001f);
        Assert.AreEqual(0f, target.DrawSpeedBonus, 0.0001f);
        Assert.AreEqual(0f, target.MountSpeedBonus, 0.0001f);
        Assert.AreEqual(0f, target.ChargeDamageBonus, 0.0001f);
        Assert.AreEqual(0f, target.DamageReductionBonus, 0.0001f);
    }
}
