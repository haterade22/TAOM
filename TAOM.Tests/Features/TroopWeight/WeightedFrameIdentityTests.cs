using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// The 2026-09-06 usage frame shows capacity as <c>weighted / true-base</c> while the engine keeps
/// enforcing <c>raw / deflated</c>. Everything about that being safe rests on one identity:
///
/// <code>raw &gt; deflated  ⟺  weighted &gt; base</code>
///
/// It is not merely cosmetic. Two vanilla confirmation prompts read properties the display hook
/// rewrites — <c>RecruitmentVM.ExecuteDone</c>'s "Over Limit" inquiry
/// (<c>CurrentPartySize &lt;= PartyCapacity</c>) and the party screen's done-path check of the troop-limit
/// warning flags — so if the identity ever broke, a confirmation dialog would silently start firing at a
/// different threshold than the engine's actual cap.
///
/// Deep review (2026-09-06, data-flow agent) flagged that the identity currently holds partly by
/// coincidence of the shipped weight range rather than by construction. These tests make the coincidence
/// an enforced invariant: they read the real <c>troop_weights.xml</c>, so adding a heavier troop tier or
/// changing <see cref="TroopWeightService.ComputeSizePenalty"/>'s clamp floor fails here rather than
/// quietly moving a player-facing prompt.
/// </summary>
[TestClass]
public class WeightedFrameIdentityTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    /// <summary>Every distinct weight the mod actually ships, plus the 1.0 default for unlisted troops.</summary>
    private static List<float> ShippedWeights()
    {
        var path = Path.Combine(
            FindRepoRoot(), "Main", "_Module", "ModuleData", "TroopWeights", "troop_weights.xml");
        Assert.IsTrue(File.Exists(path), $"troop_weights.xml not found at {path}");

        var weights = XDocument.Load(path)
            .Descendants("TroopWeight")
            .Select(e => (string)e.Attribute("weight"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture))
            .Concat(new[] { 1.0f })
            .Distinct()
            .OrderBy(w => w)
            .ToList();

        Assert.IsTrue(weights.Count > 1, "Expected troop_weights.xml to declare at least one non-default weight");
        return weights;
    }

    /// <summary>The deflated limit the engine actually enforces, for a uniform-weight roster.</summary>
    private static (int Weighted, int Deflated) Frame(int raw, float weight, int baseLimit)
    {
        int weighted = (int)Math.Ceiling(raw * (double)weight);
        int penalty = TroopWeightService.ComputeSizePenalty(raw, weighted, baseLimit);
        return (weighted, baseLimit - penalty);
    }

    [TestMethod]
    public void OverCapacity_IsTheSameVerdict_InBothFrames_AcrossEveryShippedWeight()
    {
        var weights = ShippedWeights();
        var mismatches = new List<string>();

        // baseLimit >= 2 is ComputeSizePenalty's own precondition for applying any penalty; raw >= 2 is the
        // real boundary of the identity (see the raw==1 test below for why, and why it does not matter).
        for (int baseLimit = 2; baseLimit <= 300; baseLimit += 7)
        {
            foreach (var weight in weights)
            {
                for (int raw = 2; raw <= baseLimit * 2; raw++)
                {
                    var (weighted, deflated) = Frame(raw, weight, baseLimit);
                    bool enforced = raw > deflated;
                    bool displayed = weighted > baseLimit;

                    if (enforced != displayed)
                        mismatches.Add(
                            $"base={baseLimit} weight={weight} raw={raw} weighted={weighted} "
                            + $"deflated={deflated} enforced={enforced} displayed={displayed}");
                }
            }
        }

        Assert.AreEqual(
            0, mismatches.Count,
            "The displayed over-capacity verdict must match the engine-enforced one, or a vanilla "
            + "confirmation prompt fires at a different threshold than the real cap. First few:\n"
            + string.Join("\n", mismatches.Take(5)));
    }

    [TestMethod]
    public void OverCapacity_KnownBoundary_SingleBodyUnderAClampedPenalty()
    {
        // The one place the identity breaks, recorded rather than papered over. A one-body party whose
        // single troop outweighs its entire base limit gets its penalty clamped to baseLimit-1, flooring
        // the deflated limit at 1 — so "raw > deflated" reads 1 > 1 = false while "weighted > base" is
        // true. Unreachable in practice: it needs baseLimit < weight, and DefaultPartySizeLimitModel's
        // floor for a party with no leader is already 20 bodies, against a heaviest shipped weight of 10.
        var (weighted, deflated) = Frame(raw: 1, weight: 4.0f, baseLimit: 3);

        Assert.AreEqual(4, weighted);
        Assert.AreEqual(1, deflated, "penalty clamps to baseLimit-1, flooring the enforced limit at 1");
        Assert.IsTrue(weighted > 3, "the displayed frame calls this over capacity");
        Assert.IsFalse(1 > deflated, "the enforced frame does not — this is the documented boundary");
    }

    [TestMethod]
    public void HeaviestShippedWeight_StaysWithinTheRangeTheIdentityWasProvenFor()
    {
        // A guard on the guard: the sweep above runs to baseLimit 300. If someone adds a weight so large
        // that realistic party sizes fall below it, the raw==1 boundary above stops being unreachable.
        var heaviest = ShippedWeights().Max();

        Assert.IsTrue(
            heaviest <= 20f,
            $"Heaviest shipped troop weight is {heaviest}. Above ~20 a normal party-size limit can drop "
            + "under a single troop's weight, which makes the clamped single-body boundary reachable and "
            + "the displayed/enforced over-capacity verdicts can disagree. Re-derive the identity first.");
    }
}
