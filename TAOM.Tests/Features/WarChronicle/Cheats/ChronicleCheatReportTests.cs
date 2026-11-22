using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.WarChronicle.Cheats;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.WarChronicle.Cheats;

/// <summary>The text of <c>taom.chronicle_status</c> and the argument check of <c>taom.chronicle_open</c>.</summary>
[TestClass]
public class ChronicleCheatReportTests
{
    private static List<ChronicleStatusRow> Rows() => new List<ChronicleStatusRow>
    {
        new ChronicleStatusRow { Id = "fellowship", Kind = ChronicleEventKind.Narrative, State = ChronicleEventState.Occurred, OpenDay = 20, ResolvedDay = 20 },
        new ChronicleStatusRow { Id = "hornburg", Kind = ChronicleEventKind.Siege, State = ChronicleEventState.Active, OpenDay = 49, CloseDay = 69, Assaulted = true, Target = "town_V2" },
        new ChronicleStatusRow { Id = "gondor", Kind = ChronicleEventKind.Siege, State = ChronicleEventState.Pending, Target = "town_EW1" },
    };

    [TestMethod]
    public void FormatStatus_ListsEveryEventWithItsStateAndDays()
    {
        var text = ChronicleCheatReport.FormatStatus(Rows(), new ArmyTargetFocus[0], 55.2, true, true);

        StringAssert.Contains(text, "day 55.2");
        StringAssert.Contains(text, "fellowship");
        StringAssert.Contains(text, "Occurred");
        StringAssert.Contains(text, "hornburg");
        StringAssert.Contains(text, "Active");
        StringAssert.Contains(text, "town_V2");
        StringAssert.Contains(text, "closes day 69");
        StringAssert.Contains(text, "assaulted");
        StringAssert.Contains(text, "gondor");
        StringAssert.Contains(text, "opens when its anchor is known");
    }

    [TestMethod]
    public void FormatStatus_ListsTheArmyFocus()
    {
        var focus = new[] { new ArmyTargetFocus("town_V2", new[] { "isengard", "empire" }, "vlandia", 1.4f) };

        var text = ChronicleCheatReport.FormatStatus(Rows(), focus, 55, true, true);

        StringAssert.Contains(text, "town_V2 x1.40: besieged by isengard, empire; defended by vlandia");
        Assert.IsFalse(text.Contains("inactive"));
    }

    [TestMethod]
    public void FormatStatus_StrategicIntelligenceOff_SaysTheFocusIsInactive()
    {
        var focus = new[] { new ArmyTargetFocus("town_V2", new[] { "isengard" }, "vlandia", 1.4f) };

        var text = ChronicleCheatReport.FormatStatus(Rows(), focus, 55, true, false);

        StringAssert.Contains(text, "inactive: AI Strategic Intelligence is off");
    }

    [TestMethod]
    public void FormatStatus_AnOpenHeroStory_NamesItsWindowAndNoAssault()
    {
        var rows = new[] { new ChronicleStatusRow { Id = "breaking", Kind = ChronicleEventKind.Narrative, State = ChronicleEventState.Active, OpenDay = 2, CloseDay = 12 } };

        var text = ChronicleCheatReport.FormatStatus(rows, new ArmyTargetFocus[0], 3, true, true);

        StringAssert.Contains(text, "breaking (Narrative) Active: opened day 2, closes day 12, a hero's story window");
        Assert.IsFalse(text.Contains("assaulted"), text);
    }

    [TestMethod]
    public void FormatStatus_AHeroStoryAtItsCloseDay_SaysTheDeathIsOwed()
    {
        var rows = new[] { new ChronicleStatusRow { Id = "breaking", Kind = ChronicleEventKind.Narrative, State = ChronicleEventState.Active, OpenDay = 2, CloseDay = 5 } };

        var text = ChronicleCheatReport.FormatStatus(rows, new ArmyTargetFocus[0], 5, true, true);

        StringAssert.Contains(text, "breaking (Narrative) Active: opened day 2, decided day 5, the hero's death is owed");
    }

