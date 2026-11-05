using BehaviorTreeWrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Callback = BehaviorTreeWrapper.CallbackSkipLedger.Callback;

namespace TAOM.Tests.BehaviorTreeWrapper;

/// <summary>
/// The two taom_debug.log lines behind <c>BehaviorTreeMissionLogic</c>'s early returns (plan 033, DECISIONS D6), pinned
/// without the game so hosted CI runs them; <c>BehaviorTreeMissionLogicDispatchTests</c> pins the same lines through the
/// mission logic with the game loaded.
/// </summary>
[TestClass]
public class CallbackSkipLedgerTests
{
    [TestMethod]
    public void NoteSkip_FirstOfTheMission_ReturnsTheReasonLine()
    {
        var ledger = new CallbackSkipLedger();

        string? line = ledger.NoteSkip(Callback.OnAgentRemoved);

        Assert.AreEqual(
            "[BehaviorTree] OnAgentRemoved had no tree listener, so it returned before building arguments or parking " +
            "a replay; every such skip this mission is counted in the mission-end summary.",
            line);
    }

    [TestMethod]
    public void NoteSkip_AfterTheFirst_ReturnsNull()
    {
        var ledger = new CallbackSkipLedger();
        ledger.NoteSkip(Callback.OnAgentRemoved);

        Assert.IsNull(ledger.NoteSkip(Callback.OnAgentHit));
        Assert.IsNull(ledger.NoteSkip(Callback.OnAgentRemoved));
    }

    [TestMethod]
    public void Summary_WithSkipsAndAParkedCallback_CountsEachInDeclarationOrder()
    {
        var ledger = new CallbackSkipLedger();
        ledger.NoteSkip(Callback.OnAgentHit);
        ledger.NoteSkip(Callback.OnAgentRemoved);
        ledger.NoteSkip(Callback.OnAgentRemoved);
        ledger.NoteParked();

        Assert.AreEqual(
            "[BehaviorTree] Mission end: 3 callbacks skipped with no tree listener (OnAgentRemoved 2, OnAgentHit 1); " +
            "1 parked off-thread for the mission tick.",
            ledger.Summary());
    }

    [TestMethod]
    public void Reset_AfterAMission_StartsTheCountsAndTheReasonLineAgain()
    {
        var ledger = new CallbackSkipLedger();
        ledger.NoteSkip(Callback.OnAgentFleeing);
        ledger.NoteParked();

        ledger.Reset();

        Assert.AreEqual(
            "[BehaviorTree] Mission end: 0 callbacks skipped with no tree listener; 0 parked off-thread for the mission tick.",
            ledger.Summary());
        Assert.IsNotNull(ledger.NoteSkip(Callback.OnAgentFleeing), "the next mission's first skip writes its reason line");
    }
}
