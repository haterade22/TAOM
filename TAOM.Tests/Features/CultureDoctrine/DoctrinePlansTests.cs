using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The Phase B and C plans as data: one row per behaviour per formation, roles that exist under
/// the plan's split, finite weights. That every <c>BehaviorKind</c> maps to a behaviour type
/// <c>TeamAIGeneral</c> registers or the applier adds is pinned by
/// <c>DoctrineSwitchInvariantTests</c> against the engine, not here. The doctrine intent is
/// pinned too, so a retune that quietly turns the Dwarven wall into a charge fails here.
/// </summary>
[TestClass]
public class DoctrinePlansTests
{
    private static readonly DoctrinePlan[] All =
    {
        DoctrinePlans.ShieldWallDefender, DoctrinePlans.ShieldWallAttacker, DoctrinePlans.InfantryMass,
        DoctrinePlans.CavalryDominance, DoctrinePlans.ArcherRing,
        DoctrinePlans.TwoLineWall, DoctrinePlans.Envelop, DoctrinePlans.EoredScreen, DoctrinePlans.ArcherAdvance,
        DoctrinePlans.DisciplinedLineDefender, DoctrinePlans.DisciplinedLineAttacker, DoctrinePlans.HitAndRun,
        DoctrinePlans.MumakVanguard,
    };

