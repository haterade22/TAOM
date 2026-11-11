using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarPledge;
using TAOM.Features.WarPledge.Cheats;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// #1113: the pure half of the console commands. <c>taom.add_war_pledge</c> is tier C (a pledge is saved and
/// nothing undoes it), so the bare command is a dry run, only the literal ordinal <c>confirm</c> acts, and the
/// arguments are checked before any lookup.
/// </summary>
[TestClass]
public class WarPledgeCheatReportTests
{
    private static List<string> Args(string raw) => new List<string>(raw.Split(' '));

    [DataTestMethod]
    [DataRow("cardolan free", "cardolan", FactionSide.Free, false)]
    [DataRow("cardolan FREE", "cardolan", FactionSide.Free, false)]
    [DataRow("rhudaur evil confirm", "rhudaur", FactionSide.Evil, true)]
    public void TryReadJoinArgs_ValidArguments_AreRead(string raw, string id, FactionSide side, bool confirmed)
    {
        Assert.IsTrue(WarPledgeCheatReport.TryReadJoinArgs(Args(raw), out var readId, out var readSide, out var readConfirmed, out var error), error);

        Assert.AreEqual(id, readId);
        Assert.AreEqual(side, readSide);
        Assert.AreEqual(confirmed, readConfirmed);
    }

    [DataTestMethod]
    [DataRow("cardolan", DisplayName = "no side")]
    [DataRow("cardolan neutral", DisplayName = "neutral is not a side to join")]
    [DataRow("cardolan good", DisplayName = "an unknown side")]
    [DataRow("cardolan 0", DisplayName = "a number for a side")]
    [DataRow("cardolan free Confirm", DisplayName = "confirm in another case")]
    [DataRow("cardolan free yes", DisplayName = "another word for confirm")]
    [DataRow("cardolan free confirm now", DisplayName = "a fourth argument")]
    [DataRow(" free", DisplayName = "a blank kingdom")]
    public void TryReadJoinArgs_AnythingElse_IsRefusedWithTheUsage(string raw)
    {
        Assert.IsFalse(WarPledgeCheatReport.TryReadJoinArgs(Args(raw), out _, out _, out var confirmed, out var error), raw);

        Assert.IsFalse(confirmed, raw);
        Assert.AreEqual(WarPledgeCheatReport.JoinUsage, error);
    }

    [TestMethod]
    public void TryReadJoinArgs_NoArguments_IsRefusedWithTheUsage()
    {
        Assert.IsFalse(WarPledgeCheatReport.TryReadJoinArgs(new List<string>(), out _, out _, out _, out var error));
        Assert.AreEqual(WarPledgeCheatReport.JoinUsage, error);
        Assert.IsFalse(WarPledgeCheatReport.TryReadJoinArgs(null!, out _, out _, out _, out error));
        Assert.AreEqual(WarPledgeCheatReport.JoinUsage, error);
    }

    [TestMethod]
    public void FormatJoin_ADryRunThatWouldAct_SaysNothingChangedAndHowToConfirm()
    {
        var text = WarPledgeCheatReport.FormatJoin("cardolan", FactionSide.Free, dryRun: true, acted: true, "cardolan would pledge to the Free side.");

        StringAssert.Contains(text, "Dry run, nothing changed: cardolan would pledge to the Free side.");
        StringAssert.Contains(text, "taom.add_war_pledge cardolan free confirm");
    }

    [TestMethod]
    public void FormatJoin_ADryRunThatWouldBeRefused_GivesTheReasonAndNoConfirmLine()
    {
        var text = WarPledgeCheatReport.FormatJoin("umbar", FactionSide.Free, dryRun: true, acted: false, "umbar is Hostile to empire_w, a realm of the Free side.");

        StringAssert.Contains(text, "Dry run, nothing changed: umbar is Hostile to empire_w");
        Assert.IsFalse(text.Contains("confirm"), text);
    }

    [DataTestMethod]
    [DataRow(true, "[WarPledge] Pledged: ")]
    [DataRow(false, "[WarPledge] Not pledged: ")]
    public void FormatJoin_AConfirmedRun_SaysWhetherItPledged(bool acted, string expectedStart)
    {
        var text = WarPledgeCheatReport.FormatJoin("cardolan", FactionSide.Free, dryRun: false, acted, "the reason.");

        Assert.AreEqual(expectedStart + "the reason.", text);
    }

