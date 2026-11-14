using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Tests.Features.LoadTimeStamps;
using static TAOM.Tests.Infrastructure.RepoPaths;
using ChatGate = TAOM.Core.Diagnostics.SelfDisableNotice.ChatGate;

namespace TAOM.Tests.Core.Diagnostics;

/// <summary>
/// Issue #1185: the main-thread notice that turns the ledger into a log line, a red chat line and a [Check] line.
/// It runs in two steps: Record logs each new entry on the first tick, Show names the logged entries in chat once the
/// chat can show them. The pure parts run on fakes; the engine-facing seams are pinned in the source text.
/// </summary>
[TestClass]
public class SelfDisableNoticeTests
{
    private static SelfDisableLedger LedgerWith(params string[] ids)
    {
        var ledger = new SelfDisableLedger();
        foreach (var id in ids) ledger.Report(id, "why " + id);
        return ledger;
    }

    private static IModLogger ThrowingLogger()
    {
        var logger = Substitute.For<IModLogger>();
        logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log down"));
        return logger;
    }

    // ---- Record: the log line, at once ----

    [TestMethod]
    public void Record_OnAClient_LogsEachEntry_AndKeepsItForTheChat_WithNoCheckLine()
    {
        var ledger = LedgerWith("Patch1_A", "Patch2_B");
        var logger = Substitute.For<IModLogger>();
        var outcomes = Substitute.For<IOutcomeLog>();

        SelfDisableNotice.Record(ledger, false, logger, outcomes);

        logger.Received(1).LogWarning("[SelfDisable] Patch1_A is off: why Patch1_A");
        logger.Received(1).LogWarning("[SelfDisable] Patch2_B is off: why Patch2_B");
        CollectionAssert.AreEqual(new[] { "Patch1_A", "Patch2_B" }, ledger.Unshown.Select(e => e.Id).ToArray());
        Assert.IsFalse(ledger.HasPending);
        Assert.AreEqual(0, outcomes.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Record_OnADedicatedServer_LogsAndSkipsEachId_AndKeepsNothing()
    {
        var ledger = LedgerWith("Patch1_A", "Patch2_B");
        var logger = Substitute.For<IModLogger>();
        var outcomes = Substitute.For<IOutcomeLog>();

        SelfDisableNotice.Record(ledger, true, logger, outcomes);

        logger.Received(1).LogWarning("[SelfDisable] Patch1_A is off: why Patch1_A");
        logger.Received(1).LogWarning("[SelfDisable] Patch2_B is off: why Patch2_B");
        outcomes.Received(1).Skip(SelfDisableNotice.CheckId, "Patch1_A", "feature=Patch1_A reason=dedicated-server");
        outcomes.Received(1).Skip(SelfDisableNotice.CheckId, "Patch2_B", "feature=Patch2_B reason=dedicated-server");
        outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
        Assert.AreEqual(0, ledger.Unshown.Count);
    }

    [TestMethod]
    public void Record_WithNothingPending_DoesNothing()
    {
        var ledger = new SelfDisableLedger();
        var logger = Substitute.For<IModLogger>();
        var outcomes = Substitute.For<IOutcomeLog>();

        SelfDisableNotice.Record(ledger, false, logger, outcomes);

        Assert.AreEqual(0, logger.ReceivedCalls().Count());
        Assert.AreEqual(0, outcomes.ReceivedCalls().Count());
        Assert.AreEqual(0, ledger.Unshown.Count);
    }

    [TestMethod]
    public void Record_WhenTheLoggerThrows_StillKeepsEachEntryForTheChat()
    {
        var ledger = LedgerWith("Patch1_A", "Patch2_B");

        SelfDisableNotice.Record(ledger, false, ThrowingLogger(), Substitute.For<IOutcomeLog>());

        Assert.AreEqual(2, ledger.Unshown.Count);
    }

    [TestMethod]
    public void Record_OnADedicatedServer_WhenTheLoggerThrows_StillSkipsEachId()
    {
        var outcomes = Substitute.For<IOutcomeLog>();

        SelfDisableNotice.Record(LedgerWith("Patch1_A"), true, ThrowingLogger(), outcomes);

        outcomes.Received(1).Skip(SelfDisableNotice.CheckId, "Patch1_A", "feature=Patch1_A reason=dedicated-server");
    }

    // ---- Show: the chat line, when the chat can show it ----

    private static SelfDisableLedger Recorded(params string[] ids)
    {
        var ledger = LedgerWith(ids);
        SelfDisableNotice.Record(ledger, false, Substitute.For<IModLogger>(), Substitute.For<IOutcomeLog>());
        return ledger;
    }

    [TestMethod]
    public void Show_WhenTheChatCanShowIt_ShowsOneLineNamingEveryId_AndPassesEachId()
    {
        var ledger = Recorded("Patch1_A", "Patch2_B");
        var outcomes = Substitute.For<IOutcomeLog>();
        var shown = new List<string>();

        SelfDisableNotice.Show(ledger, ChatGate.Show, outcomes, shown.Add);

        CollectionAssert.AreEqual(new[] { "Patch1_A, Patch2_B" }, shown);
        outcomes.Received(1).Pass(SelfDisableNotice.CheckId, "Patch1_A", "feature=Patch1_A shown=chat");
        outcomes.Received(1).Pass(SelfDisableNotice.CheckId, "Patch2_B", "feature=Patch2_B shown=chat");
        Assert.AreEqual(0, ledger.Unshown.Count);
    }

    [TestMethod]
    public void Show_WhileTheGateHolds_ShowsNothing_AndKeepsTheEntries()
    {
        var ledger = Recorded("Patch1_A");
        var outcomes = Substitute.For<IOutcomeLog>();
        var shown = new List<string>();

        SelfDisableNotice.Show(ledger, ChatGate.Hold, outcomes, shown.Add);

        Assert.AreEqual(0, shown.Count);
        Assert.AreEqual(0, outcomes.ReceivedCalls().Count());
        Assert.AreEqual(1, ledger.Unshown.Count);
    }

    [TestMethod]
    public void Show_WithTheChatBoxOff_SkipsEachId_ShowsNothing_AndKeepsNothing()
    {
        var ledger = Recorded("Patch1_A");
        var outcomes = Substitute.For<IOutcomeLog>();

        SelfDisableNotice.Show(ledger, ChatGate.Off, outcomes, _ => Assert.Fail("the chat box is off"));

        outcomes.Received(1).Skip(SelfDisableNotice.CheckId, "Patch1_A", "feature=Patch1_A reason=chat-off");
        outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
        Assert.AreEqual(0, ledger.Unshown.Count);
    }

    [TestMethod]
    public void Show_Twice_ShowsAnEntryOnlyOnce()
    {
        var ledger = Recorded("Patch1_A");
        var outcomes = Substitute.For<IOutcomeLog>();
        var shown = new List<string>();

        SelfDisableNotice.Show(ledger, ChatGate.Show, outcomes, shown.Add);
        SelfDisableNotice.Show(ledger, ChatGate.Show, outcomes, shown.Add);

        Assert.AreEqual(1, shown.Count);
        outcomes.Received(1).Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Show_WhenTheChatLineThrows_WritesNoPassLine_AndMarksTheFailure()
    {
        var ledger = Recorded("Patch1_A");
        var outcomes = Substitute.For<IOutcomeLog>();

        Assert.ThrowsException<InvalidOperationException>(() =>
            SelfDisableNotice.Show(ledger, ChatGate.Show, outcomes, _ => throw new InvalidOperationException("no chat")));

        outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
        Assert.IsTrue(ledger.ShowFailed);
        Assert.AreEqual(1, ledger.Unshown.Count, "the entry waits for the next attempt");
    }

    [TestMethod]
    public void AfterAChatLineThatThrew_TheNextReport_NamesBothIds_ExactlyOnce()
    {
        var ledger = Recorded("Patch1_A");
        var logger = Substitute.For<IModLogger>();
        var outcomes = Substitute.For<IOutcomeLog>();
        var shown = new List<string>();
        Assert.ThrowsException<InvalidOperationException>(() =>
            SelfDisableNotice.Show(ledger, ChatGate.Show, outcomes, _ => throw new InvalidOperationException("no chat")));

        ledger.Report("Patch2_B", "why Patch2_B");
        SelfDisableNotice.Record(ledger, false, logger, outcomes);
        Assert.IsFalse(ledger.ShowFailed, "a new entry earns another attempt");
        SelfDisableNotice.Show(ledger, ChatGate.Show, outcomes, shown.Add);
        SelfDisableNotice.Show(ledger, ChatGate.Show, outcomes, shown.Add);

        CollectionAssert.AreEqual(new[] { "Patch1_A, Patch2_B" }, shown);
        outcomes.Received(1).Pass(SelfDisableNotice.CheckId, "Patch1_A", "feature=Patch1_A shown=chat");
        outcomes.Received(1).Pass(SelfDisableNotice.CheckId, "Patch2_B", "feature=Patch2_B shown=chat");
    }

    // ---- Gate: when the chat can show the line (v1.5.7 MPChatVM.IsChatAllowedByOptions, lines 1181-1195) ----

    [DataTestMethod]
    [DataRow(false, false, false, false, false, true, "Hold", "the main menu: no game")]
    [DataRow(true, true, true, false, false, true, "Hold", "a loading window is up")]
    [DataRow(true, false, false, false, false, true, "Hold", "character creation, an intro video or another screen")]
    [DataRow(true, false, true, false, false, true, "Show", "the campaign map")]
    [DataRow(true, false, true, true, false, true, "Show", "a mission with the battle UI shown")]
    [DataRow(true, false, true, true, true, true, "Hold", "a mission with Hide Battle UI on: the chat drops the line")]
    [DataRow(true, false, true, false, true, true, "Show", "the map with Hide Battle UI on: the option only hides in a mission")]
    [DataRow(true, false, true, false, false, false, "Off", "the single-player chat box is off")]
    [DataRow(true, true, true, false, false, false, "Hold", "chat off but still loading: decide when the screen settles")]
    public void Gate_Answers(bool gameRunning, bool loading, bool onMapOrInMission, bool inMission, bool hideBattleUi,
        bool chatBoxOn, string expected, string why) =>
        Assert.AreEqual(expected,
            SelfDisableNotice.Gate(gameRunning, loading, onMapOrInMission, inMission, hideBattleUi, chatBoxOn).ToString(), why);

    // ---- The lines the manifest reads ----

    /// <summary>
    /// The lines exactly as the notice writes them through a real OutcomeLog. tools/ingame_checks/1185.toml and the
    /// Python fixtures read these literals, so a change to the line format fails here or in Issue1185Tests.
    /// </summary>
    [TestMethod]
    public void RenderedLines_AreTheOnesThe1185ManifestAndFixturesRead()
    {
        const string passed = "[Check] id=taom.patch-notice.self-disable result=pass feature=Patch15_BannerLayerLimit shown=chat";
        const string otherPassed = "[Check] id=taom.patch-notice.self-disable result=pass feature=Patch110_BardTownGuard shown=chat";
        const string subSitePassed = "[Check] id=taom.patch-notice.self-disable result=pass feature=Patch15_BannerLayerLimit.Probe shown=chat";
        const string skipped = "[Check] id=taom.patch-notice.self-disable result=skip feature=Patch15_BannerLayerLimit reason=dedicated-server";
        const string threw = "[Swallowed] feature=PatchNotice site=SelfDisableNotice.Tick first=InvalidOperationException. ";
        const string otherSwallowed = "[Swallowed] feature=MarshalsWord site=MarshalWordComponent.OnAIInputSet first=NullReferenceException. ";
        var logger = Substitute.For<IModLogger>();
        var log = new OutcomeLog(logger, new FakeStampClock(), () => 0);

        var client = Recorded("Patch15_BannerLayerLimit", "Patch110_BardTownGuard");
        SelfDisableNotice.Show(client, ChatGate.Show, log, _ => { });
        SelfDisableNotice.Show(Recorded("Patch15_BannerLayerLimit.Probe"), ChatGate.Show, log, _ => { });
        SelfDisableNotice.Record(LedgerWith("Patch15_BannerLayerLimit"), true, Substitute.For<IModLogger>(), log);
        // The call exactly as SelfDisableNotice.Tick makes it from its catch, and another site's swallow.
        log.Swallowed("PatchNotice", "SelfDisableNotice.Tick", new InvalidOperationException("no chat"));
        log.Swallowed("MarshalsWord", "MarshalWordComponent.OnAIInputSet", new NullReferenceException());

        CollectionAssert.AreEqual(new[] { passed, otherPassed, subSitePassed, skipped }, Lines(logger, nameof(IModLogger.LogInfo)));
        var warnings = Lines(logger, nameof(IModLogger.LogWarning));
        Assert.AreEqual(2, warnings.Length);
        StringAssert.StartsWith(warnings[0], threw);
        StringAssert.StartsWith(warnings[1], otherSwallowed);

        var fixtures = new Dictionary<string, string[]>
        {
            ["self_disable_pass.txt"] = new[] { "[INFO] " + passed },
            ["self_disable_tick_threw.txt"] = new[] { "[WARNING] " + threw },
            ["self_disable_pass_and_threw.txt"] = new[] { "[INFO] " + passed, "[WARNING] " + threw },
            ["self_disable_other_feature.txt"] = new[] { "[INFO] " + otherPassed },
            ["self_disable_server_skip.txt"] = new[] { "[INFO] " + skipped },
            ["self_disable_near_misses.txt"] = new[] { "[INFO] " + subSitePassed, "[WARNING] " + otherSwallowed },
        };
        foreach (var fixture in fixtures)
        {
            var path = RepoPath("tools", "tests", "fixtures", "ingame_verdict", fixture.Key);
            Assert.IsTrue(File.Exists(path), path);
            var lines = File.ReadAllLines(path);
            foreach (var line in fixture.Value)
                Assert.IsTrue(lines.Any(l => l.Contains("] " + line)), fixture.Key + " lacks the rendered line: " + line);
        }
    }

    private static string[] Lines(IModLogger logger, string method) =>
        logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == method)
            .Select(c => (string)c.GetArguments()[0])
            .ToArray();

    // ---- The engine-facing seams, pinned in the source ----

    [TestMethod]
    public void SelfDisableNotice_ShowsTheRegisteredTextAsARedLine()
    {
        var source = ReadSource("Main/Core/Diagnostics/SelfDisableNotice.cs", stripComments: true);

        StringAssert.Contains(source, "{=taom_patch_self_disabled}TAOM: some fixes turned themselves off: {FEATURES}. "
            + "They stay off for the rest of this battle or this session; the TAOM log names the cause.");
        StringAssert.Contains(source, "Colors.Red");
    }

    /// <summary>
    /// Tick reads engine statics, so its wiring is pinned by source text, in order: the idle check before the try,
    /// Record when an entry is pending, no chat on a server or after a failed line, the client gate, then Show.
    /// Removing a step fails here; the steps' own answers are the tests above.
    /// </summary>
    [TestMethod]
    public void SelfDisableNotice_Tick_RecordsFirst_ThenAsksTheGateBeforeItShows()
    {
        var source = ReadSource("Main/Core/Diagnostics/SelfDisableNotice.cs", stripComments: true);

        var tick = source.IndexOf("internal static void Tick()", StringComparison.Ordinal);
        Assert.IsTrue(tick >= 0, "Tick was not found");
        int At(string text) => source.IndexOf(text, tick, StringComparison.Ordinal);
        var idle = At("if (!ledger.HasPending && ledger.Unshown.Count == 0) return;");
        var tryAt = At("try");
        var record = At("if (ledger.HasPending) Record(ledger, server, IoC.Resolve<IModLogger>(), IoC.Resolve<IOutcomeLog>());");
        var noChat = At("if (server || ledger.ShowFailed || ledger.Unshown.Count == 0) return;");
        var gate = At("var gate = ClientGate();");
        var hold = At("if (gate == ChatGate.Hold) return;");
        var show = At("Show(ledger, gate, IoC.Resolve<IOutcomeLog>(), ShowChatLine);");
        var caught = At("Outcomes.Swallowed(\"PatchNotice\", \"SelfDisableNotice.Tick\", ex);");

        Assert.IsTrue(idle > tick && tryAt > idle && record > tryAt && noChat > record && gate > noChat
            && hold > gate && show > hold && caught > show,
            $"order: idle {idle}, try {tryAt}, record {record}, no chat {noChat}, gate {gate}, hold {hold}, show {show}, catch {caught}");
    }

    /// <summary>
    /// The client gate reads the game state, the loading window and the two chat options the engine's chat filter
    /// reads. It names a CampaignSystem type, so it is a separate method that a server never calls (ADR-013).
    /// </summary>
    [TestMethod]
    public void SelfDisableNotice_ClientGate_ReadsTheChatOptions_AndIsNeverInlined()
    {
        var source = ReadSource("Main/Core/Diagnostics/SelfDisableNotice.cs", stripComments: true);

        var gate = source.IndexOf("private static ChatGate ClientGate()", StringComparison.Ordinal);
        Assert.IsTrue(gate >= 0, "ClientGate was not found");
        var attribute = source.LastIndexOf("[MethodImpl(MethodImplOptions.NoInlining)]", gate, StringComparison.Ordinal);
        Assert.IsTrue(attribute >= 0 && gate - attribute < 80, "ClientGate is NoInlining");
        foreach (var read in new[] { "state is MissionState", "state is MapState", "Game.Current != null",
                     "LoadingWindow.IsLoadingWindowActive", "BannerlordConfig.HideBattleUI", "BannerlordConfig.EnableSingleplayerChatBox" })
            Assert.IsTrue(source.IndexOf(read, gate, StringComparison.Ordinal) > gate, "ClientGate reads " + read);
    }

    [TestMethod]
    public void SubModule_OnApplicationTick_DrainsTheSelfDisableNotice()
    {
        var source = ReadSource("Main/SubModule.cs", stripComments: true);

        var tickStart = source.IndexOf("protected override void OnApplicationTick(float dt)", StringComparison.Ordinal);
        Assert.IsTrue(tickStart >= 0, "OnApplicationTick was not found");
        var next = source.IndexOf("protected override", tickStart + 1, StringComparison.Ordinal);
        var body = source.Substring(tickStart, next - tickStart);
        var call = "Core.Diagnostics.SelfDisableNotice.Tick();";
        var first = body.IndexOf(call, StringComparison.Ordinal);
        Assert.IsTrue(first >= 0, "OnApplicationTick drains the self-disable notice");
        Assert.AreEqual(-1, body.IndexOf(call, first + 1, StringComparison.Ordinal), "exactly one call");
    }
}
