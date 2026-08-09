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
    /// The skill a gated-in player is assumed to have. Ten is not pessimistic — it is roughly a
    /// fresh hero's untrained value, and nothing in the gates requires a duty's support skill to
    /// have been trained at all. A row that needs more than this from an untrained soldier is
    /// relying on a prerequisite it never states.
    /// </summary>
    private const int UntrainedSkill = 10;

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

    [TestMethod]
    public void EveryFieldDuty_IsPassableByTheWeakestPlayerItsGatesAdmit()
    {
        var impossible = new List<string>();

        foreach (var duty in FieldDuties())
        {
            var difficulty = (int)duty["difficulty"];
            var (rank, trust) = WeakestAdmitted(duty);
            var needed = difficulty - UntrainedSkill - (trust * 2) - (rank * SkillCheckService.RankBonusPerLevel);

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

    [TestMethod]
    public void FieldDutyDifficulty_RisesWithTheRankRequiredToBeOfferedIt()
    {
        // Not a balance assertion — an ORDERING one. If a Recruit-gated duty is harder than a
        // Veteran-gated one, the gates are not doing the job they exist for, and the promotion
        // that should have opened up harder work has instead handed the player something easier.
        // This is how `bandit_hunt` (58, Recruit) came to sit above `deserter_sweep` (64, Soldier)
        // in difficulty while sitting below it in requirement.
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
