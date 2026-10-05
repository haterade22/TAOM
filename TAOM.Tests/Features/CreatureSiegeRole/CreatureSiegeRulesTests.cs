using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The pure rules of the creature siege role (docs/features/creature-siege-role.md): who is active, the detachment cost,
/// the gate damage gate, the exclusion ids, the gate status, the past-the-gate test, the ram, the role decision, the slots
/// and the reapply rule. No engine types. This file holds activation, cost, damage and exclusion; the gate and role rules
/// are in the other parts of the class. Every float that comes from the engine is gated as a positive requirement
/// (csharp-architecture.md "Engine-Float Decision Gates"), so each gate has its NaN test.
/// </summary>
[TestClass]
public partial class CreatureSiegeRulesTests
{
    // --- the engine's selection rule, mirrored ----------------------------------------------------------------------

    /// <summary>
    /// <c>DetachmentManager.TickDetachments</c> (v1.5.4 :159 and :167): the best cost starts at <c>float.MaxValue</c> and an
    /// agent is taken only when <c>best &gt; cost</c>. Mirrored here, not in production, so the tests can say WHY a creature's
    /// cost is +Infinity and not <c>float.MaxValue</c>: the cost the engine compares is <c>distance x multiplier</c>, which
    /// stays under <c>float.MaxValue</c> for any distance under 1 m.
    /// </summary>
    private static bool EngineWouldAssign(float cost) => float.MaxValue > cost;

    // --- activation -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Activate_EveryConditionHolds_IsActive()
    {
        Assert.AreEqual(ActivationVerdict.Active,
            CreatureSiegeRules.Activate(isSiegeBattle: true, hasSallyOutController: false, isClientOrReplay: false,
                roleEnabled: true, hasCreatureRaces: true, hasOuterGate: true));
    }

    [DataTestMethod]
    [DataRow(false, false, false, true, true, true, ActivationVerdict.NotSiege)]
    [DataRow(true, true, false, true, true, true, ActivationVerdict.SallyOutOrRelief)]
    [DataRow(true, false, true, true, true, true, ActivationVerdict.ClientOrReplay)]
    [DataRow(true, false, false, false, true, true, ActivationVerdict.Disabled)]
    [DataRow(true, false, false, true, false, true, ActivationVerdict.NoCreatureRace)]
    [DataRow(true, false, false, true, true, false, ActivationVerdict.NoOuterGate)]
    public void Activate_OneConditionFails_NamesThatCondition(bool siege, bool sallyOut, bool client, bool enabled,
        bool races, bool gate, ActivationVerdict expected)
    {
        Assert.AreEqual(expected, CreatureSiegeRules.Activate(siege, sallyOut, client, enabled, races, gate));
    }

    [TestMethod]
    public void Activate_SeveralConditionsFail_ReportsTheFirstInOrder()
    {
        // The order is the order of cost: a field battle is "not a siege" however the rest reads.
        Assert.AreEqual(ActivationVerdict.NotSiege, CreatureSiegeRules.Activate(false, true, true, false, false, false));
        Assert.AreEqual(ActivationVerdict.SallyOutOrRelief, CreatureSiegeRules.Activate(true, true, true, false, false, false));
        Assert.AreEqual(ActivationVerdict.ClientOrReplay, CreatureSiegeRules.Activate(true, false, true, false, false, false));
        Assert.AreEqual(ActivationVerdict.Disabled, CreatureSiegeRules.Activate(true, false, false, false, false, false));
        Assert.AreEqual(ActivationVerdict.NoCreatureRace, CreatureSiegeRules.Activate(true, false, false, true, false, false));
    }

    [TestMethod]
    public void Activate_OfAllSixtyFourCombinations_IsActiveOnlyWhenEveryConditionHolds()
    {
        for (var bits = 0; bits < 64; bits++)
        {
            var siege = (bits & 1) != 0;
            var noSallyOut = (bits & 2) != 0;
            var noClient = (bits & 4) != 0;
            var enabled = (bits & 8) != 0;
            var races = (bits & 16) != 0;
            var gate = (bits & 32) != 0;

            var verdict = CreatureSiegeRules.Activate(siege, hasSallyOutController: !noSallyOut, isClientOrReplay: !noClient,
                enabled, races, gate);

            Assert.AreEqual(bits == 63, verdict == ActivationVerdict.Active, $"combination {bits}: {verdict}");
        }
    }

    // --- the race mask ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void BuildRaceMask_TwoRaces_FlagsExactlyThoseIds()
    {
        var mask = CreatureSiegeRules.BuildRaceMask(new[] { 1, 2 });

        CollectionAssert.AreEqual(new[] { false, true, true }, mask);
    }

    [TestMethod]
    public void BuildRaceMask_SparseId_SizesTheMaskToItsHighestId()
    {
        var mask = CreatureSiegeRules.BuildRaceMask(new[] { 5 });

        Assert.AreEqual(6, mask.Length);
        Assert.IsTrue(mask[5]);
        Assert.AreEqual(1, mask.Count(flag => flag));
    }

    [TestMethod]
    public void BuildRaceMask_NullOrEmpty_IsAnEmptyMask()
    {
        Assert.AreEqual(0, CreatureSiegeRules.BuildRaceMask(null).Length);
        Assert.AreEqual(0, CreatureSiegeRules.BuildRaceMask(Array.Empty<int>()).Length);
    }

    [TestMethod]
    public void BuildRaceMask_NegativeIds_AreIgnored_AndDuplicatesCollapse()
    {
        var mask = CreatureSiegeRules.BuildRaceMask(new[] { -3, 2, 2, int.MinValue });

        CollectionAssert.AreEqual(new[] { false, false, true }, mask);
    }