    private static WarPledgeStatus Status(
        int balance = -30, int nextCourtingDay = 74, bool enabled = true, bool momentum = true, bool wotr = true,
        WarPhase phase = WarPhase.FullWar, FactionSide courting = FactionSide.Free) =>
        new WarPledgeStatus(enabled, momentum, wotr, phase, balance, courting, losingBalance: 25, daysBetweenAiJoins: 30, nextCourtingDay,
            new[]
            {
                new WarPledgeStatusRow("rhudaur", FactionSide.Evil, FactionSide.Neutral, isLive: true, isPlayerRuled: false),
                new WarPledgeStatusRow("cardolan", FactionSide.Free, FactionSide.Free, isLive: true, isPlayerRuled: false),
                new WarPledgeStatusRow("dorwinion", FactionSide.Free, FactionSide.Neutral, isLive: true, isPlayerRuled: true),
                new WarPledgeStatusRow("anduinvale", FactionSide.Neutral, FactionSide.Neutral, isLive: false, isPlayerRuled: false),
                new WarPledgeStatusRow("newrealm", null, FactionSide.Evil, isLive: true, isPlayerRuled: false),
            });

    [TestMethod]
    public void FormatStatus_PrintsTheSwitchesTheBalanceAndOneLinePerRealm()
    {
        var text = WarPledgeCheatReport.FormatStatus(Status(), day: 80);

        StringAssert.Contains(text, "[WarPledge] Campaign day 80. Switch on, momentum tracking on, War of the Ring on (FullWar).");
        StringAssert.Contains(text, "Balance -30 (positive when the Free Peoples lead); a side courts at 25 against it: the Free side courts.");
        StringAssert.Contains(text, "The AI courts no realm before day 74, and waits 30 days after a join.");
        StringAssert.Contains(text, "  rhudaur: leans Evil, not pledged");
        StringAssert.Contains(text, "  cardolan: leans Free, pledged Free");
        StringAssert.Contains(text, "  dorwinion: leans Free, not pledged, ruled by a player (the AI does not court it)");
        StringAssert.Contains(text, "  anduinvale: true neutral (the AI does not court it), not pledged, not a live kingdom");
        StringAssert.Contains(text, "  newrealm: no lean row, pledged Evil");
    }

    // The service decides which side courts (WarPledgeServiceTests pins the threshold); the report prints it.
    [DataTestMethod]
    [DataRow(FactionSide.Evil, "the Evil side courts.")]
    [DataRow(FactionSide.Free, "the Free side courts.")]
    [DataRow(FactionSide.Neutral, "no side courts.")]
    public void FormatStatus_PrintsTheCourtingSideTheServiceGives(FactionSide courting, string expected)
    {
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(courting: courting), day: 80), expected);
    }

    [TestMethod]
    public void FormatStatus_CourtingDayNotSet_SaysSo()
    {
        var text = WarPledgeCheatReport.FormatStatus(Status(nextCourtingDay: WarPledgeStore.NoCourtingDay), day: 80);

        StringAssert.Contains(text, "The courting day is not set");
    }

    [TestMethod]
    public void FormatStatus_AnythingOff_SaysTheAiCourtsNobody()
    {
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(enabled: false), day: 80), "Switch off");
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(enabled: false), day: 80), "The AI courts no realm now");
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(momentum: false), day: 80), "momentum tracking off");
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(momentum: false), day: 80), "The AI courts no realm now");
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(wotr: false), day: 80), "War of the Ring off");
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(phase: WarPhase.IsengardWar), day: 80), "The AI courts no realm now");
        Assert.IsFalse(WarPledgeCheatReport.FormatStatus(Status(), day: 80).Contains("The AI courts no realm now"));
    }

    // While the AI cannot court, the side the balance names is the side that would court, not one that does.
    [TestMethod]
    public void FormatStatus_AiCannotCourtNow_SaysWhichSideWouldCourt()
    {
        var off = WarPledgeCheatReport.FormatStatus(Status(enabled: false), day: 80);
        StringAssert.Contains(off, "the Free side would court.");
        Assert.IsFalse(off.Contains("the Free side courts."), off);

        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(), day: 80), "the Free side courts.");
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(phase: WarPhase.Peace, courting: FactionSide.Neutral), day: 80), "no side courts.");
    }

    [TestMethod]
    public void FormatStatus_AnUnknownDay_PrintsAQuestionMark()
    {
        StringAssert.Contains(WarPledgeCheatReport.FormatStatus(Status(), day: null), "Campaign day ?.");
    }
}
