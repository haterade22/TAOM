using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// Why a seated archer needs its behaviour curves rewritten (#627, 2026-09-19).
/// <c>Formation.DetachUnit</c> (Formation.cs:2389-2399), which is how vanilla hands an agent to a machine and what
/// the howdah seat now does, ends with <c>SetBehaviorValueSet(BehaviorValueSet.DefaultDetached)</c>. In v1.5.3 that
/// set leaves Melee at (8, 7, 4, 20, 1) and crushes Ranged to (0.02, 7, 0.04, 20, 0.03), roughly a hundredth of
/// Melee at every distance (HumanAIComponent.cs:770-777). A crew archer has no melee weapon, so it kept choosing a
/// stance it could not use: the second crew test logged <c>act_unequip_bow_back</c> cycling on the upper body and not
/// one arrow in a whole battle. <c>RefreshBehaviorValues</c> (HumanAIComponent.cs:783-789) re-stamps DefaultDetached
/// on any detached agent whenever its formation re-applies a movement order, so the override has to be reasserted,
/// not set once.
/// </summary>
[TestClass]
public class HowdahCrewBehaviourCurvesTests
{
    // The engine's piecewise read of a behaviour curve (HumanAIComponent.BehaviorValues.GetValueAt), kept here
    // because only these assertions need it: production reads the constants, not the curve (#627, design review P2).
    private static float ValueAt(float x, float y1, float x2, float y2, float x3, float y3)
    {
        if (x <= x2) return (y2 - y1) * x / x2 + y1;
        if (x <= x3) return (y3 - y2) * (x - x2) / (x3 - x2) + y2;
        return y3;
    }

    private static float RangedValueAt(float d) => ValueAt(d, HowdahCrewBehaviourCurves.RangedY1,
        HowdahCrewBehaviourCurves.RangedX2, HowdahCrewBehaviourCurves.RangedY2,
        HowdahCrewBehaviourCurves.RangedX3, HowdahCrewBehaviourCurves.RangedY3);

    private static float MeleeValueAt(float d) => ValueAt(d, HowdahCrewBehaviourCurves.MeleeY1,
        HowdahCrewBehaviourCurves.MeleeX2, HowdahCrewBehaviourCurves.MeleeY2,
        HowdahCrewBehaviourCurves.MeleeX3, HowdahCrewBehaviourCurves.MeleeY3);

    [TestMethod]
    public void TheRangedCurve_IsVanillasDefault_NotTheDetachedOne()
    {
        // BehaviorValueSet.Default's Ranged row, verbatim (HumanAIComponent.cs, case BehaviorValueSet.Default).
        Assert.AreEqual(2f, HowdahCrewBehaviourCurves.RangedY1, 1e-6f);
        Assert.AreEqual(7f, HowdahCrewBehaviourCurves.RangedX2, 1e-6f);
        Assert.AreEqual(4f, HowdahCrewBehaviourCurves.RangedY2, 1e-6f);
        Assert.AreEqual(20f, HowdahCrewBehaviourCurves.RangedX3, 1e-6f);
        Assert.AreEqual(5f, HowdahCrewBehaviourCurves.RangedY3, 1e-6f);
    }

    [TestMethod]
    public void TheMeleeCurve_IsFlatZero_BecauseACrewArcherCarriesNoMeleeWeapon()
    {
        Assert.AreEqual(0f, HowdahCrewBehaviourCurves.MeleeY1, 1e-6f);
        Assert.AreEqual(0f, HowdahCrewBehaviourCurves.MeleeY2, 1e-6f);
        Assert.AreEqual(0f, HowdahCrewBehaviourCurves.MeleeY3, 1e-6f);
    }

    [TestMethod]
    public void TheGoToPositionCurve_IsFlatZero_BecauseTheSeatOwnsWhereTheArcherStands()
    {
        Assert.AreEqual(0f, HowdahCrewBehaviourCurves.GoToPosY1, 1e-6f);
        Assert.AreEqual(0f, HowdahCrewBehaviourCurves.GoToPosY2, 1e-6f);
        Assert.AreEqual(0f, HowdahCrewBehaviourCurves.GoToPosY3, 1e-6f);
    }

    [TestMethod]
    public void TheRangedCurve_BeatsMelee_AtTheRangeAnEnemyFightsTheElephant()
    {
        // The measured case: enemies 1 to 2 m away horizontally, 3.2 m below, so anywhere from about 2 m to 4 m.
        // Under DefaultDetached, Melee scored about 6.9 against Ranged's 0.026 at 2 m. These curves invert that.
        foreach (float distance in new[] { 0f, 1f, 2f, 3.7f, 7f, 12f, 20f })
        {
            float ranged = RangedValueAt(distance);
            float melee = MeleeValueAt(distance);
            Assert.IsTrue(ranged > melee,
                $"at {distance} m the archer would still prefer melee (ranged {ranged}, melee {melee})");
        }
    }

    [TestMethod]
    public void TheCurveReader_MatchesTheEnginesPiecewiseInterpolation()
    {
        // HumanAIComponent.BehaviorValues.GetValueAt: y1 to y2 across 0..x2, then y2 to y3 across x2..x3.
        Assert.AreEqual(2f, RangedValueAt(0f), 1e-5f);
        Assert.AreEqual(3f, RangedValueAt(3.5f), 1e-5f);
        Assert.AreEqual(4f, RangedValueAt(7f), 1e-5f);
        Assert.AreEqual(4.5f, RangedValueAt(13.5f), 1e-5f);
        Assert.AreEqual(5f, RangedValueAt(20f), 1e-5f);
        Assert.AreEqual(5f, RangedValueAt(99f), 1e-5f, "past the last knee the curve holds");
    }

    [TestMethod]
    public void TheSeat_AppliesTheStance_WhenItSeatsAnArcherAndAgainOnTheTick()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                 | BindingFlags.Static | BindingFlags.DeclaredOnly;

        MethodInfo apply = typeof(TaomHowdahStandingPoint).GetMethod("ApplyCrewCombatStance", Any);
        Assert.IsNotNull(apply, "TaomHowdahStandingPoint no longer applies the crew behaviour curves.");
        List<string> applyCalls = IlCallScanner
            .ExtractCalledMethods(apply, apply.GetMethodBody()!.GetILAsByteArray())
            .Select(m => m.Name).ToList();
        Assert.AreEqual(3, applyCalls.Count(n => n == "OverrideBehaviorParams"),
            "expected one override each for Ranged, Melee and GoToPos: " + string.Join(", ", applyCalls));
        CollectionAssert.Contains(applyCalls, "SetFiringOrder",
            "the crew take no orders from the player, so the seat sets FireAtWill itself (Mike, 2026-09-19).");

        foreach (string caller in new[] { "OnUse", "OnTick" })
        {
            MethodInfo method = typeof(TaomHowdahStandingPoint).GetMethod(caller, Any);
            Assert.IsNotNull(method, $"TaomHowdahStandingPoint.{caller} is gone.");
            List<string> calls = IlCallScanner
                .ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray())
                .Select(m => m.Name).ToList();
            CollectionAssert.Contains(calls, "ApplyCrewCombatStance",
                $"{caller} must apply the curves; the engine re-stamps DefaultDetached on a detached agent.");
        }
    }
}
