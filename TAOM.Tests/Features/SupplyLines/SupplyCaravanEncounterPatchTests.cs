using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SupplyLines.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// #1086 follow-up: a co-op guest's caravans are the host's, so the meeting line must not call the caravan theirs.
/// The rest pins the fix for a meeting that ended its own encounter from inside <c>PlayerEncounter.Init</c>.
/// </summary>
[TestClass]
public class SupplyCaravanEncounterPatchTests
{
    [TestMethod]
    public void TheOwnersLine_CallsTheCaravanTheirs()
    {
        StringAssert.StartsWith(SupplyCaravanEncounterPatch.MeetingLine(defersToHost: false), "{=taom_sl_caravan_meet}Your ");
    }

    [TestMethod]
    public void AGuestsLine_HasItsOwnKey_AndDoesNotCallTheCaravanTheirs()
    {
        string line = SupplyCaravanEncounterPatch.MeetingLine(defersToHost: true);

        StringAssert.StartsWith(line, "{=taom_sl_caravan_meet_other}");
        Assert.IsFalse(line.Contains("Your") || line.Contains("your"), line);
    }

    [TestMethod]
    public void RunsVanilla_NotASupplyCaravan_RunsVanillaOpensNoMenuAndReportsNothing()
    {
        var menus = Substitute.For<IGameMenuAdapter>();
        var outcomes = Substitute.For<IOutcomeLog>();

        Assert.IsTrue(SupplyCaravanEncounterPatch.RunsVanilla(isSupplyCaravan: false, "lord_party", menus, outcomes));

        menus.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
        Assert.AreEqual(0, outcomes.ReceivedCalls().Count());
    }

    [TestMethod]
    public void RunsVanilla_SupplyCaravanAndTheMenuOpens_SkipsVanillaAndReportsPass()
    {
        var menus = Substitute.For<IGameMenuAdapter>();
        var outcomes = Substitute.For<IOutcomeLog>();
        menus.EnsureMenuOpen(SupplyCaravanMeetingMenu.MenuId).Returns(true);

        Assert.IsFalse(SupplyCaravanEncounterPatch.RunsVanilla(isSupplyCaravan: true, "caravan_1", menus, outcomes));

        outcomes.Received(1).Pass(SupplyCaravanEncounterPatch.MeetingCheckId, "", "party=caravan_1");
        outcomes.DidNotReceive().Fail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void RunsVanilla_SupplyCaravanAndTheMenuFails_RunsVanillaAndReportsFail()
    {
        var menus = Substitute.For<IGameMenuAdapter>();
        var outcomes = Substitute.For<IOutcomeLog>();
        menus.EnsureMenuOpen(SupplyCaravanMeetingMenu.MenuId).Returns(false);

        Assert.IsTrue(SupplyCaravanEncounterPatch.RunsVanilla(isSupplyCaravan: true, "caravan_1", menus, outcomes));

        outcomes.Received(1).Fail(SupplyCaravanEncounterPatch.MeetingCheckId, "menu-not-open", "party=caravan_1");
        outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Patch_DoesNotEndTheEncounterFromInsideInit()
    {
        // DoMeeting runs inside PlayerEncounter.Init, and EncounterManager reads PlayerEncounter.Current
        // straight after Init returns, so ending the encounter here throws there (v1.5.6 EncounterManager.cs:339-340).
        string source = File.ReadAllText(
            RepoPaths.RepoPath("Main", "Features", "SupplyLines", "Hooks", "SupplyCaravanEncounterPatch.cs"));

        Assert.IsFalse(source.Contains("PlayerEncounter.Finish("), "the prefix must leave the encounter open");
    }
}
