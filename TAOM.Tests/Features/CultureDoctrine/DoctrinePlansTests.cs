using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The four Phase B plans as data: one row per behaviour per formation, roles that exist under
/// the plan's split, finite weights. That every <c>BehaviorKind</c> maps to a behaviour type
/// <c>TeamAIGeneral</c> registers is pinned by <c>DoctrineSwitchInvariantTests</c> against the
/// engine, not here. The doctrine intent is pinned too, so a retune that quietly turns the
/// Dwarven wall into a charge fails here.
/// </summary>
[TestClass]
public class DoctrinePlansTests
{
    private static readonly DoctrinePlan[] All =
    {
        DoctrinePlans.ShieldWallDefender, DoctrinePlans.ShieldWallAttacker, DoctrinePlans.InfantryMass,
        DoctrinePlans.CavalryDominance, DoctrinePlans.ArcherRing,
    };

    [TestMethod]
    public void EveryPlan_HasNoDuplicateBehaviourPerFormation()
    {
        foreach (var plan in All)
            foreach (var phase in new[] { plan.Defend, plan.Engage })
                foreach (var formation in phase.Formations)
                {
                    var kinds = formation.Weights.Select(w => w.Kind).ToList();
                    Assert.AreEqual(kinds.Count, kinds.Distinct().Count(), $"{plan.Name} {formation.Role} repeats a behaviour");
                }
    }

    [TestMethod]
    public void EveryPlan_HasNoDuplicateRolePerPhase()
    {
        foreach (var plan in All)
            foreach (var phase in new[] { plan.Defend, plan.Engage })
            {
                var roles = phase.Formations.Select(f => f.Role).ToList();
                Assert.AreEqual(roles.Count, roles.Distinct().Count(), $"{plan.Name} repeats a role");
            }
    }

    [TestMethod]
    public void EveryPlan_UsesOnlyRolesItsSplitProduces()
    {
        foreach (var plan in All)
            foreach (var phase in new[] { plan.Defend, plan.Engage })
                foreach (var formation in phase.Formations)
                {
                    var single = formation.Role == FormationRole.Cavalry;
                    var pair = formation.Role == FormationRole.LeftCavalry || formation.Role == FormationRole.RightCavalry;
                    if (plan.Split == FormationSplit.OneOneTwoOne)
                        Assert.IsFalse(single, $"{plan.Name} splits cavalry in two but plans a single Cavalry role");
                    else
                        Assert.IsFalse(pair, $"{plan.Name} keeps one cavalry block but plans a flank role");
                }
    }

    [TestMethod]
    public void EveryPlan_WeightsAreFiniteAndNonNegative()
    {
        foreach (var plan in All)
            foreach (var phase in new[] { plan.Defend, plan.Engage })
                foreach (var formation in phase.Formations)
                    foreach (var weight in formation.Weights)
                        Assert.IsTrue(weight.Weight >= 0f && weight.Weight <= 100f && !float.IsNaN(weight.Weight), $"{plan.Name} {formation.Role} {weight.Kind} = {weight.Weight}");
    }

    [TestMethod]
    public void ShieldWallDefender_HoldsTheHighGroundInBothPhases_AndCavalryNeverFlanks()
    {
        var plan = DoctrinePlans.ShieldWallDefender;

        foreach (var phase in new[] { plan.Defend, plan.Engage })
        {
            var infantry = phase.Formations.Single(f => f.Role == FormationRole.MainInfantry);
            Assert.AreEqual(1f, infantry.Weights.Single(w => w.Kind == BehaviorKind.Defend).Weight, "the wall is BehaviorDefend at the high ground (ShieldWall when HasShield)");
            Assert.IsTrue(infantry.Weights.Single(w => w.Kind == BehaviorKind.TacticalCharge).Weight < 1f, "the wall counter-charges only when the engine's own weight is high");
            Assert.IsFalse(infantry.Weights.Any(w => w.Kind == BehaviorKind.Charge), "no mob charge row");
            foreach (var role in new[] { FormationRole.LeftCavalry, FormationRole.RightCavalry })
            {
                var cavalry = phase.Formations.Single(f => f.Role == role);
                Assert.IsTrue(cavalry.Weights.Any(w => w.Kind == BehaviorKind.ProtectFlank));
                Assert.IsFalse(cavalry.Weights.Any(w => w.Kind == BehaviorKind.Flank), "Dwarven cavalry guards the wall's flanks, it does not go hunting");
            }
        }
        Assert.IsFalse(plan.AlwaysEngaged);
        Assert.AreEqual(FormationSplit.OneOneTwoOne, plan.Split);
    }

