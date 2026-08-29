using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Every field duty must be winnable by the weakest player its own gates admit.
///
/// The check is <c>skill + max(0,trust)*2 + rank*4 + Next(0..50) &gt;= difficulty</c>
/// (<see cref="SkillCheckService.Passes"/>). The roll caps at 50, so a row is **mathematically
/// unpassable** — not merely hard, but impossible on a natural maximum — whenever
/// <c>difficulty - skill - trustBonus - rankBonus &gt; 50</c> for a player who meets its gates.
///
/// `hideout_strike` shipped in exactly that state: difficulty 76, gated at Veteran (rank bonus 8)
/// with no trust requirement. A Veteran whose Tactics and Scouting are both 10 — entirely ordinary
/// for a soldier who has been promoted on service days rather than on those two skills — needs 58
/// on a d51. Every attempt is a guaranteed failure that costs trust, and nothing in the game tells
/// the player the duty was unwinnable when it was handed to them.
///
/// This is the floor, deliberately, not a balance opinion. Whether a 6% duty is *good* is a design
/// question that belongs to whoever plays it; whether a 0% duty is a *bug* is not.
/// </summary>
[TestClass]
public class FieldDutyReachabilityTests
{
    private static readonly string DutiesPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData", "enlistment", "enlistment_duties.json"));

