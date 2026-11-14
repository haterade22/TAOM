using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Tests.Features.SeatOfSeeing;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The mark payload, the queue driver's marks, the capture prefix with the mark key held, the shot table, and the engine-facing statements of the
/// archers' hold, the mark and the outlines that a test cannot run, pinned as source text with the engine line behind each.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalMarkCaptureAndShotTableTests
{
    private FakeMarshalOrders _orders = null!;
    private RecordingOutcomeLog _log = null!;
    private MarshalQueueDriver _driver = null!;

    [TestInitialize]
    public void Setup()
    {
        MarshalHooks.Clear();
        MarshalFireTable.Clear();
        _orders = new FakeMarshalOrders();
        _log = new RecordingOutcomeLog();
        _driver = new MarshalQueueDriver(_orders, _log, "7");
    }

    [TestCleanup]
    public void Cleanup()
    {
        MarshalHooks.Clear();
        MarshalFireTable.Clear();
    }

    private static T Blank<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static MarshalOrderPayload ChargeOnACompany() => MarshalOrderPayload.ForFormation(OrderType.Charge, Blank<Formation>());

    // ---- the mark payload ----

    [TestMethod]
    public void ForMark_EndsAtOnce_KeepsItsTarget_AndNamesItself()
    {
        var target = Blank<Formation>();

        var mark = MarshalOrderPayload.ForMark(target);

        Assert.IsTrue(mark.IsMark);
        Assert.AreEqual(StepEnd.AtOnce, mark.End);
        Assert.AreSame(target, mark.Target);
        Assert.AreEqual("Mark:Charge", mark.Describe());
    }

    [TestMethod]
    public void ForMark_IsNeverIssuedOnAController_SoApplyThrows()
    {
        Assert.ThrowsException<InvalidOperationException>(() => MarshalOrderPayload.ForMark(Blank<Formation>()).Apply(Blank<OrderController>()));
    }

    [TestMethod]
    public void CanMark_OnlyAChargeOrAnAdvanceOnACompany()
    {
        Assert.IsTrue(ChargeOnACompany().CanMark);
        Assert.IsTrue(MarshalOrderPayload.ForFormation(OrderType.Advance, Blank<Formation>()).CanMark);
        Assert.IsFalse(MarshalOrderPayload.ForFormation(OrderType.Retreat, Blank<Formation>()).CanMark);
        Assert.IsFalse(MarshalOrderPayload.ForOrder(OrderType.Charge).CanMark, "a charge with no company has nothing to mark");
        Assert.IsFalse(MarshalOrderPayload.ForMark(Blank<Formation>()).CanMark, "a mark is already one");
        Assert.IsFalse(MarshalOrderPayload.ForFormation(OrderType.Charge, null!).CanMark);
    }

    [TestMethod]
    public void FireAtWill_IsAnInstantOrder_ThatDoesNotClearAQueue()
    {
        var payload = MarshalOrderPayload.FireAtWill();

        Assert.AreEqual(StepEnd.AtOnce, payload.End);
        Assert.IsFalse(MarshalOrderPayload.ClearsQueue(OrderType.FireAtWill));
    }

    // ---- the queue driver's marks ----

    [TestMethod]
    public void CaptureMark_SelectedCompaniesUnderTheirControl_AreMarkedAtTheNextPass_NotInTheCapture()
    {
        _orders.Selected.AddRange(new[] { 1, 2 });
        var mark = MarshalOrderPayload.ForMark(Blank<Formation>());

        var result = _driver.CaptureMark(mark);

        Assert.AreEqual(CaptureResult.Queued, result);
        Assert.AreEqual(0, _orders.Issued.Count, "the capture runs in the screen tick and only stores");
        _orders.Now = 0.1;
        _driver.Pass();
        Assert.AreEqual(1, _orders.Issued.Count);
        CollectionAssert.AreEqual(new[] { 1, 2 }, _orders.Issued[0].Companies);
        Assert.AreSame(mark, _orders.Issued[0].Payload);
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.MarkId));
        Assert.AreEqual(0, _driver.Queue.TotalSteps, "an immediate mark joins no queue");
    }

    [TestMethod]
    public void CaptureMark_OnlyTheCompaniesUnderTheirControl_AreMarked()
    {
        _orders.Selected.AddRange(new[] { 1, 2 });
        _orders.Lost.Add(2);

        _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>()));
        _driver.Pass();

        CollectionAssert.AreEqual(new[] { 1 }, _orders.Issued.Single().Companies);
    }

    [TestMethod]
    public void CaptureMark_NoCompanyUnderTheirControl_Declines_SoVanillaRunsAndTheClickIsNotSwallowed()
    {
        _orders.Selected.Add(2);
        _orders.Lost.Add(2);

        var result = _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>()));

        Assert.AreEqual(CaptureResult.Declined, result);
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.DeclinedId).Detail, "reason=ai-controlled");
    }

    [TestMethod]
    public void CaptureMark_NothingSelected_Declines_WithItsReason()
    {
        var result = _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>()));

        Assert.AreEqual(CaptureResult.Declined, result);
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.DeclinedId).Detail, "reason=none-selected");
    }

    [TestMethod]
    public void CaptureMark_InDeployment_Declines()
    {
        _orders.Selected.Add(1);
        _orders.IsDeployment = true;

        Assert.AreEqual(CaptureResult.Declined, _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>())));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.DeclinedId).Detail, "reason=deployment");
    }

    [TestMethod]
    public void Pass_AMarkIsAppliedEvenWhileTheQueueIsEmpty_AndInsideTheSlowPassWindow()
    {
        _orders.Selected.Add(1);
        _orders.Now = 10.0;
        _driver.Pass();
        _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>()));

        _orders.Now = 10.05;
        _driver.Pass();

        Assert.AreEqual(1, _orders.Issued.Count, "the next OnMissionTick applies it, not the next quarter second");
    }

    [TestMethod]
    public void Pass_AMarkNoMemberTook_WritesASkip_NotAPass()
    {
        _orders.Selected.Add(1);
        _orders.IssueResult = false;
        _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>()));

        _driver.Pass();

        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.MarkId));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.MarkId).Detail, "reason=not-applied");
    }

    [TestMethod]
    public void Pass_AMarkIsAppliedOnce()
    {
        _orders.Selected.Add(1);
        _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>()));

        _driver.Pass();
        _orders.Now = 5.0;
        _driver.Pass();

        Assert.AreEqual(1, _orders.Issued.Count);
    }

    [TestMethod]
    public void OnPlayerTeamChanged_DropsAMarkNotYetApplied_BecauseItsCompanyNumbersNameTheOldTeam()
    {
        _orders.Selected.Add(1);
        _driver.CaptureMark(MarshalOrderPayload.ForMark(Blank<Formation>()));

        _driver.OnPlayerTeamChanged();
        _driver.Pass();

        Assert.AreEqual(0, _orders.Issued.Count);
    }

    [TestMethod]
    public void AQueuedMarkStep_StartsAndEndsAtOnce_IssuingTheMarkAndNothingElse()
    {
        _orders.Selected.Add(1);
        var mark = MarshalOrderPayload.ForMark(Blank<Formation>());
        Assert.AreEqual(CaptureResult.Queued, _driver.Capture(StepEnd.AtOnce, mark, mark.Describe()));

        _orders.Now = 1.0;
        _driver.Pass();

        Assert.AreSame(mark, _orders.Issued.Single().Payload);
        Assert.AreEqual(0, _driver.Queue.TotalSteps, "an instant step finishes as it starts");
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.StartedId));
    }

    [TestMethod]
    public void AQueuedMarkStepBehindAMove_WaitsForTheMove_SoAMarkPlacedAfterAMoveStaysAfterIt()
    {
        _orders.Selected.Add(1);
        var move = new FakePayload("move");
        _driver.Capture(StepEnd.Arrival, move, "move");
        var mark = MarshalOrderPayload.ForMark(Blank<Formation>());
        _driver.Capture(StepEnd.AtOnce, mark, mark.Describe());

        _orders.Now = 1.0;
        _driver.Pass();

        Assert.AreSame(move, _orders.Issued.Single().Payload, "the mark has not started: the Move heads the list");
        Assert.AreEqual(2, _driver.Queue.CountOf(1));
    }

    // ---- the capture prefix with the mark key held ----

    private sealed class Sink : IMarshalHookSink
    {
        internal readonly List<MarshalOrderPayload> Offered = new();
        internal bool Answer;

        public bool TryCapture(MarshalOrderPayload payload)
        {
            Offered.Add(payload);
            return Answer;
        }

        public void ArraySolved(bool click, int companies)
        {
        }

        public void ArrayFault(Exception exception)
        {
        }

        public void MarchRequested(IReadOnlyList<MarchTarget> targets)
        {
        }

        public void PanelPress(PanelAction action, IReadOnlyList<int> companies)
        {
        }
    }

    [TestMethod]
    public void Capture_TheMarkKeyAloneArmsThePrefix_AndAnAnsweredOfferSkipsVanilla()
    {
        var sink = new Sink { Answer = true };
        var controller = Blank<OrderController>();
        MarshalHooks.PlayerController = controller;
        MarshalHooks.Begin(sink);
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true, mark: true);

        var runs = MarshalOrderCapturePatch.Prefix(controller, OrderType.Charge, Blank<Formation>());

        Assert.IsFalse(runs, "the mark replaced the charge");
        Assert.AreEqual(1, sink.Offered.Count);
        Assert.IsTrue(sink.Offered[0].CanMark);
    }

    [TestMethod]
    public void Capture_WithNeitherKeyDown_OffersNothing()
    {
        var sink = new Sink { Answer = true };
        var controller = Blank<OrderController>();
        MarshalHooks.PlayerController = controller;
        MarshalHooks.Begin(sink);
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true, mark: false);

        Assert.IsTrue(MarshalOrderCapturePatch.Prefix(controller, OrderType.Charge, Blank<Formation>()));
        Assert.AreEqual(0, sink.Offered.Count);
    }

    [TestMethod]
    public void Hooks_TheMarkArmedStatic_IsWrittenByArm_AndClearedByClear()
    {
        MarshalHooks.Begin(new Sink());
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true, mark: true);
        Assert.IsTrue(MarshalHooks.MarkArmed);

        MarshalHooks.Clear();

        Assert.IsFalse(MarshalHooks.MarkArmed);
    }

    [TestMethod]
    public void Hooks_BeginAndClear_ResetTheFireTable_SoANewMissionNeverReadsTheLastOnesModes()
    {
        MarshalHooks.Begin(new Sink());
        MarshalFireTable.SetMode(2, FireMode.OnWord);
        MarshalFireTable.GiveWord(2, 5.0);

        MarshalHooks.Begin(new Sink());
        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(2));
        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(2)));

        MarshalFireTable.SetMode(3, FireMode.OnWord);
        MarshalHooks.Clear();
        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(3));
    }

    // ---- the shot table ----

    [TestMethod]
    public void Count_EachKindGoesToItsOwnCounter_AndTheShotsBeforeTheFirstWordStopAtTheWord()
    {
        MarshalFireTable.Begin();
        MarshalFireTable.Count(4, ShotKind.WhileHolding);
        MarshalFireTable.Count(4, ShotKind.Free);
        MarshalFireTable.GiveWord(4, 3.0);
        MarshalFireTable.Count(4, ShotKind.AfterWord);
        MarshalFireTable.Count(4, ShotKind.AfterWord);
        MarshalFireTable.Count(4, ShotKind.AfterOther);

        Assert.AreEqual(new FireCounters(1, 2, 1, 2, 1), MarshalFireTable.CountersOf(4));
    }

    [TestMethod]
    public void Count_AShotAfterAReleaseThatNoWordCaused_IsNotAShotAfterTheWord()
    {
        MarshalFireTable.Begin();
        MarshalFireTable.SetMode(4, FireMode.OnWord);
        MarshalFireTable.Count(4, ShotKind.AfterOther);
        MarshalFireTable.Count(4, ShotKind.AfterOther);

        var counters = MarshalFireTable.CountersOf(4);

        Assert.AreEqual(0, counters.AfterWord, "the release check reads this counter, and a hold that ran out is not its word");
        Assert.AreEqual(2, counters.AfterOther);
        Assert.AreEqual(2, counters.BeforeFirstWord, "no word was given");
    }

    [TestMethod]
    public void TheWatch_CountsAfterBeginWatch_ResetsOnEachBegin_AndEndsWithEndWatchAndClear()
    {
        MarshalFireTable.Begin();

        MarshalFireTable.BeginWatch(1);

        Assert.AreEqual((0, 0), MarshalFireTable.Watched);
        MarshalFireTable.EndWatch();
        MarshalFireTable.BeginWatch(1);
        MarshalFireTable.Clear();
        MarshalFireTable.Begin();
        Assert.AreEqual((0, 0), MarshalFireTable.Watched);
    }

    [TestMethod]
    public void TheTable_ResetDropsTheWatchedShooters_SoNoAgentOutlivesItsMission()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalFireTable.cs");
        var reset = code.Substring(code.IndexOf("private static void Reset()", System.StringComparison.Ordinal), 520);

        StringAssert.Contains(reset, "Volatile.Write(ref _watched, -1); Watch.Reset();");
    }

    [TestMethod]
    public void SetMode_ResetsTheWordTime_SoAChangeOfModeStartsWithNoWord()
    {
        MarshalFireTable.Begin();
        MarshalFireTable.SetMode(1, FireMode.OnWord);
        MarshalFireTable.GiveWord(1, 9.0);

        MarshalFireTable.SetMode(1, FireMode.OnWord);

        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(1)));
    }

    [TestMethod]
    public void TheTable_IsSizedForEveryFormationClass_AndAnOutOfRangeCompanyReadsFreeAndCountsNothing()
    {
        MarshalFireTable.Begin();

        Assert.AreEqual(10, MarshalFireTable.Companies, "FormationClass.NumberOfAllFormations: the General and Bodyguard formations count");
        MarshalFireTable.SetMode(99, FireMode.OnWord);
        MarshalFireTable.Count(-1, ShotKind.Free);
        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(99));
        Assert.AreEqual(default(FireCounters), MarshalFireTable.CountersOf(-1));
    }

    // ---- the engine-facing statements ----

    private static string Code(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    [TestMethod]
    public void TheComponent_RunsItsWholeCallbackInOneTry_AndLatchesOffWithOneSwallowedLine()
    {
        // Agent.cs:1660-1666 calls every component's OnAIInputSet with no try, and the native shim has no finalizer (harmony-patch-registry.md:309).
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");
        var body = code.Substring(code.IndexOf("public override void OnAIInputSet(", StringComparison.Ordinal));

        StringAssert.Contains(body, "if (_faulted) return; try {");
        StringAssert.Contains(body, "catch (Exception ex) { _faulted = true; Outcomes.Swallowed(\"MarshalsWord\", \"MarshalWordComponent.OnAIInputSet\", ex); SelfDisables.Report(SelfDisableId, ex.GetType().Name); }");
        StringAssert.Contains(code, "private const string SelfDisableId = MarshalsWordModule.PatchCategory + \".SoldierCallback\";");
    }

    [TestMethod]
    public void TheComponent_HoldWritesTheDrawBitOverTheCleanedMasks_AndReleaseClearsTheAttackBitsAlone()
    {
        // Native copies the stored result over the agent's flags before the callback, so a hold repeats each call, and the AI's abandon bit
        // (0x4000) must not stand beside the draw (build-spec 7.2).
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "movementFlag &= ~(Agent.MovementControlFlag.AttackMask | Agent.MovementControlFlag.DefendMask); movementFlag |= Agent.MovementControlFlag.AttackDown;");
        StringAssert.Contains(code, "movementFlag &= ~Agent.MovementControlFlag.AttackMask; Volatile.Write(ref _releasedByWord, wordDue ? 1 : 0); Volatile.Write(ref _releasedAt, now);");
    }

    [TestMethod]
    public void TheComponent_RecordsWhetherTheWordCausedARelease_SoAHoldThatRanOutIsNotCountedAsTheWordsShot()
    {
        // The release at HoldLimit (MarshalFireRules.Step) clears the same bits as a word's, so only the cause tells a shot of the word from a shot of the limit.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "Apply(step.Action, now, wordDue, ref movementFlag);");
        StringAssert.Contains(code, "return Volatile.Read(ref _releasedByWord) != 0 ? ShotKind.AfterWord : ShotKind.AfterOther;");
    }

    [TestMethod]
    public void TheComponent_ReportsNoStandAsideBeforeItsFirstRead_SoTheCensusDoesNotCountAFreshSoldierAsAside()
    {
        // The first callback reads the answer before it uses it, so the starting value is seen only by the census that touches the soldier.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "private bool _standAside; private double _nextAsideRead = double.NegativeInfinity;");
    }

    [TestMethod]
    public void TheShotReader_NotesAWatchedCompanysShotBeforeItTestsTheMode_SoAFreeCompanyIsCounted()
    {
        var reader = Code("Main/Adapters/MarshalCombatReader.cs");
        var table = Code("Main/Features/MarshalsWord/MarshalFireTable.cs");

        StringAssert.Contains(reader, "MarshalFireTable.NoteShooter(company, shooter); MarshalFireTable.CountAnyShot(company); if (MarshalFireTable.ModeOf(company) == FireMode.Free) return;");
        StringAssert.Contains(table, "if (InRange(company) && company == Volatile.Read(ref _watched)) Watch.Note(shooter);");
    }

    [TestMethod]
    public void TheComponent_WritesNoCanAttack_NoLogLine_AndNoBehaviourWeight()
    {
        // Build-spec 7.4: no AgentFlag.CanAttack, no behaviour weights, no movement input. The callback is a hot path: counters only.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        var callback = code.Substring(code.IndexOf("public override void OnAIInputSet(", StringComparison.Ordinal), code.IndexOf("private void Apply(", StringComparison.Ordinal) - code.IndexOf("public override void OnAIInputSet(", StringComparison.Ordinal));
        Assert.IsFalse(Regex.IsMatch(callback, @"CanAttack|ApplyHold|SetAgentFlags|SetAIBehaviorParams|SetFiringOrder|inputVector\s*="), "the callback writes the movement flags and nothing else; the attack flag is the driver's, from the mission tick");
        Assert.IsFalse(Regex.IsMatch(code, @"LogInfo|LogWarning|LogError|\.Pass\(""|\.Fail\(|\.Skip\("), "no line is written from the component; counters only");
        StringAssert.Contains(code, "Holds.Pass();");
        StringAssert.Contains(code, "Releases.Pass();");
    }

    [TestMethod]
    public void TheComponent_ReadsItsStandAsideFactsAtMostThreeTimesASecond_AndNeverTheSlotsIndex()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "if (now >= _nextAsideRead) { _standAside = ReadStandsAside(formation, company); _nextAsideRead = now + MarshalTuning.StandAsideReadSeconds; }");
        Assert.IsFalse(Regex.IsMatch(code, @"Agent\.Index"), "never key on Agent.Index");
    }

    [TestMethod]
    public void TheFireAdapter_TurnsTheCallbackOnOnceForATouchedSoldier_AndOffOnlyForTheSlotsCurrentOccupant()
    {
        // Agent.cs:4409-4420: AddComponent does not call Initialize. Native calls only a flagged agent. A deleted agent's handle answers for the
        // slot's next tenant (#592), so a release is gated on the occupant.
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");

        StringAssert.Contains(code, "if (_touched.Add(agent)) agent.SetHasOnAiInputSetCallback(true);");
        StringAssert.Contains(code, "if (!AgentSlotIdentity.IsCurrentOccupant(agent) || agent.State == AgentState.Deleted) return;");
        StringAssert.Contains(code, "try { if (component is { OwesAttackFlag: true } or { OwesFiringOrder: true }) component.ReleaseHold(); } finally { agent.SetHasOnAiInputSetCallback(false); component?.Reset(); }");
        StringAssert.Contains(code, "MissionThreadGuard.NoteCall(\"MarshalFireAdapter.Sweep\", _logger.LogError);");
        Assert.IsFalse(Regex.IsMatch(code, @"\b(?:agent|unit|Agent)\.Index\b"), "the touched set is keyed by the agent object, never its index; a company is the formation's Index");
    }

    [TestMethod]
    public void TheFireAdapter_ReleaseAll_IsTheOneTeardownWithNativeCalls_ThroughTheSameOccupantGate()
    {
        // A fault leaves the mission running, so every touched soldier keeps its callback flag until this turns it off (build-spec 7.2).
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");

        StringAssert.Contains(code, "public void ReleaseAll() { MissionThreadGuard.NoteCall(\"MarshalFireAdapter.ReleaseAll\", _logger.LogError); Exception? first = null; foreach (var agent in _touched) { try { TurnOff(agent); }");
    }

    [TestMethod]
    public void TheFireAdapter_CountsEverySoldierItTouchesInOneOfFourStates_SoTheCensusSeparatesTheOnesStandingAsideFromTheIdle()
    {
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");

        StringAssert.Contains(code, "_census.Add(Read(company));");
        StringAssert.Contains(code, "if (state == DrawState.Drawing) { _drawing[company]++; if (MarshalFireRules.IsStaleDraw(component.StateSince, now)) _stale[company]++; } else if (state == DrawState.Holding) { _holding[company]++; } else if (component.StandingAside) { _aside[company]++; } else { _idle[company]++; }");
        StringAssert.Contains(code, "if (component.AttackFlagOff) _flagOff[company]++;");
    }

    [TestMethod]
    public void TheFireAdapter_DropMakesNoNativeCall()
    {
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");
        var drop = code.Substring(code.IndexOf("public void Drop()", StringComparison.Ordinal), 220);

        Assert.IsFalse(drop.Contains("SetHasOnAiInputSetCallback"), drop);
    }

    [TestMethod]
    public void TheOutlineAdapter_PaintsAsTheRaceAbilityPaintDoes_AndEachMountIsAWantedUnitOfItsOwn()
    {
        // RaceAbilityVisuals.cs:120-127: the slot's current occupant, not deleted, valid visuals.
        var code = Code("Main/Adapters/MarshalOutlineAdapter.cs");

        StringAssert.Contains(code, "if (!AgentSlotIdentity.IsCurrentOccupant(agent) || agent.State == AgentState.Deleted) return;");
        StringAssert.Contains(code, "if (visuals != null && visuals.IsValid()) visuals.SetContourColor(colour, true);");
        StringAssert.Contains(code, "MissionThreadGuard.NoteCall(\"MarshalOutlineAdapter.Paint\", _logger.LogError); PaintOne(unit, colour); }");
    }

    [TestMethod]
    public void TheOutlineAdapter_NeverReachesAMountThroughItsRiderAtClearTime_OnlyWhatWasPaintedIsCleared()
    {
        // A rider who dismounted or died before the clear has no MountAgent, so a clear that reached the mount through the rider left it painted
        // (build-spec 8.2). The mount is read as a wanted unit of its own, so the service's dictionary remembers it and clears it as itself.
        var code = Code("Main/Adapters/MarshalOutlineAdapter.cs");
        var paint = code.Substring(code.IndexOf("public void Paint(", StringComparison.Ordinal), 220);
        var add = code.Substring(code.IndexOf("private void Add(", StringComparison.Ordinal));

        Assert.IsFalse(paint.Contains("MountAgent"), paint);
        StringAssert.Contains(add, "var mount = agent.MountAgent; if (mount != null && mount.IsActive()) _into[mount] = _colour;");
    }

    [TestMethod]
    public void TheOutlineWriter_IsTheOnlyContourWriterOfMarshalsWord_InItsOwnFile()
    {
        // RaceAbilitiesWiringTests pins one write site per file, so this is a separate file.
        var writers = RepoPaths.CSharpFiles("Main", "Client").Where(f => f.Contains("Marshal") && File.ReadAllText(f).Contains("SetContourColor")).Select(Path.GetFileName).ToArray();

        CollectionAssert.AreEqual(new[] { "MarshalOutlineAdapter.cs" }, writers);
    }

    [TestMethod]
    public void TheOrderAdapter_MarksOnlyACompanyWithUnitsUnderTheirControl_AndGuardsTheNativeWrite()
    {
        // Formation.cs:240-249: the setter writes the target index of every soldier, from the main thread only.
        var code = Code("Main/Adapters/MarshalOrderAdapter.cs");
        var mark = code.Substring(code.IndexOf("private bool Mark(", StringComparison.Ordinal), 600);

        StringAssert.Contains(mark, "if (formation == null || formation.CountOfUnits == 0 || formation.IsAIControlled) continue;");
        StringAssert.Contains(mark, "MissionThreadGuard.NoteCall(\"MarshalOrderAdapter.Mark\", _logger.LogError); formation.SetTargetFormation(target);");
        StringAssert.Contains(code, "if (order.IsMark) return Mark(companies, order.Target!);");
    }

    [TestMethod]
    public void ThePlayerCommandsQuestion_IsTheOneTheOrderPanelAsks()
    {
        // MissionOrderVM.CheckCanBeOpened asks IsPlayerGeneral || IsPlayerSergeant (MissionOrderVM.cs:758).
        StringAssert.Contains(Code("Main/Adapters/MarshalOrderAdapter.cs"), "return team != null && (team.IsPlayerGeneral || team.IsPlayerSergeant);");
    }

    [TestMethod]
    public void TheMarkSample_ReadsAtMostTwentySoldiers_OneNativeReadEach()
    {
        var code = Code("Main/Adapters/MarshalOrderAdapter.cs");
        var sample = code.Substring(code.IndexOf("private void Sample(Agent agent)", StringComparison.Ordinal), 300);

        StringAssert.Contains(sample, "if (_sampled >= MarshalTuning.MarkSampleMax || !agent.IsHuman) return;");
        StringAssert.Contains(sample, "var aim = agent.GetTargetAgent();");
        Assert.AreEqual(20, MarshalTuning.MarkSampleMax);
    }

    [TestMethod]
    public void TheMarkSample_CountsAMountedTargetByItsRidersCompany()
    {
        // GetTargetAgent can answer a mount, and a mount's Formation reads null; the company a mark names is the rider's (Agent.cs:744).
        var code = Code("Main/Adapters/MarshalOrderAdapter.cs");
        var sample = code.Substring(code.IndexOf("private void Sample(Agent agent)", StringComparison.Ordinal), 400);

        StringAssert.Contains(sample, "if ((aim.RiderAgent ?? aim).Formation == _aimedAt) _onTarget++;");
    }

    [TestMethod]
    public void TheTrialHost_ReadsTheArchersCallbackLatchAsAFault_AndTheCachedRangedFlag()
    {
        // The callback latches off for the process after a throw; the session never sees it, so the trial must (build-spec 7.2). The screen tick
        // may not evaluate a query system, so the archers are found by its cached flag (FormationQuerySystem.cs:195).
        var code = Code("Client/Features/MarshalsWord/Trial/CommandTrialHost.Marshal.cs");

        StringAssert.Contains(code, "public bool MarshalFaulted => (MarshalSessionOfMission?.Faulted ?? false) || MarshalWordComponent.IsFaulted;");
        StringAssert.Contains(code, "f.CountOfUnits > 0 && f.QuerySystem.IsRangedFormationReadOnly)");
        Assert.IsFalse(code.Contains("QuerySystem.IsRangedFormation)"), "the evaluating flag writes the engine's cache");
    }

    [TestMethod]
    public void TheView_ForwardsBothCallbacksToTheCombatReader_AndReadsEachPressedKeyStaticFirst()
    {
        // The static test is cheap; the context test allocates a click key array on each call (InputContext.cs:310-312).
        var code = Code("Client/Features/MarshalsWord/MarshalView.cs");

        StringAssert.Contains(code, "base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex); MarshalCombatReader.OnShot(shooterAgent);");
        StringAssert.Contains(code, "key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyPressed(key) && Input.IsKeyPressed(key)");
        StringAssert.Contains(code, "Input.IsGameKeyDown((int)GameKeyDefinition.ShowIndicators)");
        StringAssert.Contains(code, "var fire = _trialFire || Pressed(_keys.FireMode); var word = _trialWord || Pressed(_keys.Word); var hold = _trialHold || Pressed(_keys.HoldLine); _trialFire = false; _trialWord = false; _trialHold = false;");
        StringAssert.Contains(code, "base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData); MarshalCombatReader.OnBlow(affectorAgent, affectedAgent, blow.IsMissile);");
    }

    [TestMethod]
    public void TheViewsTrialMembers_SetTheSameFieldsTheKeysDo()
    {
        var code = Code("Client/Features/MarshalsWord/MarshalView.cs");

        StringAssert.Contains(code, "internal void TrialHoldMark(bool held) => _trialMark = held;");
        StringAssert.Contains(code, "internal void TrialPressFire() => _trialFire = true;");
        StringAssert.Contains(code, "internal void TrialPressWord() => _trialWord = true;");
        StringAssert.Contains(code, "open && (_trialMark || Down(_keys.Mark))");
    }

    [TestMethod]
    public void TheShotPath_CountsWithInterlockedOnly_AndTestsTheTeamBeforeTheFormationIndex()
    {
        // Formation.Index repeats across teams, and native chooses the callback's thread (mission-frame-threads-and-native-costs.md:119).
        var reader = Code("Main/Adapters/MarshalCombatReader.cs");
        var table = Code("Main/Features/MarshalsWord/MarshalFireTable.cs");

        StringAssert.Contains(reader, "if (!MarshalFireTable.IsLive) return; var formation = shooter.Formation; if (formation == null || shooter.Team != Mission.Current?.PlayerTeam) return;");
        Assert.IsFalse(Regex.IsMatch(reader + table, @"\block\b|LogInfo|\.Pass\(|\.Fail\("), "no lock and no line from the shot callback");
        StringAssert.Contains(table, "Interlocked.Increment(ref ShotsWhileHolding[company]);");
        Assert.IsFalse(Regex.IsMatch(table, @"Mission\.|GetComponent|\.Formation\b|\.Team\b|OnShot"), "the table counts plain values and reads no engine state");
    }

    [TestMethod]
    public void TheSession_OwesTheSoldiersOneCleanupAfterAFault_PaysItAtOnceInTheMissionTick_AndSummarizesOnlyAsTheOwner()
    {
        var code = Code("Client/Features/MarshalsWord/MarshalSession.cs");

        // A fault in the screen tick or the capture writes no contour and owes the cleanup to the next mission tick; one in the mission tick pays it in the catch.
        StringAssert.Contains(code, "Swallow(_screen.HideFlags); _agentCleanupOwed = true; Swallow(Teardown);");
        StringAssert.Contains(code, "catch (Exception ex) { Fault(ex); PayAgentCleanup(); }");
        StringAssert.Contains(code, "if (_agentCleanupOwed) PayAgentCleanup(); if (_finished || Faulted) return;");
        StringAssert.Contains(code, "_agentCleanupOwed = false; Swallow(_outlines.ClearAll); Swallow(_outlines.Drop); Swallow(_fire.ReleaseAll); Swallow(_fire.Drop);");
        StringAssert.Contains(code, "if (owner) { Swallow(_fire.Summarize); Swallow(_throwers.Summarize); MarshalHooks.Clear(); } if (!_agentCleanupOwed) { Swallow(_fire.Drop); Swallow(_outlines.Drop); }");
    }

    [TestMethod]
    public void TheModule_RegistersTheFireAndOutlineAdapters_AsTransients()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalsWordModule.cs");

        StringAssert.Contains(code, "registrator.Register<IMarshalFireAdapter, MarshalFireAdapter>(Reuse.Transient);");
        StringAssert.Contains(code, "registrator.Register<IMarshalOutlineAdapter<TaleWorlds.MountAndBlade.Agent>, MarshalOutlineAdapter>(Reuse.Transient);");
    }

    [TestMethod]
    public void TheWordsLogLine_AndTheSummaryLine_AreTheContractsText()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalFireDriver.cs");

        StringAssert.Contains(code, "\"[Marshal] word company=\"");
        StringAssert.Contains(code, "\"[Marshal] fire company=\"");
        StringAssert.Contains(code, "\" shotsBeforeFirstWord=\"");
        StringAssert.Contains(code, "\" aside=\"");
        StringAssert.Contains(code, "\" shotsAfterOther=\"");
        StringAssert.Contains(Code("Main/Features/MarshalsWord/MarshalMarkWatch.cs"), "\"[Marshal] mark company=\"");
    }

    [TestMethod]
    public void TheHotPathCounters_HaveTheirIdsPinned()
    {
        Assert.AreEqual("taom.marshals-word.fire-hold", MarshalChecks.HoldCounterId);
        Assert.AreEqual("taom.marshals-word.fire-release", MarshalChecks.ReleaseCounterId);
    }
}
