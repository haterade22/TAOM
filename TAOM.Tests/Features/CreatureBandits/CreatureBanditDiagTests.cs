using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CreatureBandits;
using TAOM.Features.CreatureBandits.Diagnostics;

namespace TAOM.Tests.Features.CreatureBandits;

// Creature Bandits diagnostics (#692): a caller takes a line from the budget BEFORE it formats the line, so a
// suppressed line costs no string. TakeLine keeps Write's old rules: no logger spends nothing, the first refusal at
// the mission cap writes the one cap WARNING.
[TestClass]
[TestCategory("RequiresGame")]   // CreatureBanditDiag's static Serials map is keyed on the engine's Agent type
public class CreatureBanditDiagTests
{
    private IModLogger? _saved;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _saved = CreatureBanditLog.Logger;
        CreatureBanditDiag.ResetForMission();
        _logger = Substitute.For<IModLogger>();
        CreatureBanditLog.Logger = _logger;
    }

    [TestCleanup]
    public void Teardown()
    {
        CreatureBanditDiag.ResetForMission();
        CreatureBanditLog.Logger = _saved;
    }

    [TestMethod]
    public void TakeLine_NoLogger_ReturnsFalse_AndSpendsNoBudget()
    {
        CreatureBanditLog.Logger = null;

        Assert.IsFalse(CreatureBanditDiag.TakeLine(0, "snap"));
        Assert.AreEqual(0, CreatureBanditDiag.Ledger.MissionLines);
    }

    [TestMethod]
    public void TakeLine_WithinBudget_ReturnsTrue_SpendsOneLine_AndWritesNothingItself()
    {
        Assert.IsTrue(CreatureBanditDiag.TakeLine(0, "snap"));

        Assert.AreEqual(1, CreatureBanditDiag.Ledger.MissionLines);
        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void TakeLine_PastTheMissionCap_WritesOneCapWarning_ThenRefuses()
    {
        for (int i = 0; i < CreatureBanditDiag.MissionCap; i++)
            Assert.IsTrue(CreatureBanditDiag.TakeLine(0, "snap"));

        Assert.IsFalse(CreatureBanditDiag.TakeLine(0, "snap"));
        Assert.IsFalse(CreatureBanditDiag.TakeLine(0, "snap"));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("missionCap")));
    }

    [TestMethod]
    public void Emit_RoutesByTheWarningFlag()
    {
        CreatureBanditDiag.Emit("info line");
        CreatureBanditDiag.Emit("warning line", warning: true);

        _logger.Received(1).LogInfo("info line");
        _logger.Received(1).LogWarning("warning line");
    }

    // taom_debug.log (plan 030, DECISIONS D6): a mission with no creature spawn attempted or declined skips every agent
    // callback on one field read, and says so once at mission end, so the absence of [CreatureBandits][diag] lines is
    // explained in the log.
    [TestMethod]
    public void NoteMissionEnd_NoCreatureRegistered_WritesOneSkipLine()
    {
        CreatureBanditDiag.NoteMissionEnd(42.5f);

        _logger.Received(1).LogInfo("[CreatureBandits][diag] no-creatures t=42.50 " +
            "note=no creature spawn was attempted or declined this mission; every agent callback exited on one field read");
    }

    // A declined creature troop writes spawn-declined lines and the summary (declined=N) without registering a
    // creature, so the skip line must not follow them: each mission ends with the summary or this line, never both.
    [TestMethod]
    public void NoteMissionEnd_ACreatureTroopDeclined_LeavesTheMissionToTheSummary()
    {
        MissionThreadGuard.MarkMainThread();
        try
        {
            CreatureBanditDiag.NoteDeclined("spider_troop", isPlayerSide: true, isFieldBattle: true, hasCreatureItem: true, 3f);
            _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("spawn-declined")));
            _logger.ClearReceivedCalls();

            CreatureBanditDiag.NoteMissionEnd(42.5f);

            _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        }
        finally
        {
            MissionThreadGuard.ResetForTests();
        }
    }

    [TestMethod]
    public void NoteMissionEnd_ASpawnAttempted_LeavesTheMissionToTheSummary()
    {
        CreatureBanditDiag.SpawnsAttempted = 1;

        CreatureBanditDiag.NoteMissionEnd(42.5f);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // The per-creature cap refuses silently (no cap WARNING, that is the mission cap's) and counts the refusal.
    [TestMethod]
    public void TakeLine_PastThePerCreatureCap_RefusesWithoutAWarning()
    {
        var record = CreatureBanditDiag.Reserve("spider_troop", 1f);
        for (int i = 0; i < CreatureBanditDiag.PerCreatureCap; i++)
            Assert.IsTrue(CreatureBanditDiag.TakeLine(record.Serial, "attack"));

        Assert.IsFalse(CreatureBanditDiag.TakeLine(record.Serial, "attack"));

        Assert.AreEqual(1, record.SuppressedLines);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void NoteMissionEnd_ACreatureRegistered_WritesNothing()
    {
        CreatureBanditDiag.Reserve("spider_troop", 1f);

        CreatureBanditDiag.NoteMissionEnd(42.5f);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // A refused event line costs no string, the thread label included: WriteEvent used to format "<id>/main" before it
    // asked the budget, so every refused event still built two strings (Codex review of plan 030, 2026-10-03). Each
    // kind's per-creature allowance is spent through the writer itself, so a new kind is covered without being listed;
    // then every kind is replayed refused and the thread's allocation counter must not move. The counter is
    // GC.GetAllocatedBytesForCurrentThread: the .NET Framework runtime has it, the net472 reference assembly does not.
    [TestMethod]
    public void WriteEvent_EveryKindRefusedByTheBudget_AllocatesNothing()
    {
        var counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        Assert.IsNotNull(counter, "the runtime must expose GC.GetAllocatedBytesForCurrentThread");
        var allocatedBytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counter!);
        var record = CreatureBanditDiag.Reserve("spider_troop", 1f);
        var events = Enum.GetValues(typeof(CreatureDiagEventKind)).Cast<CreatureDiagEventKind>()
            .Select(kind => new CreatureDiagEvent(kind, record.Serial, 12.5f, threadId: 4321, onMain: false)).ToArray();
        foreach (var e in events)
            for (int i = 0; i <= CreatureBanditDiag.PerCreatureCap; i++)
                CreatureBanditDiagTicker.WriteEvent(e, record);   // the allowance, then one refusal that warms the refused path
        _logger.ClearReceivedCalls();

        long before = allocatedBytes();
        for (int round = 0; round < 100; round++)
            foreach (var e in events)
                CreatureBanditDiagTicker.WriteEvent(e, record);
        long allocated = allocatedBytes() - before;

        Assert.AreEqual(0L, allocated, "a refused event line must build no string");
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "a refused event line writes nothing");
    }

    // The label itself is unchanged: "<managed thread id>/main" or "/off", the last field of the line.
    [TestMethod]
    public void WriteEvent_AGrantedLine_EndsWithTheThreadAndItsSide()
    {
        var offThread = new CreatureDiagEvent(CreatureDiagEventKind.Panicked, 0, 3f, threadId: 4321, onMain: false, detail: "OnAgentPanicked");
        var mainThread = new CreatureDiagEvent(CreatureDiagEventKind.HitTaken, 0, 4f, threadId: 7, onMain: true);

        CreatureBanditDiagTicker.WriteEvent(offThread, null);
        CreatureBanditDiagTicker.WriteEvent(mainThread, null);

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.EndsWith(" panicked t=3.00 detail=OnAgentPanicked thread=4321/off")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.EndsWith(" thread=7/main")));
    }
}
