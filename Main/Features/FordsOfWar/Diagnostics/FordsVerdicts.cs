using System;
using System.Collections.Generic;
using System.Text;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The sixteen verdict rows, judged from evidence alone. Pure: no clock, no engine, no logger. A part passes only on positive
/// evidence, fails only on a contradicting number, and is NotSeen when its stage never ran. Checks 1 and 2 read the session;
/// checks 3 to 16 read the run.
/// </summary>
internal static partial class FordsVerdicts
{
    /// <summary>The start of the reply <c>FordsSetupChoices</c> gives for an unknown argument; pinned by a test.</summary>
    internal const string UnknownPrefix = "Unknown ";

    /// <summary>The start of the reply <c>taom.fords_gold</c> gives after a grant; pinned by a test.</summary>
    internal const string GrantPrefix = "Added ";

    internal static readonly IReadOnlyDictionary<int, string> Titles = new Dictionary<int, string>
    {
        [1] = "Main-menu entry, prompts, cancel",
        [2] = "taom.fords_start",
        [3] = "Match started, no spawn failure, hero ids resolve",
        [4] = "Countdown, towers, waves, march, fight, gold on kills, camera",
        [5] = "Escape menu",
        [6] = "Gold and packs",
        [7] = "Heroes, respawn, buyback",
        [8] = "Orders and hold",
        [9] = "Melee at an outer tower, lock chain",
        [10] = "No own-side damage",
        [11] = "Volley item and damage formula",
        [12] = "Raging warg returns to its formation",
        [13] = "Capture row",
        [14] = "Keep falls, result, exit",
        [15] = "Forced start failure, 3 s exit, notice",
        [16] = "Agent cap over 30 minutes",
    };

    internal static IReadOnlyList<FordsVerdictRow> Judge(FordsSessionEvidence session, FordsRunEvidence run) => new[]
    {
        Check01(session),
        Check02(session, run),
        Check03(run),
        Check04(run),
        Check05(run),
        Check06(session, run),
        Check07(run),
        Check08(run),
        Check09(run),
        Check10(run),
        Check11(run),
        Check12(run),
        Check13(run),
        Check14(run),
        Check15(run),
        Check16(run),
    };

    private static FordsVerdictRow Row(int check, FordsPart[] parts, string info, string rule, string human) =>
        new(check, Titles[check], FordsResults.Combine(parts), parts, info, rule, human);

    private static FordsPart Part(string name, FordsResult result, string numbers = "") => new(name, result, numbers);

    private static string N(double v) => FordsLogLine.F1(v);

    private static string N3(double v) => FordsLogLine.F3(v);

    private static int Count(IReadOnlyDictionary<string, int> map, string key) => map.TryGetValue(key, out var n) ? n : 0;

    private static FordsStageMark? Stage(FordsRunEvidence run, string name)
    {
        for (var i = 0; i < run.Stages.Count; i++)
        {
            if (run.Stages[i].Name == name)
                return run.Stages[i];
        }

        return null;
    }

    /// <summary>Blows summed over the towers, for one class (null: all) and one reason (null: all).</summary>
    internal static long Blows(FordsRunEvidence run, TowerBlowClass? cls, TowerDamageReason? reason)
    {
        long total = 0;
        foreach (var tower in run.Towers.Values)
        {
            for (var c = 0; c < FordsTowerEvidence.BlowClasses; c++)
            {
                if (cls is { } only && (int)only != c)
                    continue;
                for (var r = 0; r < FordsTowerEvidence.BlowReasons; r++)
                {
                    if (reason is not { } wanted || (int)wanted == r)
                        total += tower.Blows[c, r];
                }
            }
        }

        return total;
    }

    private static bool AnyDrift(FordsRunEvidence run)
    {
        foreach (var tower in run.Towers.Values)
        {
            if (tower.DriftFlagged)
                return true;
        }

        return false;
    }

    private static string Join(IEnumerable<string> items) => string.Join(", ", items);

    private static string Pairs(IReadOnlyDictionary<string, int> map)
    {
        var sb = new StringBuilder();
        foreach (var pair in map)
            sb.Append(sb.Length == 0 ? "" : " ").Append(pair.Key).Append('=').Append(pair.Value);
        return sb.Length == 0 ? "-" : sb.ToString();
    }
}