    [TestMethod]
    public void FormatStatus_ActivePastItsCloseDay_SaysTheCloseWaitsOnTheSiege()
    {
        var text = ChronicleCheatReport.FormatStatus(Rows(), new ArmyTargetFocus[0], 72, true, true);

        StringAssert.Contains(text, "overtime: an attacker still besieges it");
    }

    [TestMethod]
    public void FormatStatus_ChronicleOff_SaysSo()
    {
        StringAssert.Contains(ChronicleCheatReport.FormatStatus(Rows(), new ArmyTargetFocus[0], 1, false, true), "off");
    }

    [TestMethod]
    public void FormatStatus_NoEvents_SaysTheFileLoadedNone()
    {
        StringAssert.Contains(ChronicleCheatReport.FormatStatus(new List<ChronicleStatusRow>(), new ArmyTargetFocus[0], 1, true, true), "No chronicle events");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(3)]
    public void TryReadOpenArgs_NeitherAnIdNorAnIdAndConfirm_ReturnsTheUsage(int count)
    {
        var args = new List<string>();
        for (var i = 0; i < count; i++)
            args.Add("x" + i);

        Assert.IsFalse(ChronicleCheatReport.TryReadOpenArgs(args, out _, out _, out var error));
        StringAssert.Contains(error, "taom.chronicle_open <id> [confirm]");
    }

    [TestMethod]
    public void TryReadOpenArgs_OneId_IsADryRunWithTheIdTrimmed()
    {
        Assert.IsTrue(ChronicleCheatReport.TryReadOpenArgs(new List<string> { " hornburg " }, out var id, out var confirmed, out _));
        Assert.AreEqual("hornburg", id);
        Assert.IsFalse(confirmed);
    }

    [TestMethod]
    public void TryReadOpenArgs_IdThenTheLiteralConfirm_Acts()
    {
        Assert.IsTrue(ChronicleCheatReport.TryReadOpenArgs(new List<string> { "hornburg", "confirm" }, out var id, out var confirmed, out _));
        Assert.AreEqual("hornburg", id);
        Assert.IsTrue(confirmed);
    }

    [DataTestMethod]
    [DataRow("Confirm")]
    [DataRow("yes")]
    [DataRow("confirm!")]
    public void TryReadOpenArgs_ASecondWordOtherThanConfirm_IsRefused(string word)
    {
        Assert.IsFalse(ChronicleCheatReport.TryReadOpenArgs(new List<string> { "hornburg", word }, out _, out var confirmed, out var error));
        Assert.IsFalse(confirmed);
        StringAssert.Contains(error, "confirm");
    }

    [TestMethod]
    public void FormatOpen_ADryRunThatWouldAct_SaysHowToConfirm()
    {
        var text = ChronicleCheatReport.FormatOpen("hornburg", dryRun: true, acted: true, "'hornburg' would open its window until day 23.");

        StringAssert.StartsWith(text, "[WarChronicle] Dry run, nothing changed: ");
        StringAssert.Contains(text, "taom.chronicle_open hornburg confirm");
    }

    [TestMethod]
    public void FormatOpen_ADryRunThatWouldNotAct_OffersNoConfirm()
    {
        var text = ChronicleCheatReport.FormatOpen("hornburg", dryRun: true, acted: false, "'hornburg' would stay Pending: x.");

        StringAssert.StartsWith(text, "[WarChronicle] Dry run, nothing changed: ");
        Assert.IsFalse(text.Contains("confirm"), text);
    }

    [DataTestMethod]
    [DataRow(true, "[WarChronicle] Opened: ")]
    [DataRow(false, "[WarChronicle] Not opened: ")]
    public void FormatOpen_Confirmed_SaysWhatHappened(bool acted, string prefix)
    {
        StringAssert.StartsWith(ChronicleCheatReport.FormatOpen("hornburg", dryRun: false, acted, "why"), prefix);
    }
}
