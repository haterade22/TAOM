using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The field trial's two panel click steps (Marshal's Word stage M7), against the scripted stand-in.</summary>
[TestClass]
public class CommandTrialPanelClickStepsTests
{
    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
    }

    private void Run(params string[] steps)
    {
        var list = "enter," + string.Join(",", steps);
        Assert.IsTrue(SeatTrialSteps.TryChoose(list, out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _ => { });
        _world.Run(script, 150f);
        Assert.IsTrue(script.IsDone, "the run ended");
    }

    private string Line(string step) => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    private void AssertFails(string step, params string[] mentions)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        foreach (var mention in mentions)
            StringAssert.Contains(line, mention, line);
    }

    [TestMethod]
    public void TheRows_FollowTheRiseSteps_BeforeTheKillSteps_AndEachNeedsTheEnterStep()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(new[] { "panel_click_card", "panel_click_order", "hero_falls" }, names.Skip(names.IndexOf("sink_on_close") + 1).Take(3).ToArray());
        foreach (var name in new[] { "panel_click_card", "panel_click_order" })
        {
            Assert.IsFalse(SeatTrialSteps.TryChoose(name, out _, out var error), name);
            StringAssert.Contains(error, "enter");
        }
    }

    // ---- panel_click_card ----

    [TestMethod]
    public void PanelClickCard_ClearsTheSelection_ClicksTheCard_AndFindsOnlyItsCompanySelected()
    {
        Run("panel_click_card");

        var line = Line("panel_click_card");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("Acted", Fact(line, "clicked"));
        Assert.AreEqual("1", Fact(line, "only_selected"));
        Assert.AreEqual(1, _world.Injections);
        CollectionAssert.Contains(_world.Writes, "clear-selected");
        Assert.IsFalse(_world.WritesOutsideTheMissionTick.Contains("inject-panel-click"), "a formation write is made in the mission tick");
        Assert.IsFalse(_world.PanelOpen, "the step closes the panel it opened");
    }

    [TestMethod]
    public void PanelClickCard_WhenThePanelShowsNoCard_FailsWithTheReason_AndClicksNothing()
    {
        _world.PanelHasCard = false;

        Run("panel_click_card");

        AssertFails("panel_click_card", "reason=no-card", "not-found");
        Assert.AreEqual(0, _world.Injections);
        Assert.IsFalse(_world.PanelOpen);
    }

    [TestMethod]
    public void PanelClickCard_WhenTheClickDoesNothing_Fails()
    {
        _world.CardClickDoesNothing = true;

        Run("panel_click_card");

        AssertFails("panel_click_card", "clicked=Idle", "only_selected=0");
    }

    [TestMethod]
    public void PanelClickCard_WhenMoreThanTheCardsCompanyIsSelected_Fails()
    {
        _world.CardClickSelectsTwo = true;

        Run("panel_click_card");

        AssertFails("panel_click_card", "clicked=Acted", "only_selected=0");
    }

    [TestMethod]
    public void PanelClickCard_WhenTheSeatFaultsDuringTheStep_Fails()
    {
        _world.FaultMidStep = true;

        Run("panel_click_card");

        AssertFails("panel_click_card", "faulted=1");
    }

    // ---- panel_click_order ----

    [TestMethod]
    public void PanelClickOrder_ClicksTheSet_ThenAnOrder_AndTheEngineRunsIt()
    {
        Run("panel_click_order");

        var line = Line("panel_click_order");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("Acted", Fact(line, "set"));
        Assert.AreEqual("1", Fact(line, "sub_menu"));
        Assert.AreEqual("Acted", Fact(line, "order"));
        Assert.AreEqual("1", Fact(line, "orders_seen"));
        Assert.AreEqual(2, _world.Injections);
        CollectionAssert.Contains(_world.Writes, "select-company");
        Assert.IsFalse(_world.PanelOpen);
    }

    [TestMethod]
    public void PanelClickOrder_WhenThePanelShowsNoSet_FailsAndClicksNothing()
    {
        _world.PanelHasSet = false;

        Run("panel_click_order");

        AssertFails("panel_click_order", "set=Idle", "sub_menu=0");
        Assert.AreEqual(0, _world.Injections);
    }

    [TestMethod]
    public void PanelClickOrder_WhenTheSetClickDoesNothing_FailsBeforeTheOrderClick()
    {
        _world.SetClickDoesNothing = true;

        Run("panel_click_order");

        AssertFails("panel_click_order", "set=Idle", "sub_menu=0", "no-sub-menu");
        Assert.AreEqual(1, _world.Injections);
    }

    [TestMethod]
    public void PanelClickOrder_WhenTheSubMenuShowsNoOrder_Fails()
    {
        _world.PanelSetOpensItems = false;

        Run("panel_click_order");

        AssertFails("panel_click_order", "order=Idle");
    }

    [TestMethod]
    public void PanelClickOrder_WhenTheClickReachesNoViewModelOrder_Fails()
    {
        _world.OrderClickRunsNoOrder = true;

        Run("panel_click_order");

        AssertFails("panel_click_order", "order=Acted", "orders_seen=0");
    }

    [TestMethod]
    public void PanelClickOrder_WithNoCompany_FailsWithTheReason()
    {
        _world.NoCompany = true;

        Run("panel_click_order");

        AssertFails("panel_click_order", "reason=no-company");
    }
}
