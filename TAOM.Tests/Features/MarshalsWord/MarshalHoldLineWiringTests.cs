using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.MarshalsWord;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The hold-the-line slice's engine-facing statements, which a unit run cannot execute (the component and the adapter need a live agent), pinned to their source with
/// the engine line behind each, and the session's path for key 545.
/// </summary>
[TestClass]
public class MarshalHoldLineWiringTests
{
    private static string Code(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    [TestMethod]
    public void TheComponent_ClearsAndRestoresOnlyThroughTheFlagRule_AndOnlyTheAttackBit()
    {
        // AgentFlag.CanAttack = 8 (TaleWorlds.Core.AgentFlag.cs:12); Agent.SetAgentFlags writes the whole word (Agent.cs:2495), so the other bits are carried over.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "switch (MarshalHoldRules.FlagAction(clear, (flags & AgentFlag.CanAttack) != 0, _attackCleared, engineCleared))");
        StringAssert.Contains(code, "Agent.SetAgentFlags(flags & ~AgentFlag.CanAttack); _attackCleared = true;");
        StringAssert.Contains(code, "Agent.SetAgentFlags(flags | AgentFlag.CanAttack); _attackCleared = false;");
        StringAssert.Contains(code, "case AttackFlagAction.Forget: _attackCleared = false; break;");
    }

    [TestMethod]
    public void TheComponent_KeepsItsDebtUntilTheWriteWasMade()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        Assert.IsTrue(Regex.IsMatch(code, @"SetAgentFlags\(flags \| AgentFlag\.CanAttack\); _attackCleared = false;"), "the debt is cleared after the restoring write, so a throw leaves it owed");
        Assert.IsFalse(Regex.IsMatch(code, @"_attackCleared = false; Agent\.SetAgentFlags"), "never forget before the write");
    }

    [TestMethod]
    public void TheComponent_ReadsTheFlagBackOnlyAfterItClearedIt_SoAFlagTheEngineClearedIsNotOurs()
    {
        // ClimbingMachineDetachment.cs:241-243 and 266-268 clear CanAttack and CanDefend on a climber in water; that clear is not a hold.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "internal bool AttackFlagOff => _attackCleared && (Agent.GetAgentFlags() & AgentFlag.CanAttack) == 0;");
        StringAssert.Contains(code, "internal bool OwesAttackFlag => _attackCleared;");
    }

    [TestMethod]
    public void TheComponent_PassesTheCompanysAiControlAsTheThirdFact_SoALineEndsWhenTheHeroFalls()
    {
        // After the hero falls the team goes to the AI (Mission.cs:3041, Team.cs:483), which can keep a company in a wall under a Move; only this fact ends the line then.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");
        var constructor = typeof(HoldFacts).GetConstructors().First(c => c.GetParameters().Length == 11);

        Assert.AreEqual("CompanyUnderPlayer", constructor.GetParameters()[2].Name, "the third argument is the fact the next line passes");
        Assert.IsTrue(Regex.IsMatch(code, @"new HoldFacts\([^,]+,[^,]+, !formation\.IsAIControlled, Agent\.HasShieldCached,"), "the company's AI control, negated, is the third argument");
    }

