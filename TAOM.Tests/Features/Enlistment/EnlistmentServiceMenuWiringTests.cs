using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Source pins for the service wait menu (#567, #828). GameMenu.GetMenuOptionConditionsHold ANDs a
/// wait menu's condition delegate with EVERY option's own condition, so a condition that turns
/// false (a discharge during load) hides every option and nothing exits the menu: a softlock that
/// persists into the save. The condition is `args => true` and each option gates itself.
/// </summary>
[TestClass]
public class EnlistmentServiceMenuWiringTests
{
    private const string BehaviorPath = "Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs";
    private const string OptionsPath = "Main/Features/Enlistment/Presentation/EnlistmentWaitMenuOptions.cs";
    private const string DischargedExitId = "taom_enlist_menu_discharged_leave";

    private static string[] OptionBlocks()
    {
        var src = RepoPaths.ReadSource(OptionsPath, stripComments: true);
        // Segment 0 is the text before the first registration.
        var blocks = src.Split(new[] { "starter.AddGameMenuOption(" }, StringSplitOptions.None);
        return blocks.Skip(1).ToArray();
    }

    [TestMethod]
    public void ServiceWaitMenu_WaitCondition_IsExactlyTrue()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        int start = src.IndexOf("starter.AddWaitGameMenu(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the service wait menu is no longer registered with AddWaitGameMenu");
        int end = src.IndexOf(");", start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, "could not find the end of the AddWaitGameMenu call");
        var call = src.Substring(start, end - start);

        StringAssert.Contains(call, "args => true,",
            "the wait-menu condition must be exactly `args => true`; anything else hides every option "
            + "the moment it turns false");
        Assert.IsFalse(call.Contains("IsEnlisted"),
            "the wait-menu condition gates on IsEnlisted again; a discharge during load leaves the "
            + "saved menu up with every option hidden and no exit (#828)");
    }

    [TestMethod]
    public void ServiceWaitMenuOptions_EveryServiceOption_GatesItselfOnServing()
    {
        var blocks = OptionBlocks();
        Assert.IsTrue(blocks.Length >= 6, "expected the five service options plus the discharged exit");

        var failures = blocks
            .Where(b => !b.Contains(DischargedExitId))
            .Where(b => !b.Contains("_actions.IsServing"))
            .Select(b => b.Substring(0, Math.Min(80, b.Length)).Replace("\n", " "))
            .ToList();

        var inverted = blocks
            .Where(b => !b.Contains(DischargedExitId))
            .Where(b => b.Contains("!_actions.IsServing"))
            .Count();
        Assert.AreEqual(0, inverted, "a service option gates on the INVERTED serving check");

        Assert.AreEqual(0, failures.Count,
            "service options that no longer gate themselves on _actions.IsServing (they would show "
            + "beside the discharged exit): " + string.Join(" | ", failures));
    }

    [TestMethod]
    public void ServiceWaitMenuOptions_DischargedExit_IsLeaveAndNeverDisabled()
    {
        var exit = OptionBlocks().SingleOrDefault(b => b.Contains(DischargedExitId));
        Assert.IsNotNull(exit, "the discharged exit option is not registered");

        StringAssert.Contains(exit, "!_actions.IsServing",
            "the discharged exit must show only while nobody is serving");
        StringAssert.Contains(exit, "_ => _actions.LeaveAfterDischarge()",
            "the discharged exit must call the one action-service method");
        StringAssert.Contains(exit, "true, 5",
            "the discharged exit must be registered isLeave = true at index 5");
        Assert.IsFalse(exit!.Contains("IsEnabled = false"),
            "a disabled exit is no exit: the player would be back in the softlock");
    }
}
