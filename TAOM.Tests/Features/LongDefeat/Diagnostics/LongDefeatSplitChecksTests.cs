using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;
using TAOM.Tests.Infrastructure;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

[TestClass]
public class LongDefeatSplitChecksTests
{
    private static readonly int[] Close = { 1, 0, 0 };
    private static readonly LdSplitRelease Release = new(4, 0);

    private static LdSplitGroup[] Groups(float wallMedian = 6f, int wallCount = 21, float lowMedian = 30f) => new[]
    {
        new LdSplitGroup("wall", wallCount, wallMedian, 18, false),
        new LdSplitGroup("gate", 9, 4f, 0, false),
        new LdSplitGroup("low", 7, lowMedian, 2, true),
        new LdSplitGroup("high", 3, 8f, 3, true),
    };

    [TestMethod]
    public void Stand_EveryGroupInReach_Passes_AndTheArchersHaveTheWiderReach()
    {
        var v = LongDefeatSplitChecks.Stand(1, Groups(lowMedian: LongDefeatSplitChecks.ArcherReach), 30f, Release, Close);

        Assert.AreEqual(CheckResult.Pass, v.Result, v.Detail);
        StringAssert.Contains(v.Detail, "low=7 low_median=40.0 low_high=2");
        StringAssert.StartsWith(v.Detail, "w=1 wall=21 wall_median=6.0 wall_high=18 gate=9 ");
        StringAssert.EndsWith(v.Detail, "held_s=30.0 released=4 kept=0 close=1,0,0");
    }

    [TestMethod]
    public void Stand_NoGroupStillHeld_IsAnEngineTookSkip()
    {
        var v = LongDefeatSplitChecks.Stand(1, Groups(wallMedian: 90f), 30f, new LdSplitRelease(0, 0), Close);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        StringAssert.EndsWith(v.Detail, "reason=engine-took");
    }

    [TestMethod]
    public void ThePassLine_AsTheLogWritesIt_MatchesThePassPatternOfTheManifestRow()
    {
        // The game's transform (OutcomeLog.SanitiseDetail) and the text the verdict tool reads: a renamed key fails here instead of grading every real pass NOT_REACHED.
        var toml = RepoPaths.ReadSource("tools/ingame_checks/1079.toml");
        var row = toml.Substring(toml.IndexOf("id = \"taom.long-defeat.split\"", StringComparison.Ordinal));
        var pattern = Regex.Match(row, @"^pass = \['(.*)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.IsTrue(pattern.Length > 0, "the split row has no pass pattern");

        var verdict = LongDefeatSplitChecks.Stand(1, Groups(), 30f, Release, Close);

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        var line = "[Check] id=taom.long-defeat.split result=pass " + OutcomeLog.SanitiseDetail(verdict.Detail);
        Assert.IsTrue(Regex.IsMatch(line, pattern), line);
    }

    [TestMethod]
    public void Stand_AnArcherGroupPastItsReach_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatSplitChecks.Stand(1, Groups(lowMedian: LongDefeatSplitChecks.ArcherReach + 0.1f), 30f, Release, Close).Result);
    }

    [TestMethod]
    public void Stand_AFootGroupPastItsReach_Fails()
    {
        var v = LongDefeatSplitChecks.Stand(1, Groups(wallMedian: 30f), 30f, Release, Close);

        Assert.AreEqual(CheckResult.Fail, v.Result);
    }

    [TestMethod]
    public void Stand_AnEmptyGroup_OrANaNMedian_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatSplitChecks.Stand(1, Groups(wallCount: 0), 30f, Release, Close).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatSplitChecks.Stand(1, Groups(wallMedian: float.NaN), 30f, Release, Close).Result);
    }

    [TestMethod]
    public void Stand_AShortOrNaNHold_IsASkip()
    {
        Assert.AreEqual(CheckResult.Skip, LongDefeatSplitChecks.Stand(1, Groups(), LongDefeatSplitChecks.MinHoldSeconds - 0.1f, Release, Close).Result);
        StringAssert.EndsWith(LongDefeatSplitChecks.Stand(1, Groups(), float.NaN, Release, Close).Detail, "reason=short-hold");
    }

    [TestMethod]
    public void Stand_NoGroups_IsASkip()
    {
        var v = LongDefeatSplitChecks.Stand(1, Array.Empty<LdSplitGroup>(), 30f, Release, Close);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        StringAssert.EndsWith(v.Detail, "reason=no-groups");
    }

    [TestMethod]
    public void NoPlaces_Fails_WithOneWordProblem()
    {
        var v = LongDefeatSplitChecks.NoPlaces(1, "no gate place");

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("w=1 reason=no-places problem=no_gate_place", v.Detail);
    }
}
