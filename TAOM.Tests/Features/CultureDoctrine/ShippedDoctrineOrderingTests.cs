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
        switch (taomTactic)
        {
            case DoctrineTactic.ShieldWall: inf = 0.70f; rng = 0.20f; cav = 0.10f; rangedCav = 0f; break;
            case DoctrineTactic.ArcherRing: inf = 0.45f; rng = 0.45f; cav = 0.10f; rangedCav = 0f; break;
            case DoctrineTactic.CavalryDominance: inf = 0.30f; rng = 0.10f; cav = 0.50f; rangedCav = 0.10f; break;
            case DoctrineTactic.InfantryMass: inf = 0.75f; rng = 0.15f; cav = 0.10f; rangedCav = 0f; break;
            default: throw new ArgumentOutOfRangeException(nameof(taomTactic), taomTactic, "no canonical army");
        }
        return new TeamQuerySnapshot(
            memberCount: 200, enemyUnitCount: 200, infantryRatio: inf, rangedRatio: rng, cavalryRatio: cav,
            rangedCavalryRatio: rangedCav, remainingPowerRatio: 1f, notEngagingAdvantage: 1f,
            hasInfantry: true, hasArchers: true, hasCavalry: true, isDefenseApplicable: true, ringFits: true,
            isDefender: defender);
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
