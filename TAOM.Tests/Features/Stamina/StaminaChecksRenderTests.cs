using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Stamina;
using TAOM.Tests.Features.LoadTimeStamps;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Stamina;

/// <summary>
/// The [Check] lines stamina writes, judged by the patterns of tools/ingame_checks/1497.toml. The lines come from a real
/// <see cref="OutcomeLog"/>, so they are the ones the game writes: a pattern that drifts from <see cref="StaminaChecks"/>
/// would make every run read NOT REACHED, and no other test would see it (lessons/testing-qa.md, "A verdict manifest
/// needs a test that renders the real line and judges it").
/// </summary>
[TestClass]
public class StaminaChecksRenderTests
{
    private const string Manifest = "1497.toml";

    private IModLogger _logger = null!;
    private OutcomeLog _log = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _log = new OutcomeLog(_logger, new FakeStampClock(), () => 0);
    }

    private string[] CheckLines() =>
        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => c.GetArguments()[0]?.ToString() ?? string.Empty)
            .Where(l => l.StartsWith("[Check] ", StringComparison.Ordinal))
            .ToArray();

    private string OnlyLine()
    {
        var lines = CheckLines();
        Assert.AreEqual(1, lines.Length, string.Join("\n", lines));
        return lines[0];
    }

    // The patterns of one [[check]] block: reached, pass and fail, each a list of single-quoted regular expressions.
    private static Dictionary<string, Regex[]> Patterns(string id)
    {
        var lines = File.ReadAllLines(RepoPath("tools", "ingame_checks", Manifest));
        var start = Array.FindIndex(lines, l => l.Trim() == "id = \"" + id + "\"");
        Assert.IsTrue(start >= 0, Manifest + " has no check " + id);
        var block = lines.Skip(start + 1).TakeWhile(l => !l.StartsWith("[[", StringComparison.Ordinal)).ToArray();
        var found = new Dictionary<string, Regex[]>(StringComparer.Ordinal);
        foreach (var name in new[] { "reached", "pass", "fail" })
        {
            var line = block.SingleOrDefault(l => l.StartsWith(name + " = [", StringComparison.Ordinal));
            Assert.IsNotNull(line, id + " has no " + name + " list");
            found[name] = Regex.Matches(line!, "'([^']*)'").Cast<Match>().Select(m => new Regex(m.Groups[1].Value)).ToArray();
            Assert.IsTrue(found[name].Length > 0, id + ": the " + name + " list is empty");
        }
        return found;
    }

    private static bool Any(Regex[] patterns, string line) => patterns.Any(p => p.IsMatch(line));

    private static void AssertReadsAs(string id, string line, string verdict)
    {
        var patterns = Patterns(id);
        Assert.AreEqual(verdict != "skip", Any(patterns["reached"], line), "reached: " + line);
        Assert.AreEqual(verdict == "pass", Any(patterns["pass"], line), "pass: " + line);
        Assert.AreEqual(verdict == "fail", Any(patterns["fail"], line), "fail: " + line);
    }

    [TestMethod]
    public void CheckIds_AreValid_AndTheManifestNamesEachOfThem()
    {
        var toml = File.ReadAllText(RepoPath("tools", "ingame_checks", Manifest));
        foreach (var id in new[] { StaminaChecks.ArmedId, StaminaChecks.LockId, StaminaChecks.RefreshId })
        {
            Assert.IsTrue(OutcomeLog.IsValidId(id), id);
            StringAssert.Contains(toml, "id = \"" + id + "\"");
        }
    }

    [TestMethod]
    public void Armed_TheRenderedPass_IsThePassOfTheManifest()
    {
        StaminaChecks.Armed(_log, "m1", enabled: true, armed: true);

        var line = OnlyLine();
        Assert.AreEqual("[Check] id=taom.stamina.armed result=pass armed=true", line);
        AssertReadsAs(StaminaChecks.ArmedId, line, "pass");
    }

    [DataTestMethod]
    [DataRow(false, "[Check] id=taom.stamina.armed result=skip reason=toggle-off")]
    [DataRow(true, "[Check] id=taom.stamina.armed result=skip reason=gate-closed")]
    public void Armed_ARenderedSkip_LeavesTheCheckNotReached(bool enabled, string expected)
    {
        StaminaChecks.Armed(_log, "m1", enabled, armed: false);

        var line = OnlyLine();
        Assert.AreEqual(expected, line);
        AssertReadsAs(StaminaChecks.ArmedId, line, "skip");
    }

    [TestMethod]
    public void Lock_TheRenderedPassAndFail_AreThePassAndFailOfTheManifest()
    {
        StaminaChecks.Lock(_log, "m1", exhaustions: 12, recoveries: 9, stuck: 0);
        StaminaChecks.Lock(_log, "m2", exhaustions: 12, recoveries: 9, stuck: 2);

        var lines = CheckLines();
        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.stamina.lock result=pass entered=12 recovered=9 stuck=0",
            "[Check] id=taom.stamina.lock result=fail entered=12 recovered=9 stuck=2",
        }, lines);
        AssertReadsAs(StaminaChecks.LockId, lines[0], "pass");
        AssertReadsAs(StaminaChecks.LockId, lines[1], "fail");
    }

    [DataTestMethod]
    [DataRow(0, 0, "[Check] id=taom.stamina.lock result=skip reason=none-exhausted entered=0 recovered=0 stuck=0")]
    [DataRow(3, 0, "[Check] id=taom.stamina.lock result=skip reason=none-recovered entered=3 recovered=0 stuck=0")]
    public void Lock_ARenderedSkip_LeavesTheCheckNotReached(int exhaustions, int recoveries, string expected)
    {
        StaminaChecks.Lock(_log, "m1", exhaustions, recoveries, stuck: 0);

        var line = OnlyLine();
        Assert.AreEqual(expected, line);
        AssertReadsAs(StaminaChecks.LockId, line, "skip");
    }

    [TestMethod]
    public void Refresh_TheRenderedPassFailAndSkip_ReadAsTheManifestSays()
    {
        StaminaChecks.Refresh(_log, "m1", refreshes: 400, slowestBatchMs: 0.31);
        StaminaChecks.Refresh(_log, "m2", refreshes: 400, slowestBatchMs: 1.5);
        StaminaChecks.Refresh(_log, "m3", refreshes: 0, slowestBatchMs: 0);

        var lines = CheckLines();
        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.stamina.refresh result=pass refreshes=400 slowest_batch_ms=0.310",
            "[Check] id=taom.stamina.refresh result=fail refreshes=400 slowest_batch_ms=1.500",
            "[Check] id=taom.stamina.refresh result=skip reason=no-refresh refreshes=0 slowest_batch_ms=0.000",
        }, lines);
        AssertReadsAs(StaminaChecks.RefreshId, lines[0], "pass");
        AssertReadsAs(StaminaChecks.RefreshId, lines[1], "fail");
        AssertReadsAs(StaminaChecks.RefreshId, lines[2], "skip");
    }

    [TestMethod]
    public void EachMission_WritesItsOwnLine_BecauseTheKeyIsTheMissionSerial()
    {
        // The real log writes one line per (id, result, key): with one key for the whole process, the second battle would write nothing.
        StaminaChecks.Armed(_log, "m1", enabled: true, armed: true);
        StaminaChecks.Armed(_log, "m1", enabled: true, armed: true);
        StaminaChecks.Armed(_log, "m2", enabled: true, armed: true);

        Assert.AreEqual(2, CheckLines().Length);
    }

    [DataTestMethod]
    [DataRow(StaminaChecks.ArmedId, "[Stamina] OnMissionTick error", "ERROR")]
    [DataRow(StaminaChecks.LockId, "[Stamina] the mission end failed", "ERROR")]
    public void TheFeaturesOwnFailureLines_AreInTheCode_AndFailTheirCheck(string id, string prefix, string level)
    {
        StringAssert.Contains(ReadSource("Main/Features/Stamina/Hooks/StaminaMissionLogic.cs", stripComments: true), prefix);

        var line = "[12:00:00.000] [" + level + "] " + prefix + ": NullReferenceException: boom";
        Assert.IsTrue(Any(Patterns(id)["fail"], line), id + " does not fail on: " + line);
    }
}
