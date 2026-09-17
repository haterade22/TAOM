using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CultureDoctrine;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The machine-readable form of the A/B's "preferred set": for every shipped culture that
/// carries a TAOM tactic, that tactic's weight times its multiplier beats every vanilla row it
/// shares the list with by the 1.5x that <c>TeamAIComponent.MakeDecision</c> grants the current
/// tactic (`TeamAIComponent.cs:301,316`), at the culture's canonical composition, at full
/// strength, with every scene position scored at its nominal 1. Without this, a retune of one
/// multiplier can make the doctrine's own tactic unreachable and no other test notices: the
/// first shipped file had Rohan's <c>FrontalCavalryCharge*2.0</c> above <c>CavalryDominance</c>
/// (the same formula times 1.3) and the Elven <c>DefensiveEngagement*1.5</c> above the ring.
/// </summary>
[TestClass]
public class ShippedDoctrineOrderingTests
{
    private const float StickyFactor = 1.5f;

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static DoctrineCatalog LoadShipped()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(Path.Combine(RepoRoot, @"Main\_Module\ModuleData"));
        return new CultureDoctrineConfigProvider(pathService, Substitute.For<IModLogger>()).GetCatalog();
    }

    /// <summary>The composition a culture's doctrine is written for, keyed by the TAOM tactic it
    /// ships (a new culture that reuses a tactic inherits its canonical army).</summary>
    private static TeamQuerySnapshot Canonical(DoctrineTactic taomTactic, bool defender)
    {
        float inf, rng, cav, rangedCav;
        int members = 200, enemies = 200;
        var throwing = 0f;
        var vanguard = false;
        switch (taomTactic)
        {
            case DoctrineTactic.ShieldWall: inf = 0.70f; rng = 0.20f; cav = 0.10f; rangedCav = 0f; break;
            case DoctrineTactic.TwoLineWall: inf = 0.70f; rng = 0.20f; cav = 0.10f; rangedCav = 0f; members = 300; break;
            case DoctrineTactic.ArcherRing:
            case DoctrineTactic.ArcherAdvance: inf = 0.45f; rng = 0.45f; cav = 0.10f; rangedCav = 0f; break;
            case DoctrineTactic.CavalryDominance:
            case DoctrineTactic.EoredScreen: inf = 0.30f; rng = 0.10f; cav = 0.50f; rangedCav = 0.10f; break;
            case DoctrineTactic.InfantryMass: inf = 0.75f; rng = 0.15f; cav = 0.10f; rangedCav = 0f; break;
            case DoctrineTactic.Envelop: inf = 0.75f; rng = 0.15f; cav = 0.10f; rangedCav = 0f; members = 300; break;
            case DoctrineTactic.DisciplinedLine: inf = 0.55f; rng = 0.25f; cav = 0.20f; rangedCav = 0f; break;
            case DoctrineTactic.HitAndRun: inf = 0.60f; rng = 0.20f; cav = 0.20f; rangedCav = 0f; throwing = 0.6f; break;
            case DoctrineTactic.MumakVanguard: inf = 0.50f; rng = 0.30f; cav = 0.20f; rangedCav = 0f; vanguard = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(taomTactic), taomTactic, "no canonical army");
        }
        return new TeamQuerySnapshot(
            memberCount: members, enemyUnitCount: enemies, infantryRatio: inf, rangedRatio: rng, cavalryRatio: cav,
            rangedCavalryRatio: rangedCav, remainingPowerRatio: 1f, notEngagingAdvantage: 1f,
            hasInfantry: true, hasArchers: true, hasCavalry: true, isDefenseApplicable: true, ringFits: true,
            isDefender: defender, infantryCount: (int)(members * inf), throwingRatio: throwing, hasVanguard: vanguard,
            infantryTotal: (int)(members * inf));
    }

    /// <summary>A gated variant refines a plain tactic the same culture carries: while its gate
    /// passes it must beat the plain one by the sticky factor (or it can never take the team
    /// back once the plain one holds it), and when the gate fails it is 0 and yields.</summary>
    [TestMethod]
    public void GatedVariants_BeatTheTacticTheyRefineByTheStickyFactor_AndYieldWhenTheirGateFails()
    {
        var catalog = LoadShipped();
        foreach (var (culture, variant, plain) in new[]
        {
            ("erebor", DoctrineTactic.TwoLineWall, DoctrineTactic.ShieldWall),
            ("mordor", DoctrineTactic.Envelop, DoctrineTactic.InfantryMass),
            ("gundabad", DoctrineTactic.Envelop, DoctrineTactic.InfantryMass),
        })
        {
            var rows = catalog.Resolve(culture).Tactics;
            var v = rows.Single(t => t.Tactic == variant);
            var p = rows.Single(t => t.Tactic == plain);
            var open = Canonical(variant, defender: true);
            Assert.IsTrue(VanillaTacticWeightReference.TaomWeight(variant, in open) * v.Multiplier
                > VanillaTacticWeightReference.TaomWeight(plain, in open) * p.Multiplier * StickyFactor, $"{culture}: {variant} does not beat {plain} by {StickyFactor} while its gate passes");
            // The gate closed: too few foot for two lines, or no numbers edge for the envelopment.
            var closed = new TeamQuerySnapshot(
                memberCount: 100, enemyUnitCount: 100, infantryRatio: 0.7f, rangedRatio: 0.2f, cavalryRatio: 0.1f,
                rangedCavalryRatio: 0f, remainingPowerRatio: 1f, notEngagingAdvantage: 1f,
                hasInfantry: true, hasArchers: true, hasCavalry: true, isDefenseApplicable: true, ringFits: true,
                isDefender: true, infantryCount: 70, throwingRatio: 0f, hasVanguard: false, infantryTotal: 70);
            Assert.AreEqual(0f, VanillaTacticWeightReference.TaomWeight(variant, in closed), $"{culture}: {variant} should be 0 with its gate closed");
            Assert.IsTrue(VanillaTacticWeightReference.TaomWeight(plain, in closed) > 0f, $"{culture}: {plain} still stands when the variant's gate is closed");
        }
    }

    /// <summary>Rhun carries the cavalry lead and the line on both sides; which wins is the
    /// army's shape, and both shapes are pinned (Mike, 2026-09-16: heavy cavalry like Rohan,
    /// better foot and bow).</summary>
    [TestMethod]
    public void Rhun_CavalryHeavyArmyLeadsWithCavalry_FootHeavyArmyHoldsTheLine()
    {
        var rows = LoadShipped().Resolve("khuzait").Tactics;
        var cavalry = rows.Single(t => t.Tactic == DoctrineTactic.CavalryDominance).Multiplier;
        var line = rows.Single(t => t.Tactic == DoctrineTactic.DisciplinedLine).Multiplier;
        var mounted = Canonical(DoctrineTactic.CavalryDominance, defender: false);
        var foot = Canonical(DoctrineTactic.DisciplinedLine, defender: false);
        Assert.IsTrue(DoctrineWeights.CavalryDominance(in mounted) * cavalry > DoctrineWeights.DisciplinedLine(in mounted) * line);
        Assert.IsTrue(DoctrineWeights.DisciplinedLine(in foot) * line > DoctrineWeights.CavalryDominance(in foot) * cavalry);
    }

    private static IEnumerable<(string culture, TacticEntry taom, DoctrineSide side)> ShippedTaomRows(DoctrineCatalog catalog)
    {
        foreach (var culture in catalog.CultureIds.OrderBy(c => c, StringComparer.Ordinal))
            foreach (var entry in catalog.Resolve(culture).Tactics.Where(t => !DoctrineTacticIds.IsVanilla(t.Tactic)))
                foreach (var side in new[] { DoctrineSide.Attacker, DoctrineSide.Defender })
                    if (entry.Side == DoctrineSide.Any || entry.Side == side)
                        yield return (culture, entry, side);
    }

    [TestMethod]
    public void ShippedCatalog_HasTaomRowsToCheck()
    {
        Assert.IsTrue(ShippedTaomRows(LoadShipped()).Any());
    }

    [TestMethod]
    public void EveryShippedTaomTactic_BeatsEveryVanillaRowByTheStickyFactor_AtItsCanonicalArmy()
    {
        var catalog = LoadShipped();
        var failures = new List<string>();
        foreach (var (culture, taom, side) in ShippedTaomRows(catalog))
        {
            var snapshot = Canonical(taom.Tactic, side == DoctrineSide.Defender);
            var taomWeight = VanillaTacticWeightReference.TaomWeight(taom.Tactic, in snapshot) * taom.Multiplier;
            // Skill 300 keeps every minTactics row: a campaign lord can reach any of them.
            foreach (var row in TacticRoster.Build(catalog.Resolve(culture), tacticsSkill: 300, side))
            {
                if (!DoctrineTacticIds.IsVanilla(row.Tactic))
                    continue;
                var vanilla = VanillaTacticWeightReference.Weight(row.Tactic, in snapshot, sceneScore: 1f) * row.Multiplier;
                if (taomWeight <= vanilla * StickyFactor)
                    failures.Add($"{culture} {side}: {taom.Tactic}*{taom.Multiplier} = {taomWeight:0.000} does not beat {row.Tactic}*{row.Multiplier} = {vanilla:0.000} x {StickyFactor}");
            }
        }
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void EveryShippedTaomTactic_IsPositive_AtItsCanonicalArmy()
    {
        var catalog = LoadShipped();
        foreach (var (culture, taom, side) in ShippedTaomRows(catalog))
        {
            var snapshot = Canonical(taom.Tactic, side == DoctrineSide.Defender);
            Assert.IsTrue(VanillaTacticWeightReference.TaomWeight(taom.Tactic, in snapshot) * taom.Multiplier > 0f, $"{culture} {side} {taom.Tactic}");
        }
    }
}