    /// <summary>Mirrors <c>ServiceRank</c>'s ordinal, which is what <c>(int)record.Rank</c> feeds the check.</summary>
    private static readonly Dictionary<string, int> RankOrdinal =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Recruit"] = 0, ["Soldier"] = 1, ["Veteran"] = 2, ["Sergeant"] = 3,
        };

    /// <summary>
    /// The skill a gated-in player is assumed to have. **Zero**, because that is what a Bannerlord
    /// hero actually has in a skill they never invested in, and nothing in a duty's gates requires
    /// its support skill to have been trained at all. Charm on an orc warrior is the ordinary case,
    /// not a corner one.
    ///
    /// This constant used to be 10, described in a comment as "roughly a fresh hero's untrained
    /// value". It is not, and the whole floor rested on it: a live session on 2026-08-12 logged
    /// `duty 'recruitment_errand' failed — skill 0 ... vs difficulty 54`, and recomputing at 0 turned
    /// eight of the thirteen rows from hard into impossible while the suite stayed green. The
    /// difficulties were retuned to meet the honest floor rather than the constant being kept
    /// flattering (#438).
    /// </summary>
    private const int UntrainedSkill = 0;

    private static JArray FieldDuties()
    {
        Assert.IsTrue(File.Exists(DutiesPath), $"enlistment_duties.json not found at {DutiesPath}");
        var root = JObject.Parse(File.ReadAllText(DutiesPath));
        var duties = (JArray)root["fieldDuties"];
        Assert.IsNotNull(duties, "fieldDuties array missing");
        Assert.IsTrue(duties!.Count > 0,
            "no field duties parsed — a test that finds nothing to check passes for the wrong reason");
        return duties;
    }

    private static (int Rank, int Trust) WeakestAdmitted(JToken duty)
    {
        var gates = duty["gates"];
        var rankName = (string)gates?["minRank"] ?? "Recruit";
        Assert.IsTrue(RankOrdinal.ContainsKey(rankName),
            $"duty '{duty["id"]}' gates on an unknown rank '{rankName}'");
        // minTrust may be negative; the check only ever adds POSITIVE trust, so floor at zero.
        var minTrust = (int?)gates?["minTrust"] ?? 0;
        return (RankOrdinal[rankName], Math.Max(0, minTrust));
    }

    /// <summary>The roll this row demands of the weakest player its own gates admit.</summary>
    private static int NeededRoll(JToken duty)
    {
        var difficulty = (int)duty["difficulty"];
        var (rank, trust) = WeakestAdmitted(duty);
        return difficulty - UntrainedSkill - (trust * 2) - (rank * SkillCheckService.RankBonusPerLevel);
    }

    /// <summary>
    /// How many of the 51 equally-likely rolls clear <paramref name="needed"/>. Counted rather than
    /// divided, so the expectation below stays in integers and no float exists here to round the
    /// wrong way at a boundary.
    /// </summary>
    private static int PassingRolls(int needed)
        => Math.Max(0, Math.Min(SkillCheckService.RollRange, SkillCheckService.RollRange - needed));

    [TestMethod]
    public void EveryFieldDuty_IsPassableByTheWeakestPlayerItsGatesAdmit()
    {
        var impossible = new List<string>();

        foreach (var duty in FieldDuties())
        {
            var difficulty = (int)duty["difficulty"];
            var (rank, trust) = WeakestAdmitted(duty);
            var needed = NeededRoll(duty);

            // RollRange is exclusive (Next(51) yields 0..50), so the best possible roll is one less.
            if (needed > SkillCheckService.RollRange - 1)
                impossible.Add(
                    $"{duty["id"]} — difficulty {difficulty}, gated at rank {rank}/trust {trust}: " +
                    $"needs {needed} on a d{SkillCheckService.RollRange - 1}, so it can NEVER pass");
        }

        Assert.AreEqual(0, impossible.Count,
            "Field duties that a gated-in player cannot pass on a natural maximum roll. Every "
            + "attempt is a guaranteed trust loss the player had no way to avoid or foresee. Raise "
            + "the duty's gates or lower its difficulty:\n  " + string.Join("\n  ", impossible));
    }

    /// <summary>
    /// Passable is not the same as worth taking. A row that pays +2 on success and charges -1 on
    /// failure is a standing SINK below a one-in-three pass rate, and the player has no way to see
    /// that: the board shows standing as words, the offer states no odds, and the check happens off
    /// screen. Ten such rows shipped, which is how a 73-day soldier reached "badly thought of" with
    /// 2903 service XP and no route to Veteran (its gate is `minTrust: 0`).
    ///
    /// So the floor is the expectation, not the possibility: at skill 0, for the weakest player the
    /// row's own gates admit, a field duty may not be trust-negative on average. Every row meets it
    /// today by charging no standing for failure, which makes this a guard rather than a live
    /// constraint — that is the point. Re-add a trust cost without lowering the difficulty to match
    /// and this reddens.
    ///
    /// Interactive duties and incidents are deliberately NOT covered. Their negative outcomes follow
    /// a choice the player made from a popup that stated the stakes; a field duty is handed to you.
    /// </summary>
    [TestMethod]
    public void EveryFieldDuty_IsTrustPositiveInExpectationForTheWeakestPlayerItsGatesAdmit()
    {
        var sinks = new List<string>();

        foreach (var duty in FieldDuties())
        {
            var onSuccess = (int?)duty["reportReward"]?["trust"] ?? 0;
            var onFailure = (int?)duty["failureReward"]?["trust"] ?? 0;

            var needed = NeededRoll(duty);
            var passing = PassingRolls(needed);
            var failing = SkillCheckService.RollRange - passing;

            // Expectation x RollRange, so the comparison is exact integer arithmetic.
            var expectation = (passing * onSuccess) + (failing * onFailure);
            if (expectation < 0)
                sinks.Add(
                    $"{duty["id"]} — needs {needed} on a d{SkillCheckService.RollRange - 1} "
                    + $"({passing}/{SkillCheckService.RollRange} pass), pays {onSuccess:+#;-#;0} / "
                    + $"charges {onFailure:+#;-#;0}: {expectation / (double)SkillCheckService.RollRange:0.00} trust per offer");
        }

        Assert.AreEqual(0, sinks.Count,
            "Field duties that cost the player standing on average. Accepting camp work must never "
            + "be the wrong move for a soldier who cannot see the odds. Lower the difficulty, raise "
            + "the gates, or stop charging trust for the failure:\n  " + string.Join("\n  ", sinks));
    }

    /// <summary>
    /// The gap between the two floors above. Passable-at-all is satisfied by a row needing exactly
    /// 50 on a d50, which is one roll in fifty-one; trust-positive-in-expectation is satisfied by
    /// any row that charges nothing for failure, which since #520 is all thirteen. So both can be
    /// green while a row is, in practice, the unreachable-standing bug wearing a legal number.
    /// <c>bandit_hunt</c> (50) and <c>deserter_sweep</c> (54) landed exactly there and were lowered.
    ///
    /// Two rolls of headroom is a deliberately weak line, not a balance opinion. A real pass-rate
    /// floor at, say, one in three would redden most of the ladder at skill 0, and whether a 6% duty
    /// is GOOD belongs to whoever plays it. Whether a 2% duty is indistinguishable from a broken one
    /// does not.
    /// </summary>
    [TestMethod]
    public void NoFieldDuty_IsPassableOnlyOnANearMaximumRoll()
    {
        var razorThin = FieldDuties()
            .Select(d => new { Id = (string)d["id"], Passing = PassingRolls(NeededRoll(d)) })
            .Where(x => x.Passing <= 2)
            .Select(x => $"{x.Id} ({x.Passing}/{SkillCheckService.RollRange})")
            .ToList();

        Assert.AreEqual(0, razorThin.Count,
            "Field duties a gated-in player clears only on a near-maximum roll. Legal under the "
            + "impossibility floor, and in play indistinguishable from it:\n  "
            + string.Join("\n  ", razorThin));
    }

    [TestMethod]
    public void FieldDutyDifficulty_RisesWithTheRankRequiredToBeOfferedIt()
    {
        // Not a balance assertion — an ORDERING one. If the hardest work open to a Recruit is
        // harder than anything a Veteran can be handed, the gates are not doing the job they exist
        // for, and a promotion that should have opened up harder work has instead handed the player
        // something easier. A Recruit row at 80 against a Soldier band topping out at 52 is the
        // shape this catches.
        //
        // Read the scope precisely, because the example that used to sit here did not: this compares
        // band CEILINGS, so overlap is legal and an individual inversion between two rows is
        // invisible to it. (The old comment offered `bandit_hunt` 58 Recruit "above" `deserter_sweep`
        // 64 Soldier, which is neither an inversion nor something this assertion could see. Both
        // numbers were real; the sentence was not.)
        var byRank = new Dictionary<int, (int Min, int Max, List<string> Ids)>();

        foreach (var duty in FieldDuties())
        {
            var difficulty = (int)duty["difficulty"];
            var (rank, _) = WeakestAdmitted(duty);
            if (!byRank.TryGetValue(rank, out var band))
                band = (int.MaxValue, int.MinValue, new List<string>());
            band.Ids.Add($"{duty["id"]}({difficulty})");
            byRank[rank] = (Math.Min(band.Min, difficulty), Math.Max(band.Max, difficulty), band.Ids);
        }

        var ranks = byRank.Keys.OrderBy(r => r).ToList();
        var inversions = new List<string>();
        for (var i = 1; i < ranks.Count; i++)
        {
            var lower = byRank[ranks[i - 1]];
            var higher = byRank[ranks[i]];
            // The hardest duty at a lower rank must not exceed the hardest at a higher one. Bands
            // are allowed to OVERLAP — an easy Veteran duty is fine — but the ceiling must rise.
            if (lower.Max > higher.Max)
                inversions.Add(
                    $"rank {ranks[i - 1]} tops out at {lower.Max} [{string.Join(" ", lower.Ids)}] " +
                    $"but rank {ranks[i]} tops out at only {higher.Max} [{string.Join(" ", higher.Ids)}]");
        }

        Assert.AreEqual(0, inversions.Count,
            "A lower rank is gated into harder work than the rank above it:\n  "
            + string.Join("\n  ", inversions));
    }
}
