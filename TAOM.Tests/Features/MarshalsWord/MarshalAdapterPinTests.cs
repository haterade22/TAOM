using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// What the engine-facing adapters of Marshal's Word must keep doing, pinned as source text because a test cannot build a mission to run them
/// in. Each pin names the engine line that makes the code necessary, so a reader who wants to delete it can check the line first.
/// </summary>
[TestClass]
public class MarshalAdapterPinTests
{
    private const string OrderAdapter = "Main/Adapters/MarshalOrderAdapter.cs";
    private const string ArrayLayout = "Main/Features/MarshalsWord/Hooks/MarshalArrayLayout.cs";
    private const string Module = "Main/Features/MarshalsWord/MarshalsWordModule.cs";
    private const string ScreenAdapter = "Client/Adapters/MarshalScreenAdapter.cs";
    private const string Payload = "Main/Adapters/MarshalOrderPayload.cs";

    // Comments stripped and every run of white space made one space, so a pin survives a reformat but not a changed statement.
    private static string Code(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    private static string Member(string path, string signature)
    {
        var code = Code(path);
        var start = code.IndexOf(signature, System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, path + " has no " + signature);
        var next = Regex.Match(code.Substring(start + signature.Length), @" (public|private|internal|protected) ");
        return next.Success ? code.Substring(start, signature.Length + next.Index) : code.Substring(start);
    }

    // ---- the march reads what a company is doing (build-spec 6.4) ----

    [TestMethod]
    public void ReadMarch_ACompanyThatLeftItsMove_ReportsAShiftOverTheLimit_BecauseChargeStopAndRetreatMoveNoOrderPoint()
    {
        // MovementOrder.cs:178 gives Charge, Stop and Retreat an invalid position, :552 hands it back and :690-691 applies a position to a Move
        // only; Formation.cs:862 ignores an invalid position and :2485-2490 is the tick that offers it. So OrderPosition stays where it was.
        var shift = Member(OrderAdapter, "private static float TargetShift(");

        StringAssert.Contains(shift, "formation.IsAIControlled || formation.GetReadonlyMovementOrderReference().OrderEnum != MovementOrder.MovementOrderEnum.Move ? float.MaxValue");
        StringAssert.Contains(shift, ": formation.OrderPosition.Distance(new Vec2(target.X, target.Y))");
        StringAssert.Contains(Member(OrderAdapter, "public IReadOnlyList<MarchLeg> ReadMarch("), "formation.CachedMovementSpeed, TargetShift(formation, target)));");
    }

    [TestMethod]
    public void ReadMarch_AnEmptiedCompany_LeavesTheMarch_BecauseTheTeamNoLongerTicksIt()
    {
        // Team.cs:616-622 ticks a formation only while CountOfUnits > 0, so an emptied company keeps its last position and a stale ETA.
        var read = Member(OrderAdapter, "public IReadOnlyList<MarchLeg> ReadMarch(");

        StringAssert.Contains(read, "if (formation.CountOfUnits == 0) continue;");
    }

    [TestMethod]
    public void ReadMarch_AMissingFormation_StillReportsANonFiniteLeg_SoTheMarchEndsInvalid()
    {
        var read = Member(OrderAdapter, "public IReadOnlyList<MarchLeg> ReadMarch(");

        StringAssert.Contains(read, "if (formation == null) { legs.Add(new MarchLeg(target.Company, float.NaN, 1f, 0f)); continue; }");
    }

    // ---- arrival (build-spec 5.4) ----

    [TestMethod]
    public void Arrived_NeedsUndetachedUnits_BecauseALineOrderSkipsACompanyWithNone()
    {
        // OrderController.cs:990-998 sends a line order only to formations with CountOfUnitsWithoutDetachedOnes > 0, so a company with none keeps
        // its old order point, and an old point it stands on would otherwise read as an arrival that no order caused.
        var arrived = Member(OrderAdapter, "public bool Arrived(int company)");

        StringAssert.Contains(arrived, "formation.OrderPositionIsValid && formation.CountOfUnitsWithoutDetachedOnes > 0 && formation.CurrentPosition.Distance(formation.OrderPosition) <= MarshalTuning.ArrivalSlack");
    }

    // ---- a queued drag keeps the widths key it was given with (build-spec 6.2) ----

    [TestMethod]
    public void Issue_SetsTheWidthsKeyForTheCall_AndRestoresItInAFinally()
    {
        // The commit reads MarshalHooks.KeepWidthsHeld inside the array prefix. The key as it stands when a step starts is not the key the
        // player held when the drag was given, so the payload carries its own and the call borrows it.
        var issue = Member(OrderAdapter, "public bool Issue(");

        StringAssert.Contains(issue, "var keptWidths = MarshalHooks.KeepWidthsHeld; try {");
        StringAssert.Contains(issue, "MarshalHooks.KeepWidthsHeld = order.KeepWidths; order.Apply(master);");
        StringAssert.Contains(issue, "finally { MarshalHooks.KeepWidthsHeld = keptWidths; master.ClearSelectedFormations(); }");
    }

    // ---- the flag of a line (build-spec 5.5) ----

    [TestMethod]
    public void TryPoint_ALine_AsksTheGroundAtItsMiddlePoint_AndFallsBackToTheEndsAverage()
    {
        // The Seat asks the ground the same way (SeatScreenAdapter.cs:83): the probe starts 1.5 m above the asked Z and casts down, with a terrain
        // fallback, and the native call answers 0 when nothing is there.
        var point = Member(Payload, "private Vec3 ReadPoint(");

        StringAssert.Contains(point, "var z = FlagHeight((a.z + b.z) / 2f, Mission.Current?.Scene?.GetGroundHeightAtPosition(new Vec3(x, y, Math.Max(a.z, b.z), -1f), BodyFlags.CommonCollisionExcludeFlags));");
    }

    [TestMethod]
    public void TryPoint_AMoveOrALine_ReadsItsGroundPointOnce_BecauseEachReadOfAReadonlyFieldProbesNativeCodeAgain()
    {
        // _first and _second are readonly fields of a non-readonly struct (WorldPosition.cs:8), so GetGroundVec3 runs on a defensive copy:
        // ValidateZ writes its result into the copy (WorldPosition.cs:105-111) and the next call pays WorldPositionValidateZ again. A drag's
        // end point is built with hasValidZ false and stays Invalid (WorldPosition.cs:98-102). Dropping readonly would fix only part of that
        // and would change the State that Apply hands to vanilla, so the point is read once and kept. An orderable stays live: an entity moves.
        var point = Member(Payload, "internal bool TryPoint(");

        StringAssert.Contains(point, "_point ??= ReadPoint()");
        Assert.IsFalse(point.Contains("GetGroundVec3"), "the ground is read in ReadPoint alone");
        StringAssert.Contains(point, "missionObject.GameEntity.GlobalPosition");
        Assert.IsFalse(point.Contains("_point ??= missionObject"), "an entity can move, so an orderable's point is not kept");
        Assert.AreEqual(2, Regex.Matches(Member(Payload, "private Vec3 ReadPoint("), "GetGroundVec3").Count, "the first end, and the second for a line");
        StringAssert.Contains(Code(Payload), "private readonly WorldPosition _first; private readonly WorldPosition _second;");
    }

    // ---- who is ordered (build-spec 5.3, rule 2) ----

    [TestMethod]
    public void Issue_SelectsOnlyWhatTheMasterControllerCanSelect_BecauseSelectFormationAssertsOnTheRest()
    {
        // OrderController.cs:105-125 (v1.5.6-202611041234): SelectFormation selects a formation that is not selected and passes
        // IsFormationSelectable, and its else branch is a FailedAssert. The public IsFormationSelectable (:157-160, :174-181) is the very
        // test it consults, and with the master's null owner it asks for one living unit, so the guard cannot drift from the engine's.
        var issue = Member(OrderAdapter, "public bool Issue(");

        StringAssert.Contains(issue, "if (formation != null && !master.SelectedFormations.Contains(formation) && master.IsFormationSelectable(formation)) master.SelectFormation(formation);");
    }

    [TestMethod]
    public void Issue_WithNothingSelected_ReturnsBeforeAnyOrder_BecauseSetOrderWithOrderableObjectIndexesTheFirstSelection()
    {
        // OrderController.cs:1010 reads SelectedFormations[0] with no test, so an empty selection inside Mission.Tick would throw.
        var issue = Member(OrderAdapter, "public bool Issue(");

        StringAssert.Contains(issue, "if (master.SelectedFormations.Count == 0) return false;");
        Assert.IsTrue(issue.IndexOf("if (master.SelectedFormations.Count == 0) return false;", System.StringComparison.Ordinal)
            < issue.IndexOf("order.Apply(master);", System.StringComparison.Ordinal), "the test comes before the order");
    }

    [TestMethod]
    public void IsControllable_NeedsUnitsAndNoAiControl_BecauseAStepOnAnAiCompanyOrdersNobody()
    {
        // OrderController.cs:722-727: BeforeSetOrder takes a company that has a player owner off AI control, so the queue tests it before each start.
        StringAssert.Contains(Member(OrderAdapter, "public bool IsControllable("),
            "return formation != null && formation.CountOfUnits > 0 && !formation.IsAIControlled;");
    }

    // ---- keep the array (build-spec 6.1 to 6.3) ----

    [TestMethod]
    public void TryTakeOver_RefusesDeploymentFewerThanTwoCompaniesAndAnotherTeam()
    {
        // 6.1: Deployment has its own position rules (OrderController.cs, SimulateNewOrderWithFrameAndWidth), a takeover needs two or more
        // companies, and every one must be the player's team's.
        var take = Member(ArrayLayout, "internal static bool TryTakeOver(");

        StringAssert.Contains(take, "if (mission == null || mission.Mode == MissionMode.Deployment) return false;");
        StringAssert.Contains(take, "if (group.Count < 2 || !group.All(f => IsOurs(f, mission.PlayerTeam))) return false;");
        StringAssert.Contains(Member(ArrayLayout, "private static bool IsOurs("), "f.Team == playerTeam && !(f.IsAIControlled && f.PlayerOwner == null) && f.CountOfUnitsWithoutDetachedOnes > 0;");
    }

    [TestMethod]
    public void PlaceOf_IsTheOrderPointOnlyWhileTheCompanyHolds_ElseItsCurrentPosition()
    {
        // 6.2: a second order during a walk keeps the array the player last ordered, because the order point stands for the place while it holds one.
        StringAssert.Contains(Member(ArrayLayout, "private static Vec2 PlaceOf("),
            "f.OrderPositionIsValid && f.GetReadonlyMovementOrderReference().MovementState == MovementOrder.MovementStateEnum.Hold ? f.OrderPosition : f.CurrentPosition;");
        StringAssert.Contains(Member(ArrayLayout, "private static ArrayCompany Read("), "var place = PlaceOf(f);");
    }

    [TestMethod]
    public void Build_AColumnPassesItsOwnFlankWidth_BecauseACustomWidthThatDiffersTransposesIt()
    {
        // 6.3: the long line branch applies FormOrder.FormOrderCustom to every company (OrderController.cs:1576), and a custom width that differs
        // from the column's own by over 0.01 transposes a column (FormOrder.cs:89-93).
        StringAssert.Contains(Member(ArrayLayout, "private static void Build("), "var width = IsColumn(f) ? f.Arrangement.FlankWidth : placement.Width;");
    }

    // ---- the module and the flags (build-spec 3 and 5.5) ----

    [TestMethod]
    public void OnPhase_RegistersTheKeysAtProcessLoadOnly_AndNeverLetsAFailureEscape()
    {
        // After Native has cleared and filled the key contexts, as the Seat does. A failure leaves the keys unbound; an escape would fault the module.
        var phase = Member(Module, "public override void OnPhase(");

        StringAssert.Contains(phase, "if (phase != ApplyPhase.ProcessLoad) return; try { TaomMarshalHotKeyCategory.Register(); } catch (Exception ex) {");
    }

    [TestMethod]
    public void FlagAt_BuildsTheFlagAsTheOrderPlacerDoes_FromTheOrderFlagSmallMetaMesh()
    {
        // OrderTroopPlacer.cs:503-511 (v1.5.6-202611041234): an empty entity, not affected by the season, holding a copy of the order_flag_small
        // metamesh. That name is a metamesh and not a prefab, and a prefab lookup was the defect build-spec Deviations row 2 records.
        var flag = Member(ScreenAdapter, "private GameEntity FlagAt(");

        StringAssert.Contains(flag, "var entity = GameEntity.CreateEmpty(_mission.Scene); entity.EntityFlags |= EntityFlags.NotAffectedBySeason; entity.AddComponent(MetaMesh.GetCopy(\"order_flag_small\")); entity.SetVisibilityExcludeParents(visible: false);");
    }

    // ---- the fit test (build-spec 6.3) ----

    [TestMethod]
    public void Build_OnlyTheFitTestReplacesTheSimulationCopy_AndTheFramesUseTheDictionarysCopy()
    {
        // OrderController.cs:1336-1339 replaces the copy inside the fit function only; the frames at :1329 and :1410 use the dictionary's copy,
        // which syncs its spacing (Formation.cs:3121). A replacement kept for the frames costs a new Formation per company per preview frame.
        var build = Member(ArrayLayout, "private static void Build(");

        StringAssert.Contains(build, "var fit = simulation != null && simulation.UnitSpacing != f.UnitSpacing ? new Formation(null, -1) : simulation;");
        StringAssert.Contains(build, "f.GetUnitPositionWithIndexAccordingToNewOrder(fit, unitIndex, in position, in direction, width, f.UnitSpacing - reduction, out var last, out _, out actualWidth);");
        StringAssert.Contains(build, "f.GetUnitPositionWithIndexAccordingToNewOrder(simulation, index, in position, in direction, width, f.UnitSpacing - reduction, out var unitPosition, out _);");
        Assert.IsFalse(Regex.IsMatch(build, @"\bsimulation = new Formation"), "the replacement belongs to the fit local alone");
    }

    [TestMethod]
    public void Build_TheFitLoop_StaysOnThePublicOverloadWithActualWidth_BecausePatch92OpensItsScopeThere()
    {
        var build = Member(ArrayLayout, "private static void Build(");

        StringAssert.Contains(build, "out var last, out _, out actualWidth);");
    }
}