    [TestMethod]
    public void ShieldWallAttacker_AdvancesInAWall_AndCommitsHarderOnceJoined()
    {
        var plan = DoctrinePlans.ShieldWallAttacker;

        var before = plan.Defend.Formations.Single(f => f.Role == FormationRole.MainInfantry);
        var after = plan.Engage.Formations.Single(f => f.Role == FormationRole.MainInfantry);
        Assert.AreEqual(1f, before.Weights.Single(w => w.Kind == BehaviorKind.Advance).Weight, "BehaviorAdvance goes ShieldWall under fire when HasShield");
        Assert.IsFalse(before.Weights.Any(w => w.Kind == BehaviorKind.Charge));
        Assert.IsTrue(after.Weights.Single(w => w.Kind == BehaviorKind.TacticalCharge).Weight > before.Weights.Single(w => w.Kind == BehaviorKind.TacticalCharge).Weight);
    }

    [TestMethod]
    public void InfantryMass_IsAlwaysEngaged_AndLeadsWithTheInfantryCharge()
    {
        var plan = DoctrinePlans.InfantryMass;

        Assert.IsTrue(plan.AlwaysEngaged, "no cautious phase: the horde comes on from the first tick");
        var infantry = plan.Engage.Formations.Single(f => f.Role == FormationRole.MainInfantry);
        Assert.IsTrue(infantry.Weights.Any(w => w.Kind == BehaviorKind.Charge));
        Assert.IsTrue(infantry.Weights.Any(w => w.Kind == BehaviorKind.TacticalCharge));
        Assert.IsFalse(infantry.Weights.Any(w => w.Kind == BehaviorKind.Defend || w.Kind == BehaviorKind.HoldHighGround));
    }

    [TestMethod]
    public void CavalryDominance_KeepsOneCavalryBlock_LeadsWithVanguard_ThenChargesAndFlanks()
    {
        var plan = DoctrinePlans.CavalryDominance;

        Assert.AreEqual(FormationSplit.OneOneOneOne, plan.Split);
        Assert.AreEqual(7f, plan.BattleJoinedSeconds, "vanilla's cavalry tactic waits 7 s of closing distance, not the infantry 5");
        var before = plan.Defend.Formations.Single(f => f.Role == FormationRole.Cavalry);
        var after = plan.Engage.Formations.Single(f => f.Role == FormationRole.Cavalry);
        Assert.IsTrue(before.Weights.Any(w => w.Kind == BehaviorKind.Vanguard));
        Assert.IsTrue(after.Weights.Any(w => w.Kind == BehaviorKind.TacticalCharge) && after.Weights.Any(w => w.Kind == BehaviorKind.Flank));
    }

    [TestMethod]
    public void ArcherRing_RingsTheArchers_AndHoldsTheRingInBothPhases()
    {
        var plan = DoctrinePlans.ArcherRing;

        foreach (var phase in new[] { plan.Defend, plan.Engage })
        {
            var infantry = phase.Formations.Single(f => f.Role == FormationRole.MainInfantry);
            var archers = phase.Formations.Single(f => f.Role == FormationRole.Archers);
            Assert.AreEqual(1f, infantry.Weights.Single(w => w.Kind == BehaviorKind.DefensiveRing).Weight);
            Assert.AreEqual(1f, archers.Weights.Single(w => w.Kind == BehaviorKind.FireFromInfantryCover).Weight, "the archers' Square is what the ring sizes itself around");
            Assert.IsFalse(infantry.Weights.Any(w => w.Kind == BehaviorKind.Charge));
        }
    }
}