    [TestMethod]
    public void BuildRaceMask_AnIdNoRaceTableCouldHold_IsIgnored_NotAHugeAllocation()
    {
        var mask = CreatureSiegeRules.BuildRaceMask(new[] { int.MaxValue, 1 << 20, 1 });

        CollectionAssert.AreEqual(new[] { false, true }, mask);
        Assert.AreEqual(0, CreatureSiegeRules.BuildRaceMask(new[] { int.MaxValue }).Length);
    }

    [TestMethod]
    public void BuildRaceMask_EveryCallBuildsItsOwnArray()
    {
        var first = CreatureSiegeRules.BuildRaceMask(new[] { 1 });
        first[1] = false;

        CollectionAssert.AreEqual(new[] { false, true }, CreatureSiegeRules.BuildRaceMask(new[] { 1 }));
    }

    // --- the detachment cost ----------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(1f)]
    [DataRow(10f)]
    [DataRow(0f)]
    [DataRow(-5f)]
    [DataRow(float.MaxValue)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void DetachmentCost_ACreature_IsPositiveInfinity_WhateverTheBase(float baseCost)
    {
        Assert.AreEqual(float.PositiveInfinity, CreatureSiegeRules.DetachmentCost(isCreature: true, baseCost));
    }

    [DataTestMethod]
    [DataRow(1f)]
    [DataRow(10f)]
    [DataRow(0f)]
    [DataRow(float.PositiveInfinity)]
    public void DetachmentCost_ANonCreature_KeepsItsOwnValue(float baseCost)
    {
        Assert.AreEqual(baseCost, CreatureSiegeRules.DetachmentCost(isCreature: false, baseCost));
    }

    [TestMethod]
    public void DetachmentCost_ANonCreatureWithANaNBase_StaysNaN_NotAVerdictFromGarbage()
    {
        Assert.IsTrue(float.IsNaN(CreatureSiegeRules.DetachmentCost(isCreature: false, float.NaN)));
    }

    [TestMethod]
    public void EngineSelection_AgentWithAPositiveInfiniteCost_NeverWinsAnySlot()
    {
        // The engine multiplies the cost into a distance (UsableMachine.cs:980): +Inf x 5 m is +Inf, and +Inf x 0 m is NaN.
        // A strict '>' against float.MaxValue is false for both, so the creature is never the lowest-cost candidate.
        var cost = CreatureSiegeRules.DetachmentCost(isCreature: true, 1f);

        Assert.IsFalse(EngineWouldAssign(5f * cost));
        Assert.IsFalse(EngineWouldAssign(0f * cost), "0 x +Infinity is NaN, which also loses a '>' compare");
        Assert.IsFalse(EngineWouldAssign(float.NaN));
    }

    [TestMethod]
    public void EngineSelection_FloatMaxValueWouldNotBeEnough_BecauseTheMultiplierMeetsADistance()
    {
        // Why +Infinity and not float.MaxValue: under 1 m the product is smaller than float.MaxValue and the agent wins.
        Assert.IsTrue(EngineWouldAssign(float.MaxValue * 0.5f));
        Assert.IsFalse(EngineWouldAssign(float.MaxValue), "and exactly MaxValue loses only by the strict compare");
        Assert.IsFalse(EngineWouldAssign(float.PositiveInfinity));
    }

    // --- the gate damage gate ---------------------------------------------------------------------------------------

    private static float Scale(float multiplier = 2f, float damage = 100f, bool friendly = false,
        bool mount = false, bool missile = false, bool creature = true) =>
        CreatureSiegeRules.ScaleGateDamage(multiplier, damage, friendly, mount, missile, creature);

    [TestMethod]
    public void ScaleGateDamage_ACreatureMeleeBlowOnAGate_IsMultiplied()
    {
        Assert.AreEqual(200f, Scale());
        Assert.AreEqual(450f, Scale(multiplier: 4.5f));
    }

    [TestMethod]
    public void ScaleGateDamage_AMultiplierOfOne_ChangesNothing()
    {
        Assert.AreEqual(100f, Scale(multiplier: 1f));
    }

    [TestMethod]
    public void ScaleGateDamage_FriendlyFire_IsUnchanged() => Assert.AreEqual(100f, Scale(friendly: true));

    [TestMethod]
    public void ScaleGateDamage_TheAttackerIsAMount_IsUnchanged() => Assert.AreEqual(100f, Scale(mount: true));

    [TestMethod]
    public void ScaleGateDamage_AMissile_IsUnchanged() => Assert.AreEqual(100f, Scale(missile: true));

    [TestMethod]
    public void ScaleGateDamage_TheAttackerIsNotACreature_IsUnchanged() => Assert.AreEqual(100f, Scale(creature: false));

    [TestMethod]
    public void ScaleGateDamage_ANaNDamage_IsHandedBack_NotAVerdictFromGarbage()
    {
        Assert.IsTrue(float.IsNaN(Scale(damage: float.NaN)));
    }

    [DataTestMethod]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(-0.001f)]
    public void ScaleGateDamage_ADamageThatIsNotFiniteAndPositive_IsUnchanged(float damage)
    {
        Assert.AreEqual(damage, Scale(damage: damage));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0.5f)]
    [DataRow(0f)]
    [DataRow(-2f)]
    public void ScaleGateDamage_AMultiplierThatIsNotFiniteAndAtLeastOne_IsUnchanged(float multiplier)
    {
        Assert.AreEqual(100f, Scale(multiplier: multiplier));
    }

    [TestMethod]
    public void ScaleGateDamage_ARunawayProduct_IsUnchanged_SoTheEngineNeverGetsInfinity()
    {
        // The exit is gated too: a finite damage times a finite multiplier can still overflow a float.
        Assert.AreEqual(float.MaxValue, Scale(multiplier: 10f, damage: float.MaxValue));
    }
}