    private static bool Allowed(FormationSplit split, FormationRole role)
    {
        switch (role)
        {
            case FormationRole.Cavalry: return split == FormationSplit.OneOneOneOne;
            case FormationRole.LeftCavalry:
            case FormationRole.RightCavalry: return split != FormationSplit.OneOneOneOne;
            case FormationRole.SecondInfantry: return split == FormationSplit.TwoOneTwoOne;
            case FormationRole.LeftWing:
            case FormationRole.RightWing: return split == FormationSplit.ThreeOneTwoOne;
            case FormationRole.Vanguard: return split == FormationSplit.OneOneTwoOneVanguard;
            default: return true;
        }
    }

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
                    Assert.IsTrue(Allowed(plan.Split, formation.Role), $"{plan.Name} ({plan.Split}) plans a {formation.Role} its split never fills");
    }

    [TestMethod]
    public void EveryPlan_HasADistinctName()
    {
        var names = All.Select(p => p.Name).ToList();
        Assert.AreEqual(names.Count, names.Distinct().Count());
    }

    [TestMethod]
    public void EverySplitWithExtraSlots_PlansThoseSlots()
    {
        foreach (var plan in All)
        {
            var roles = plan.Defend.Formations.Select(f => f.Role).Concat(plan.Engage.Formations.Select(f => f.Role)).ToHashSet();
            if (plan.Split == FormationSplit.TwoOneTwoOne)
                Assert.IsTrue(roles.Contains(FormationRole.SecondInfantry), plan.Name + " splits the infantry in two and leaves the second line without a plan");
            if (plan.Split == FormationSplit.ThreeOneTwoOne)
                Assert.IsTrue(roles.Contains(FormationRole.LeftWing) && roles.Contains(FormationRole.RightWing), plan.Name + " splits the infantry in three and leaves a wing without a plan");
            if (plan.Split == FormationSplit.OneOneTwoOneVanguard)
                Assert.IsTrue(roles.Contains(FormationRole.Vanguard), plan.Name + " keeps a vanguard slot and leaves it without a plan");
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
            Assert.AreEqual(1f, infantry.Weights.Single(w => w.Kind == BehaviorKind.BracedDefend).Weight, "the wall is BracedDefend at the high ground (ShieldWall when HasShield, Square under horse)");
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
        Assert.AreEqual(1f, before.Weights.Single(w => w.Kind == BehaviorKind.BracedAdvance).Weight, "BracedAdvance goes ShieldWall under fire when HasShield and Square under horse");
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
        var cycle = after.Weights.Single(w => w.Kind == BehaviorKind.CycleCharge);
        Assert.IsTrue(cycle.Weight > after.Weights.Single(w => w.Kind == BehaviorKind.TacticalCharge).Weight, "once joined the cycle charge outranks the endless one");
    }

    [TestMethod]
    public void TwoLineWall_SecondLineHoldsThenCommits()
    {
        var plan = DoctrinePlans.TwoLineWall;

        var before = plan.Defend.Formations.Single(f => f.Role == FormationRole.SecondInfantry);
        var after = plan.Engage.Formations.Single(f => f.Role == FormationRole.SecondInfantry);
        Assert.AreEqual(1f, before.Weights.Single(w => w.Kind == BehaviorKind.BracedDefend).Weight);
        Assert.IsTrue(after.Weights.Single(w => w.Kind == BehaviorKind.TacticalCharge).Weight > after.Weights.Single(w => w.Kind == BehaviorKind.BracedDefend).Weight, "the reserve commits once joined");
        Assert.AreEqual(1f, plan.Engage.Formations.Single(f => f.Role == FormationRole.MainInfantry).Weights.Single(w => w.Kind == BehaviorKind.BracedDefend).Weight, "the front still holds");
    }

    [TestMethod]
    public void Envelop_WingsWalkRoundAndTheCentreAdvances()
    {
        var plan = DoctrinePlans.Envelop;

        Assert.IsTrue(plan.AlwaysEngaged);
        Assert.AreEqual(FormationSplit.ThreeOneTwoOne, plan.Split);
        foreach (var role in new[] { FormationRole.LeftWing, FormationRole.RightWing })
            Assert.AreEqual(1f, plan.Engage.Formations.Single(f => f.Role == role).Weights.Single(w => w.Kind == BehaviorKind.EnvelopWing).Weight);
        var centre = plan.Engage.Formations.Single(f => f.Role == FormationRole.MainInfantry);
        Assert.IsTrue(centre.Weights.Any(w => w.Kind == BehaviorKind.Advance) && centre.Weights.Any(w => w.Kind == BehaviorKind.TacticalCharge));
        Assert.IsFalse(centre.Weights.Any(w => w.Kind == BehaviorKind.Charge), "the centre is a line, not the mob");
    }

    [TestMethod]
    public void EoredScreen_ScreensThenCycleChargesFromBothFlanks()
    {
        var plan = DoctrinePlans.EoredScreen;

        Assert.AreEqual(FormationSplit.OneOneTwoOne, plan.Split, "CavalryScreen needs a Left and a Right block; a single Middle block screens nothing");
        foreach (var role in new[] { FormationRole.LeftCavalry, FormationRole.RightCavalry })
        {
            var before = plan.Defend.Formations.Single(f => f.Role == role);
            var after = plan.Engage.Formations.Single(f => f.Role == role);
            Assert.IsTrue(before.Weights.Any(w => w.Kind == BehaviorKind.CavalryScreen) && before.Weights.Any(w => w.Kind == BehaviorKind.ProtectFlank));
            Assert.IsFalse(before.Weights.Any(w => w.Kind == BehaviorKind.CycleCharge), "no charge before the battle is joined");
            Assert.IsTrue(after.Weights.Single(w => w.Kind == BehaviorKind.CycleCharge).Weight >= 1f);
        }
    }

    [TestMethod]
    public void ArcherPlans_CarryVolleyControl_AndTheOthersFireAtWill()
    {
        Assert.IsTrue(DoctrinePlans.ArcherRing.HasVolleyControl);
        Assert.IsTrue(DoctrinePlans.ArcherAdvance.HasVolleyControl);
        Assert.IsTrue(DoctrinePlans.ElvenVolley.ReleaseFraction < DoctrinePlans.ElvenVolley.HoldFraction, "release inside, hold outside: hysteresis");
        foreach (var plan in All.Where(p => p != DoctrinePlans.ArcherRing && p != DoctrinePlans.ArcherAdvance))
            Assert.IsFalse(plan.HasVolleyControl, plan.Name);
    }

    [TestMethod]
    public void ArcherAdvance_AdvancesCautiouslyBehindTheArcherLine_ThenCloses()
    {
        var plan = DoctrinePlans.ArcherAdvance;

        var before = plan.Defend.Formations.Single(f => f.Role == FormationRole.MainInfantry);
        var after = plan.Engage.Formations.Single(f => f.Role == FormationRole.MainInfantry);
        Assert.AreEqual(1f, before.Weights.Single(w => w.Kind == BehaviorKind.CautiousAdvance).Weight);
        Assert.IsTrue(plan.Defend.Formations.Single(f => f.Role == FormationRole.Archers).Weights.Any(w => w.Kind == BehaviorKind.SkirmishLine));
        Assert.IsTrue(after.Weights.Any(w => w.Kind == BehaviorKind.Advance) && after.Weights.Any(w => w.Kind == BehaviorKind.TacticalCharge));
    }

    [TestMethod]
    public void DisciplinedLine_HoldsOrCreepsUntilJoined_ThenAdvances_CavalryHeldUntilEngage()
    {
        Assert.AreEqual(1f, DoctrinePlans.DisciplinedLineDefender.Defend.Formations.Single(f => f.Role == FormationRole.MainInfantry).Weights.Single(w => w.Kind == BehaviorKind.Defend).Weight);
        Assert.AreEqual(1f, DoctrinePlans.DisciplinedLineAttacker.Defend.Formations.Single(f => f.Role == FormationRole.MainInfantry).Weights.Single(w => w.Kind == BehaviorKind.CautiousAdvance).Weight);
        foreach (var plan in new[] { DoctrinePlans.DisciplinedLineDefender, DoctrinePlans.DisciplinedLineAttacker })
        {
            Assert.AreEqual(1f, plan.Engage.Formations.Single(f => f.Role == FormationRole.MainInfantry).Weights.Single(w => w.Kind == BehaviorKind.Advance).Weight, plan.Name);
            foreach (var role in new[] { FormationRole.LeftCavalry, FormationRole.RightCavalry })
            {
                Assert.IsFalse(plan.Defend.Formations.Single(f => f.Role == role).Weights.Any(w => w.Kind == BehaviorKind.Flank || w.Kind == BehaviorKind.TacticalCharge), plan.Name + ": cavalry is held until Engage");
                Assert.IsTrue(plan.Engage.Formations.Single(f => f.Role == role).Weights.Any(w => w.Kind == BehaviorKind.Flank), plan.Name);
            }
        }
    }

    [TestMethod]
    public void HitAndRun_LeadsWithTheSkirmish_AndKeepsAMeleeRowForWhenTheJavelinsAreSpent()
    {
        var plan = DoctrinePlans.HitAndRun;

        Assert.IsTrue(plan.AlwaysEngaged);
        var infantry = plan.Engage.Formations.Single(f => f.Role == FormationRole.MainInfantry);
        Assert.AreEqual(1f, infantry.Weights.Single(w => w.Kind == BehaviorKind.InfantrySkirmish).Weight);
        Assert.IsTrue(infantry.Weights.Any(w => w.Kind == BehaviorKind.TacticalCharge), "InfantrySkirmish weighs 0 once spent; something must take the line");
    }

    [TestMethod]
    public void MumakVanguard_LeadsWithTheVanguard_ThenCharges()
    {
        var plan = DoctrinePlans.MumakVanguard;

        Assert.AreEqual(FormationSplit.OneOneTwoOneVanguard, plan.Split);
        Assert.AreEqual(1f, plan.Defend.Formations.Single(f => f.Role == FormationRole.Vanguard).Weights.Single(w => w.Kind == BehaviorKind.Vanguard).Weight);
        Assert.IsTrue(plan.Engage.Formations.Single(f => f.Role == FormationRole.Vanguard).Weights.Any(w => w.Kind == BehaviorKind.TacticalCharge));
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
