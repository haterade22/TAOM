using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.Elephant;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Warg;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// The warg gait step of the command trial (#1563) against the stand-in: it passes on wargs that run when charged, and fails
/// with its own facts on wargs that stay slow. The game is the only proof of the speeds themselves.
/// </summary>
[TestClass]
public class CommandTrialWargStepsTests
{
    private const float Frame = 1f / 30f;

    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
    }

    private void Run()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose("enter,warg_gaits", out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _ => { });
        _world.Run(script, 600f, Frame);
        Assert.IsTrue(script.IsDone, "the run ended");
    }

    private string Line() => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=warg_gaits ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    [TestMethod]
    public void TheStep_OnWargsThatRun_PassesWithItsFacts()
    {
        _world.HorseChargeSpeed = 11f;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("12", Fact(line, "riders"));
        Assert.AreEqual("12", Fact(line, "mounted"));
        Assert.AreEqual("0.0", Fact(line, "stand"));
        Assert.AreEqual("9.0", Fact(line, "top"));
        Assert.AreEqual("13.0", Fact(line, "limit"));
        Assert.AreEqual("11.0", Fact(line, "horse"));
        Assert.AreEqual("1.10", Fact(line, "scale"));
    }

    [TestMethod]
    public void WargsThatStaySlow_Fail_WithTheirTopAndTheHorsesTop()
    {
        _world.WargChargeSpeed = 2.5f;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=FAIL", line);
        StringAssert.Contains(line, "top=2.5", line);
        StringAssert.Contains(line, "horse=13.0", line);
    }

    [TestMethod]
    public void ASpeedThatIsNoNumber_Fails()
    {
        _world.WargSpeedIsNoNumber = true;

        Run();

        StringAssert.Contains(Line(), "result=FAIL");
    }

    [TestMethod]
    public void OneReadingThatIsNoNumber_AmongGoodOnes_StillFails()
    {
        _world.WargSpeedIsNoNumberOnChargeRead = 20;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=FAIL", line);
        StringAssert.Contains(line, "top=NaN", line);
    }

    [TestMethod]
    public void ABattleWithNoWargRider_FailsWithItsReason()
    {
        _world.NoWargs = true;

        Run();

        StringAssert.Contains(Line(), "reason=no-wargs");
    }

    [TestMethod]
    public void TheStep_HoldsCharges_ThenHolds_AndReadsOnlyInTheMissionTick()
    {
        Run();

        var orders = _world.Writes.Where(w => w is "hold-all" or "charge-all").ToArray();
        CollectionAssert.AreEqual(new[] { "hold-all", "charge-all", "hold-all" }, orders);
        Assert.AreEqual(0, _world.WargReadsOutsideTheMissionTick);
        Assert.AreEqual(0, _world.WritesOutsideTheMissionTick.Count, string.Join(", ", _world.WritesOutsideTheMissionTick));
    }

    [TestMethod]
    public void TheWargStep_BesideAStepOtherThanEnter_IsRefused()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("enter,fly,warg_gaits", out _, out var error));
        StringAssert.Contains(error, "a warg step runs with 'enter' only");
        Assert.IsFalse(SeatTrialSteps.TryChoose("enter,spider_gaits,warg_gaits", out _, out _));
    }

    [TestMethod]
    public void TheWargStep_AddsTheWargRiders_AndFightsTheMeleeEnemy()
    {
        var army = SeatTrialRoster.PlayerFor("enter,warg_gaits");
        CollectionAssert.IsSubsetOf(WargTrialRows.Riders.ToList(), army.ToList());
        Assert.IsTrue(army.Any(g => g.Id == SeatTrialRoster.MountedTroop), "the horsemen stay as the control");
        CollectionAssert.AreEqual(CrewTrialRows.Enemy.ToList(), SeatTrialRoster.EnemyFor("enter,warg_gaits").ToList());
        Assert.IsFalse(SeatTrialRoster.PlayerFor("enter").Any(g => WargTrialRows.Riders.Contains(g)));
    }

    [TestMethod]
    public void AMissingWargRiderTroop_RefusesTheStart_OnlyWhenTheWargStepIsNamed()
    {
        bool AllButTheRider(string id) => id != "fow_mordor_warg_raider";

        CollectionAssert.Contains(SeatTrialRoster.Missing(_ => true, AllButTheRider, "enter,warg_gaits").ToList(), "character:fow_mordor_warg_raider");
        Assert.AreEqual(0, SeatTrialRoster.Missing(_ => true, AllButTheRider, "enter").Count);
    }

    [TestMethod]
    public void ARunWithNoStepList_ArmsNoWargStep()
    {
        Assert.IsFalse(SeatTrialSteps.All.Any(s => s.Name == WargTrialRows.Gaits));
        Assert.IsTrue(SeatTrialSteps.Known.Any(s => s.Name == WargTrialRows.Gaits));
    }
}
