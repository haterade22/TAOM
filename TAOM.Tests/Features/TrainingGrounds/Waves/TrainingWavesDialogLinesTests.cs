using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrainingGrounds.Waves.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>
/// The conversation graph of the wave fight, pinned from the source: a mistyped token ends a conversation with no error, so each line's
/// id, kind, input, output, priority and text are read back and compared with the design (docs/features/training-waves.md).
/// </summary>
[TestClass]
public class TrainingWavesDialogLinesTests
{
    private const string Path = "Main/Features/TrainingGrounds/Waves/Hooks/TrainingWavesDialogLines.cs";

    /// <summary>Vanilla's priority for the practice master's hub options and its start lines (AMCB: hub 100, leave 80, start lines the default 100).</summary>
    private const int VanillaPriority = 100;

    private sealed record Line(string Kind, string Id, string In, string Out, string Text, int Priority);

    private static readonly Line[] Design =
    {
        new("Player", "taom_tgw_offer", "arena_master_talk", "taom_tgw_rules", "Offer", 101),
        new("Dialog", "taom_tgw_rules", "taom_tgw_rules", "taom_tgw_stake", "Rules", 101),
        new("Player", "taom_tgw_stake_low", "taom_tgw_stake", "taom_tgw_go", "StakeLow", 104),
        new("Player", "taom_tgw_stake_mid", "taom_tgw_stake", "taom_tgw_go", "StakeMid", 103),
        new("Player", "taom_tgw_stake_high", "taom_tgw_stake", "taom_tgw_go", "StakeHigh", 102),
        new("Player", "taom_tgw_not_today", "taom_tgw_stake", "arena_master_pre_talk", "NotToday", 101),
        new("Player", "taom_tgw_withdraw", "taom_tgw_stake", "taom_tgw_withdrawn", "Withdraw", 101),
        new("Dialog", "taom_tgw_go", "taom_tgw_go", "close_window", "Go", 102),
        new("Dialog", "taom_tgw_go_failed", "taom_tgw_go", "arena_master_talk", "GoFailed", 101),
        new("Dialog", "taom_tgw_withdrawn", "taom_tgw_withdrawn", "close_window", "Withdrawn", 101),
        new("Dialog", "taom_tgw_cleared", "start", "taom_tgw_stake", "Cleared", 120),
        new("Dialog", "taom_tgw_lost", "start", "arena_master_talk", "Lost", 120),
        new("Dialog", "taom_tgw_done", "start", "arena_master_talk", "Done", 120),
    };

    private static string Source() => RepoPaths.ReadSource(Path, stripComments: true);

