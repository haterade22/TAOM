using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The field trial's process state (build-spec section 12): the script a mission gets, and every reason it gets none.</summary>
[TestClass]
public class CommandTrialRunTests
{
    private ListLogger _log = null!;
    private CommandTrialWorld _world = null!;
    private int _hostsBuilt;

    [TestInitialize]
    public void Setup()
    {
        CommandTrialRun.Reset();
        _log = new ListLogger();
        _world = new CommandTrialWorld();
        _hostsBuilt = 0;
    }

    [TestCleanup]
    public void Cleanup() => CommandTrialRun.Reset();

    private static void Arm(params string[] lines) =>
        CommandTrialRun.Arm(CommandTrialOptions.Parse("arm.txt", _ => true, _ => lines, new ListLogger())!);

    private CommandTrialScript? Script() => CommandTrialRun.Script(Host, _log);

    private ICommandTrialHost Host()
    {
        _hostsBuilt++;
        return _world;
    }

    [TestMethod]
    public void Settings_InAnUnarmedSession_IsThePlainProviderItself()
    {
        var plain = new FakeSeatSettings { Enabled = false, BeginBattlesInSeat = true };

        var given = CommandTrialRun.Settings(plain);

        Assert.AreSame(plain, given, "a player's session gets the saved values, not the trial's");
        Assert.IsFalse(given.Enabled);
        Assert.IsTrue(given.BeginBattlesInSeat);
    }

    [TestMethod]
    public void Settings_WhenArmed_AreTheTrialsOwn_WhateverThePlayerSaved()
    {
        Arm("out=C:\\runs");
        var plain = new FakeSeatSettings { Enabled = false, BeginBattlesInSeat = true, CursorWithPanel = false, KeepPanelOpen = false };

        var given = CommandTrialRun.Settings(plain);

        Assert.IsInstanceOfType(given, typeof(CommandTrialSettings));
        Assert.IsTrue(given.Enabled, "the Seat is on");
        Assert.IsFalse(given.BeginBattlesInSeat, "a saved Begin Battles in the Seat would turn the script's press into a leave");
        Assert.IsTrue(given.CursorWithPanel);
        Assert.IsTrue(given.KeepPanelOpen);
        Assert.AreEqual(0, plain.Reads, "nothing is read from, and nothing is written to, the saved values");
    }

    [TestMethod]
    public void Script_InAnUnarmedSession_IsNull_BuildsNoHost_AndWritesNothing()
    {
        Assert.IsNull(Script());

        Assert.AreEqual(0, _hostsBuilt);
        Assert.AreEqual(0, _log.Lines.Count);
        Assert.IsNull(CommandTrialRun.Outcome);
    }

    [TestMethod]
    public void Script_WhenArmed_BuildsOneHostAndOneScript_AndNamesTheStepsItWillRun()
    {
        Arm("out=E:/trial-out");

        var script = Script();

        Assert.IsNotNull(script);
        Assert.AreEqual(1, _hostsBuilt);
        StringAssert.Contains(_log.Lines.Single(), "script armed: steps=" + string.Join(",", SeatTrialSteps.All.Select(s => s.Name)));
        Assert.IsNull(CommandTrialRun.Outcome);
    }

    [TestMethod]
    public void Script_WhenArmedWithASubset_RunsOnlyThoseSteps()
    {
        Arm("out=E:/trial-out", "steps=enter,fly");

        Assert.IsNotNull(Script());

        StringAssert.EndsWith(_log.Lines.Single(), "steps=enter,fly");
    }

    [TestMethod]
    public void Script_ForALaterMissionAfterTheRunEnded_IsNull_BuildsNoHost_WhileTheRunningOneGetsAScript()
    {
        Arm("out=E:/trial-out");
        Assert.IsNotNull(Script(), "armed and still running");

        CommandTrialRun.Finish("final");

        Assert.IsNull(Script(), "the run has ended: a later mission is the player's");
        Assert.AreEqual(1, _hostsBuilt);
    }

    [TestMethod]
    public void Script_ForAStepListTheGameRefuses_EndsTheRunAsStartRefused_NamingTheStep()
    {
        Arm("out=E:/trial-out", "steps=enter,flye");

        Assert.IsNull(Script());

        Assert.AreEqual("start_refused", CommandTrialRun.Outcome);
        Assert.AreEqual(0, _hostsBuilt, "no host is built for a run that cannot start");
        StringAssert.Contains(_log.Errors.Single(), "flye");
    }

    [TestMethod]
    public void Script_WhenTheHostCannotBeBuilt_EndsTheRunAsScriptFailed_InsteadOfThrowingIntoTheMission()
    {
        Arm("out=E:/trial-out");

        var script = CommandTrialRun.Script(() => throw new InvalidOperationException("no screen"), _log);

        Assert.IsNull(script);
        Assert.AreEqual("script_failed", CommandTrialRun.Outcome);
        StringAssert.Contains(_log.Errors.Single(), "no screen");
    }

    [TestMethod]
    public void Script_WhenARefusalCameFirst_KeepsThatOutcome()
    {
        Arm("out=E:/trial-out");
        CommandTrialRun.Finish("never_live");

        Assert.IsNull(Script());

        Assert.AreEqual("never_live", CommandTrialRun.Outcome);
    }

    [TestMethod]
    public void TheScript_ItBuilds_ReportsEachStepAndFinalToTheRun()
    {
        Arm("out=E:/trial-out", "steps=refuse_in_deployment");
        var script = Script()!;

        _world.Run(script, 20f);

        Assert.AreEqual("final", CommandTrialRun.Outcome);
        Assert.AreEqual("refuse_in_deployment", CommandTrialRun.Step);
    }

    [TestMethod]
    public void Arm_ThenReset_IsUnarmedAgain()
    {
        Arm("out=E:/trial-out");
        Assert.IsTrue(CommandTrialRun.IsArmed);

        CommandTrialRun.Reset();

        Assert.IsFalse(CommandTrialRun.IsArmed);
        Assert.IsNull(CommandTrialRun.Options);
        Assert.AreEqual("none", CommandTrialRun.Step);
    }
}
