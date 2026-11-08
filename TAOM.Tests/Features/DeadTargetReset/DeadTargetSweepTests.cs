using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.DeadTargetReset;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.DeadTargetReset;

/// <summary>
/// #1096: the engine deletes a faded corpse 30 s after its fade starts and sets every soldier's target on it to -1, but leaves the
/// soldier's "target is valid" flag, and the next AI tick reads agent slot -1 (TaleWorlds.Native 0x6B716E). The sweep resets a dead
/// target long before that delete.
/// </summary>
[TestClass]
public class DeadTargetSweepTests
{
    [DataTestMethod]
    [DataRow(AgentState.Killed)]
    [DataRow(AgentState.Unconscious)]
    public void ADeadOrUnconsciousTarget_IsStale(AgentState state)
    {
        Assert.IsTrue(DeadTargetSweep.IsStale(state));
    }

    [DataTestMethod]
    [DataRow(AgentState.Active)]
    [DataRow(AgentState.Routed)]
    [DataRow(AgentState.Deleted)]
    [DataRow(AgentState.None)]
    public void ALiveRoutedOrDeletedTarget_IsLeftAlone(AgentState state)
    {
        // Routed: a routed agent has left the battle, and the engine's rout already reset every soldier that targeted it.
        Assert.IsFalse(DeadTargetSweep.IsStale(state));
    }

    [TestMethod]
    public void TheSweep_IsDueOnceASecond()
    {
        var sweep = new DeadTargetSweep();

        Assert.IsTrue(sweep.Due(0.016f), "the first tick sweeps, so a mission that starts mid-fight is covered at once");
        Assert.IsFalse(sweep.Due(0.5f));
        Assert.IsFalse(sweep.Due(0.49f));
        Assert.IsTrue(sweep.Due(0.02f));
        Assert.IsFalse(sweep.Due(0.016f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    [DataRow(0f)]
    public void ABadOrEmptyFrame_DoesNotMoveTheClock(float dt)
    {
        var sweep = new DeadTargetSweep();
        Assert.IsTrue(sweep.Due(0.016f));

        Assert.IsFalse(sweep.Due(dt));
        Assert.IsFalse(sweep.Due(0.9f), "the bad frame added nothing to the clock");
    }

    [TestMethod]
    public void TheMissionEndLine_CountsSweepsAndResets()
    {
        var sweep = new DeadTargetSweep();
        sweep.Due(0.016f);
        sweep.Record(3);
        sweep.Due(1f);
        sweep.Record(0);

        Assert.AreEqual("[DeadTarget] mission end: sweeps=2 reset=3", sweep.Summary());
    }

    // tools/ingame_checks/1096.toml reads this line from the game log: a format change here must not leave the check unable to pass.
    [TestMethod]
    public void TheInGameCheck_PassesOnASweepThatReset_AndOnlyThen()
    {
        var pass = new Regex(TomlPattern("pass"));
        var reached = new Regex(TomlPattern("reached"));
        var reset = new DeadTargetSweep();
        reset.Due(0.016f);
        reset.Record(2);
        var idle = new DeadTargetSweep();
        idle.Due(0.016f);

        Assert.IsTrue(pass.IsMatch(reset.Summary()), reset.Summary());
        Assert.IsTrue(reached.IsMatch(idle.Summary()), idle.Summary());
        Assert.IsFalse(pass.IsMatch(idle.Summary()), "a mission with no dead target reset is reached, not passed");
    }

    private static string TomlPattern(string key)
    {
        var line = File.ReadAllLines(RepoPaths.RepoPath("tools", "ingame_checks", "1096.toml"))
            .Single(l => l.StartsWith(key + " = ['"));
        var start = line.IndexOf("['") + 2;
        return line.Substring(start, line.LastIndexOf("']") - start);
    }
}