    private static Dictionary<string, string> Constants(string src)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(src, @"const\s+(?:string|int)\s+(\w+)\s*=\s*(""[^""]*""|\d+)\s*;"))
            map[m.Groups[1].Value] = m.Groups[2].Value.Trim('"');
        return map;
    }

    /// <summary>Every <c>s.AddPlayerLine(</c> and <c>s.AddDialogLine(</c> call, its arguments split at the top level.</summary>
    private static List<(string Kind, List<string> Args)> Calls(string src)
    {
        var calls = new List<(string, List<string>)>();
        foreach (Match m in Regex.Matches(src, @"\.Add(Player|Dialog)Line\("))
        {
            var args = new List<string>();
            var depth = 1;
            var inString = false;
            var current = new StringBuilder();
            for (var i = m.Index + m.Length; i < src.Length && depth > 0; i++)
            {
                var c = src[i];
                if (inString)
                {
                    current.Append(c);
                    if (c == '"' && src[i - 1] != '\\')
                        inString = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '(' || c == '{' || c == '[')
                    depth++;
                else if (c == ')' || c == '}' || c == ']')
                    depth--;

                if (depth == 0)
                    break;
                if (c == ',' && depth == 1)
                {
                    args.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            args.Add(current.ToString().Trim());
            calls.Add((m.Groups[1].Value, args));
        }

        return calls;
    }

    private static string Resolve(string arg, Dictionary<string, string> constants) =>
        constants.TryGetValue(arg, out var value) ? value : arg.Trim('"');

    private static List<Line> Parsed()
    {
        var src = Source();
        var constants = Constants(src);
        return Calls(src).Select(c => new Line(c.Kind, Resolve(c.Args[0], constants), Resolve(c.Args[1], constants), Resolve(c.Args[2], constants),
            c.Args[3].Replace("TrainingWavesTexts.", ""), int.Parse(Resolve(c.Args[6], constants)))).ToList();
    }

    [TestMethod]
    public void TheGraph_IsExactlyTheDesign_IdKindTokensTextAndPriority()
    {
        var parsed = Parsed();

        Assert.AreEqual(Design.Length, parsed.Count, "a line was added or lost");
        foreach (var expected in Design)
        {
            var found = parsed.Where(p => p.Id == expected.Id).ToList();
            Assert.AreEqual(1, found.Count, expected.Id + " must be registered exactly once");
            Assert.AreEqual(expected, found[0]);
        }
    }

    [TestMethod]
    public void EveryLineOfTheHub_SitsAboveVanillasHubAndLeaveLines()
    {
        foreach (var line in Parsed().Where(p => p.In == "arena_master_talk"))
            Assert.IsTrue(line.Priority > VanillaPriority, line.Id);
    }

    [TestMethod]
    public void EveryStartLine_SitsAboveVanillasStartLines_SoThePracticePrizeNeverPaysAfterAWave()
    {
        var starts = Parsed().Where(p => p.In == "start").ToList();

        Assert.AreEqual(3, starts.Count);
        foreach (var line in starts)
            Assert.IsTrue(line.Priority > VanillaPriority, line.Id);
        Assert.IsTrue(TrainingWavesDialogLines.DebriefPriority > VanillaPriority);
    }

    [TestMethod]
    public void TheStakeLines_ListTheSmallestFirst()
    {
        var parsed = Parsed();
        int Priority(string id) => parsed.Single(p => p.Id == id).Priority;

        Assert.IsTrue(Priority("taom_tgw_stake_low") > Priority("taom_tgw_stake_mid"));
        Assert.IsTrue(Priority("taom_tgw_stake_mid") > Priority("taom_tgw_stake_high"));
    }

    [TestMethod]
    public void EveryStakeLine_ShowsAndPlacesTheSameSlot_AndEachSlotSetsItsOwnAmountVariable()
    {
        var calls = Calls(Source());
        foreach (var (id, slot) in new[] { ("taom_tgw_stake_low", 2), ("taom_tgw_stake_mid", 1), ("taom_tgw_stake_high", 0) })
        {
            var args = calls.Single(c => c.Args[0].Trim('"') == id).Args;
            Assert.AreEqual($"() => b.CanStakeLine({slot})", args[4], id);
            Assert.AreEqual($"() => b.Stake({slot})", args[5], id);
            Assert.AreEqual($"(out TextObject why) => b.StakeClickable({slot}, out why)", args[7], id);
        }

        var texts = RepoPaths.ReadSource("Main/Features/TrainingGrounds/Waves/Hooks/TrainingWavesTexts.cs", stripComments: true);
        StringAssert.Contains(texts, "0 => StakeHighAmount");
        StringAssert.Contains(texts, "1 => StakeMidAmount");
        StringAssert.Contains(texts, "_ => StakeLowAmount");
    }

    [TestMethod]
    public void ThePlayerLines_AllCarryAClickableCondition_OnlyWhereTheDesignGreysThem()
    {
        var src = Source();
        var withClickable = Calls(src).Where(c => c.Kind == "Player" && c.Args.Count >= 8).Select(c => c.Args[0].Trim('"')).ToList();

        CollectionAssert.AreEquivalent(new[] { "taom_tgw_offer", "taom_tgw_stake_low", "taom_tgw_stake_mid", "taom_tgw_stake_high" }, withClickable.Select(i => i).ToList());
    }
}