    [TestMethod]
    public void TheComponent_ReadsTheHoldFactsAtMostThreeTimesASecond_AndBothReadersGoThroughTheGate()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        Assert.AreEqual(1.0 / 3.0, MarshalTuning.HoldReadSeconds, 1e-12, "three reads a second");
        StringAssert.Contains(code, "if (now < Volatile.Read(ref _nextHoldRead)) return; Volatile.Write(ref _nextHoldRead, now + MarshalTuning.HoldReadSeconds);");
        StringAssert.Contains(code, "ReadHold(holdOn, now); var want = MarshalHoldLineRules.Want(");
        StringAssert.Contains(code, "ReadHold(holdOn, now); if (!holdOn || !_qualifies");
    }

    [TestMethod]
    public void TheComponent_CallsASoldierTheEnginesClear_OnlyWhenHeIsDetachedAndTheDefendBitIsOffToo()
    {
        // ClimbingMachineDetachment.AddAgentAtSlotIndex sets the detachment (cs:50) and the same call clears both bits (cs:241-243); another detachment leaves CanDefend on.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "var engineCleared = Agent.IsDetachedFromFormation && (flags & AgentFlag.CanDefend) == 0;");
    }

    [TestMethod]
    public void TheAdapter_AppliesTheHoldToTheCompanysSoldiers_NeverToTheMainAgent_AndNeverReadsAFlagForACompanyThatIsNotHeld()
    {
        // The Seat can hand the hero to the AI (S4), which makes the main agent an AI-controlled soldier of a held company; the rule's IsMainAgent clause spares him.
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");
        var component = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "if (_holdThisCompany || component.OwesAttackFlag || component.OwesFiringOrder) component.RefreshHold(_holdThisCompany, _now);");
        StringAssert.Contains(code, "_holdThisCompany = Contains(held, company);");
        StringAssert.Contains(code, "if (component is { OwesAttackFlag: true } or { OwesFiringOrder: true }) component.ReleaseHold();");
        StringAssert.Contains(component, "Agent == Mission.Current?.MainAgent, Agent.IsRunningAway, Agent.HumanAIComponent?.IsInterestedInAnyGameObject() ?? false);");
    }

    [TestMethod]
    public void TheComponent_LeavesOutASoldierTheEngineSaysRunsAwayOrWalksToAnObject_FromMembersThatAreManagedFieldReads()
    {
        // Agent.cs:810 is a managed property the native retreat callback sets (cs:1641-1646); HumanAIComponent.cs:521-524 tests a field, so neither adds a native call.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");
        var constructor = typeof(HoldFacts).GetConstructors().First(c => c.GetParameters().Length == 11);

        Assert.AreEqual("RunningAway", constructor.GetParameters()[9].Name);
        Assert.AreEqual("GoingToObject", constructor.GetParameters()[10].Name);
        Assert.IsTrue(code.IndexOf("Agent.IsRunningAway", System.StringComparison.Ordinal) > code.IndexOf("private void ReadHold(", System.StringComparison.Ordinal), "read in the hold read, at the hold cadence, and not in the callback");
        Assert.IsFalse(code.Substring(code.IndexOf("public override void OnAIInputSet(", System.StringComparison.Ordinal), 900).Contains("IsRunningAway"), "never per tick");
    }

    [TestMethod]
    public void TheComponent_RefreshHold_ActsAsHoldOffOnceTheCallbackFaulted_SoTheMissionTickGivesBackWhatTheLineTook()
    {
        // The callback's catch latches the process off; the flag and order writes are mission-tick writes and keep working, so they must stop and pay.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");
        var refresh = code.Substring(code.IndexOf("internal void RefreshHold(", System.StringComparison.Ordinal), 260);

        StringAssert.Contains(refresh, "holdOn &= !_faulted; ReadHold(holdOn, now);");
        StringAssert.Contains(Code("Main/Adapters/MarshalFireAdapter.cs"), "public bool CallbackFaulted => MarshalWordComponent.IsFaulted;");
        StringAssert.Contains(Code("Main/Features/MarshalsWord/MarshalFireDriver.cs"), "var hold = MarshalFireTable.HoldOf(company) && !_fire.CallbackFaulted;");
    }

    [TestMethod]
    public void TheAdapter_ReleaseAll_TriesEachSoldierOnHisOwn_ForgetsThemAll_AndThrowsTheFirstFailureLast()
    {
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");
        var release = code.Substring(code.IndexOf("public void ReleaseAll()", System.StringComparison.Ordinal), 640);

        StringAssert.Contains(release, "foreach (var agent in _touched) { try { TurnOff(agent); } catch (Exception ex) { first ??= ex; } }");
        StringAssert.Contains(release, "Drop(); if (first != null) ExceptionDispatchInfo.Capture(first).Throw();");
    }

    [TestMethod]
    public void TheAdapter_ADeparture_StaysTouchedUntilItsRestoreWasMade_SoAThrowLeavesItToTheFaultCleanup()
    {
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");

        StringAssert.Contains(code, "foreach (var agent in _leaving) { TurnOff(agent); _touched.Remove(agent); }");
        Assert.IsFalse(Regex.IsMatch(code, @"_touched\.Remove\(agent\); TurnOff\(agent\);"), "forgetting before the write loses the soldier from the fault cleanup");
    }

    [TestMethod]
    public void TheAdapter_CountsAWoundedSoldiersFlagFromTheFlagWordItself_SoAStrikeBackThatWritesNothingReadsFail()
    {
        // AttackFlagOff needs the debt this component keeps, and a strike back leaves no debt, so it read the same as the settled count whatever the flag was.
        var adapter = Code("Main/Adapters/MarshalFireAdapter.cs");
        var component = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(adapter, "_struckSettled++; if (component.AttackFlagOn) _struckFlagOn++;");
        StringAssert.Contains(component, "internal bool AttackFlagOn => (Agent.GetAgentFlags() & AgentFlag.CanAttack) != 0;");
    }

    [TestMethod]
    public void TheTrialHost_ChoosesAPureCompanyFromItsSoldiers_NotFromTheClassOfItsFormation()
    {
        // FormationQuerySystem calls a company ranged when RangedUnitRatio exceeds InfantryUnitRatio, so an even mix passes a class test (QueryLibrary.cs:5-22 reads each soldier).
        var host = Code("Client/Features/MarshalsWord/Trial/CommandTrialHost.Marshal.cs");
        var pure = host.Substring(host.IndexOf("private Formation? PureInfantry()", System.StringComparison.Ordinal), 520);

        StringAssert.Contains(pure, "f.GetCountOfUnitsBelongingToPhysicalClass(FormationClass.Infantry, false) == f.CountOfUnits");
        Assert.IsFalse(pure.Contains("IsRangedFormationReadOnly"), pure);
        StringAssert.Contains(host, "MarshalSessionOfMission?.LatestSquare is { } square && square.Company == MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]) && square.LaidOut;");
    }

    [TestMethod]
    public void TheSpacingRead_ReadsTheSignificanceFirst_AsTheTrialHostDoes_SoNeitherSeesAStaleCount()
    {
        // QueryData`1.cs:18-36 evaluates an expired ratio, :73-76 returns the stored one as it stands (Formation.cs:404-414, 528-538).
        var adapter = Code("Main/Adapters/MarshalOrderAdapter.cs");
        var host = Code("Client/Features/MarshalsWord/Trial/CommandTrialHost.Marshal.cs");

        StringAssert.Contains(adapter, "formation.CountOfUnits == 0 || formation.CalculateHasSignificantNumberOfMounted || !formation.HasAnyMountedUnit");
        StringAssert.Contains(host, "_ = _chosenCompanies[company].CalculateHasSignificantNumberOfMounted; return _chosenCompanies[company].HasAnyMountedUnit;");
    }

    [TestMethod]
    public void TheHoldPass_ReadsOnlyTheHeldCompanysTouchedSoldiers_ThroughTheOccupantGate_AndSkipsAnyoneWhoIsNoLongerAi()
    {
        // The pass adds no component and turns no callback on: the 1 Hz sweep owns the touched set.
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");
        var pass = code.Substring(code.IndexOf("public IReadOnlyList<HoldCensus> Hold(", System.StringComparison.Ordinal), 1100);

        StringAssert.Contains(pass, "MissionThreadGuard.NoteCall(\"MarshalFireAdapter.Hold\", _logger.LogError);");
        StringAssert.Contains(pass, "foreach (var agent in _touched)");
        StringAssert.Contains(pass, "!AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsAIControlled) continue; component.RefreshHold(true, now);");
        Assert.IsFalse(Regex.IsMatch(pass, @"AddComponent|SetHasOnAiInputSetCallback"), pass);
    }

    [TestMethod]
    public void TheSweep_TreatsASoldierWhoIsNoLongerAiControlled_AsLeaving_SoTheReleaseGivesHisFlagAndOrderBack()
    {
        // Seat stages S4 and S5 can reach this: native stops calling the callback with the controller, so nothing in it can restore him.
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");

        StringAssert.Contains(code, "if (!agent.IsHuman || !agent.IsAIControlled || !agent.IsActive()) return;");
    }

    [TestMethod]
    public void TheComponent_GivesTheFiringOrderBackFromTheCompany_AndWritesHoldFireOnlyWhileTheLineStands()
    {
        // AddUnit and RemoveUnit overwrite a soldier's order from the formation (Formation.cs:2319, 2382), so the company's own order is the one to give back.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "var company = formation == null ? current : (int)formation.FiringOrder.OrderEnum;");
        StringAssert.Contains(code, "Agent.SetFiringOrder(hold); _firingOwed = true;");
        StringAssert.Contains(code, "Agent.SetFiringOrder(formation!.FiringOrder.OrderEnum); _firingOwed = false;");
    }

    [TestMethod]
    public void TheComponent_RefreshHold_ClearsTheFlagOnlyOutsideAStrikeBack_AndKeepsTheOrderAtHoldFireThroughIt()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "ApplyHold(want == HoldWant.FlagOff); ApplyFiringOrder(want != HoldWant.Off);");
    }

    [TestMethod]
    public void TheComponent_OnFormationSet_WritesOneFieldAndMakesNoNativeCall_AndOnHitUsesTheDamageAndTheMissileFlag()
    {
        // Agent.OnFormationSet runs inside the Formation setter, on either thread, during removal too (build-spec 9.2).
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "public override void OnFormationSet() => Volatile.Write(ref _nextHoldRead, double.NegativeInfinity);");
        StringAssert.Contains(code, "if (MarshalHoldLineRules.IsWound(damage, b.IsMissile)) Volatile.Write(ref _woundedAt, Mission.Current?.CurrentTime ?? double.NegativeInfinity);");
    }

    [TestMethod]
    public void TheComponent_UnderStopNeverAsksTheEngineForTheSlot_AndSteersOnlyWhileHeldQualifiedAndBeyondTheSlack()
    {
        // Under StandGround the member answers the soldier's own place (Formation.cs, GetOrderPositionOfUnit), and Patch30 runs on every call.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "if (!_standGround) { var place = formation.GetOrderPositionOfUnit(Agent);");
        StringAssert.Contains(code, "if (!holdOn || !_qualifies || !_slotValid || !MarshalHoldLineRules.Beyond(_slotDistance) || !MarshalHoldLineRules.Moves(inputVector.x, inputVector.y)) return; var frame = Agent.Frame;");
        StringAssert.Contains(code, "frame.rotation.TransformToLocal(");
        StringAssert.Contains(code, "inputVector = new Vec2(steered.X, steered.Y);");
    }

    [TestMethod]
    public void TheComponent_CountsTheDistanceTrendOfASteeredSoldier_FromTheFirstReplacedTick_WithNoNewNativeCall()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");
        var note = code.Substring(code.IndexOf("private void NoteTrend(", System.StringComparison.Ordinal), 640);
        note = note.Substring(0, note.IndexOf("private void ForgetSlotAndTrend()", System.StringComparison.Ordinal));

        StringAssert.Contains(code, "if (!_steered) _steerBaseline = toSlotWorld.Length; _steered = true; Clamps.Pass();");
        StringAssert.Contains(code, "_slotDistance = (float)Math.Sqrt((double)dx * dx + (double)dy * dy); NoteTrend(now);");
        StringAssert.Contains(note, "var trend = MarshalHoldLineRules.Trend(_steerBaseline, _slotDistance, _steered, MarshalHoldLineRules.StrikingBack(WoundedAt, now));");
        StringAssert.Contains(note, "if (trend == SlotTrend.Closer) _closer++; else if (trend == SlotTrend.Farther) _farther++; if (trend != SlotTrend.None) _steeredReads++;");
        StringAssert.Contains(note, "if (MarshalHoldLineRules.IsLoose(_slotDistance, _steered)) _loose++; _steered = false; _steerBaseline = float.NaN;");
        Assert.IsFalse(Regex.IsMatch(note, @"Agent\.|Mission\.|Formation|Scene"), "the trend reads stored floats and a time and nothing from the engine: " + note);
    }

    [TestMethod]
    public void TheComponent_TestsAZeroInputBeforeItReadsTheAgentFrame()
    {
        // Agent.Frame is a native read (Agent.cs:953-961); an input the clamp would leave alone never needs it.
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");
        var clamp = code.Substring(code.IndexOf("private void ClampToSlot(", System.StringComparison.Ordinal), 700);

        Assert.IsTrue(clamp.IndexOf("MarshalHoldLineRules.Moves(inputVector.x, inputVector.y)", System.StringComparison.Ordinal) > 0, clamp);
        Assert.IsTrue(clamp.IndexOf("MarshalHoldLineRules.Moves(", System.StringComparison.Ordinal) < clamp.IndexOf("Agent.Frame", System.StringComparison.Ordinal), clamp);
    }

    [TestMethod]
    public void TheComponent_ForgetsTheSlotTheDistanceAndTheCounts_WhenTheHoldEndsOrTheFormationIsGone()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "_qualifies = false; ForgetSlotAndTrend(); _nextHoldRead = double.NegativeInfinity;");
        StringAssert.Contains(code, "if (!holdOn) { _qualifies = false; ForgetSlotAndTrend(); return; }");
        StringAssert.Contains(code, "_whyNot = \"no-formation\"; ForgetSlotAndTrend(); return;");
        StringAssert.Contains(code, "_slotValid = false; _anchored = false; _slotDistance = float.NaN; _steered = false; _steerBaseline = float.NaN; _closer = 0; _farther = 0; _steeredReads = 0; _loose = 0;");
        StringAssert.Contains(code, "_slotDistance = float.NaN; _steered = false; _steerBaseline = float.NaN; return;", "a slot that reads invalid ends the steered interval");
    }

    [TestMethod]
    public void TheComponent_HandsOverEachCountOnce_SoTheNextPassStartsFromZero()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(code, "var taken = (_closer, _farther, _steeredReads, _loose); _closer = 0; _farther = 0; _steeredReads = 0; _loose = 0; return taken;");
    }

    [TestMethod]
    public void TheHoldPass_DrainsEverySoldiersTrend_BeforeTheEarlyReturnOfASoldierWhoDoesNotQualify()
    {
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");
        var drain = code.IndexOf("var (closer, farther, steered, loose) = component.TakeSlotTrend();", System.StringComparison.Ordinal);
        var early = code.IndexOf("if (!component.HoldQualifies)", System.StringComparison.Ordinal);

        Assert.IsTrue(drain > 0 && early > drain, "drain at " + drain + ", early return at " + early);
        StringAssert.Contains(code, "_closer += closer; _farther += farther; _steered += steered; _loose += loose;");
        StringAssert.Contains(code, "order, _why, _closer, _farther, _steered, _loose);");
    }

    [TestMethod]
    public void TheTrialHost_ReadsTheSameClampCounterTheComponentCounts_AndTheDriversTrendTotals()
    {
        var host = Code("Client/Features/MarshalsWord/Trial/CommandTrialHost.Marshal.cs");
        var component = Code("Main/Features/MarshalsWord/MarshalWordComponent.cs");

        StringAssert.Contains(host, "public int MarshalClamps => (int)Math.Min(int.MaxValue, Outcomes.Counter(MarshalChecks.ClampCounterId).Passes);");
        StringAssert.Contains(component, "private static readonly OutcomeCounter Clamps = Outcomes.Counter(MarshalChecks.ClampCounterId);");
        StringAssert.Contains(host, "Fire.SlotCloserOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]))");
        StringAssert.Contains(host, "Fire.SlotFartherOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]))");
        StringAssert.Contains(host, "Fire.SlotSteeredOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]))");
        StringAssert.Contains(host, "Fire.SlotFailed(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]))");
    }

    [TestMethod]
    public void TheAdapterCount_AddsNoComponent_WritesNoFlag_AndMakesNoNativeCall()
    {
        var code = Code("Main/Adapters/MarshalFireAdapter.cs");
        var count = code.Substring(code.IndexOf("public IReadOnlyList<FireCensus> Count(", System.StringComparison.Ordinal), 900);
        count = count.Substring(0, count.IndexOf("public void ReleaseAll()", System.StringComparison.Ordinal));

        Assert.IsFalse(Regex.IsMatch(count, @"AddComponent|SetAgentFlags|SetHasOnAiInputSetCallback|ApplyHold|NoteCall"), count);
        StringAssert.Contains(count, "foreach (var agent in _touched)");
    }

    [TestMethod]
    public void TheBlowCounter_CountsOnlyAMeleeBlowOfAPlayerTeamSoldierAtAnotherTeam_WithTheTeamTestedBeforeTheIndex()
    {
        // Mission.cs:5646-5671 raises OnAgentHit for a blocked blow too, from a thread native chooses; the counter is an Interlocked add.
        var code = Code("Main/Features/MarshalsWord/MarshalFireTable.cs");

        StringAssert.Contains(code, "if (!_live || isMissile || affector == null || affected == null || affector.Team == affected.Team) return;");
        StringAssert.Contains(code, "if (formation == null || affector.Team != Mission.Current?.PlayerTeam) return;");
        StringAssert.Contains(code, "var inTheLine = component != null && component.HoldQualifies;");
        StringAssert.Contains(code, "if (component != null && MarshalHoldLineRules.BlowIsStrikeBack(inTheLine, component.WoundedAt, Mission.Current.CurrentTime)) CountStrikeBlow(formation.Index); else if (MarshalHoldLineRules.BlowCountsForLine(inTheLine, HoldOf(formation.Index))) CountBlow(formation.Index);");
        StringAssert.Contains(code, "Interlocked.Increment(ref BlowsDealt[company]);");
        StringAssert.Contains(code, "Interlocked.Increment(ref StrikeBlowsDealt[company]);");
    }

    [TestMethod]
    public void TheTable_CountsBlowsPerCompany_ReadsAnOutOfRangeCompanyAsZero_AndBeginAndClearZeroThem()
    {
        MarshalFireTable.Begin();
        MarshalFireTable.CountBlow(2);
        MarshalFireTable.CountBlow(2);
        MarshalFireTable.CountBlow(-1);
        MarshalFireTable.CountBlow(MarshalFireTable.Companies);
        MarshalFireTable.CountStrikeBlow(2);
        MarshalFireTable.CountStrikeBlow(-1);
        MarshalFireTable.CountStrikeBlow(MarshalFireTable.Companies);
        MarshalFireTable.SetHold(2, true);

        Assert.AreEqual(2, MarshalFireTable.BlowsOf(2));
        Assert.AreEqual(1, MarshalFireTable.StrikeBlowsOf(2), "a strike back blow is counted apart from the blows the flag should stop");
        Assert.AreEqual(0, MarshalFireTable.StrikeBlowsOf(MarshalFireTable.Companies));
        Assert.AreEqual(0, MarshalFireTable.BlowsOf(3));
        Assert.AreEqual(0, MarshalFireTable.BlowsOf(MarshalFireTable.Companies));
        Assert.IsTrue(MarshalFireTable.HoldOf(2));
        Assert.IsFalse(MarshalFireTable.HoldOf(-1));

        MarshalFireTable.Clear();
        Assert.AreEqual(0, MarshalFireTable.BlowsOf(2));
        Assert.AreEqual(0, MarshalFireTable.StrikeBlowsOf(2));
        Assert.IsFalse(MarshalFireTable.HoldOf(2));

        MarshalFireTable.CountBlow(2);
        MarshalFireTable.SetHold(2, true);
        MarshalFireTable.Begin();
        Assert.AreEqual(0, MarshalFireTable.BlowsOf(2));
        Assert.IsFalse(MarshalFireTable.HoldOf(2));
        MarshalFireTable.Clear();
    }

    [TestMethod]
    public void TheSession_AKeyPressOfHoldTheLine_ReachesTheDriverAtTheNextMissionTick_AndTheHollowSquareSettingIsCopiedEachFrame()
    {
        MarshalHooks.Clear();
        var orders = new FakeMarshalOrders();
        orders.Selected.Add(3);
        var settings = new FakeMarshalSettings();
        var session = new MarshalSession(orders, new FakeMarshalFire(), new FakeMarshalOutlines(), new FakeMarshalScreen(), settings, () => true,
            new RecordingOutcomeLog(), Substitute.For<IModLogger>(), "7");
        try
        {
            session.OnMissionTick();
            Assert.IsTrue(MarshalHooks.HollowSquare, "copied when the session goes live");

            settings.HollowSquare = false;
            session.OnScreenTick(new MarshalScreenInput(false, false, false, 1f, holdPressed: true));
            Assert.IsFalse(MarshalHooks.HollowSquare);
            Assert.IsFalse(MarshalFireTable.HoldOf(3), "an engine write waits for the mission tick");

            session.OnMissionTick();
            Assert.IsTrue(MarshalFireTable.HoldOf(3));
        }
        finally
        {
            session.Finish();
            MarshalHooks.Clear();
        }

        Assert.IsFalse(MarshalHooks.HollowSquare, "the finish clears what a patch reads");
    }
}
