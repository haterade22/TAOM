using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Client.Features.MarshalsWord;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal sealed partial class CommandTrialHost
{
    private readonly List<Formation> _chosenCompanies = new();
    private readonly Dictionary<int, WorldPosition> _points = new();
    private WorldPosition _dragBegin;
    private WorldPosition _dragEnd;
    private Formation? _enemyCompany;
    private MarshalView? _marshalView;

    private MarshalView? MarshalViewOfMission => _marshalView ??= _mission.GetMissionBehavior<MarshalView>();

    private MarshalSession? MarshalSessionOfMission => MarshalViewOfMission?.Session;

    private OrderController? PlayerController => _mission.PlayerTeam?.PlayerOrderController;

    public bool MarshalLive => MarshalSessionOfMission is { IsLive: true };

    public bool MarshalFaulted => (MarshalSessionOfMission?.Faulted ?? false) || MarshalWordComponent.IsFaulted;

    public int MarshalChooseCompanies(int count)
    {
        _chosenCompanies.Clear();
        var team = _mission.PlayerTeam;
        if (team == null)
            return 0;
        var hero = _mission.MainAgent;
        var picked = team.FormationsIncludingEmpty.Where(f => f.CountOfUnits > 0 && !(hero != null && hero.Formation == f))
            .OrderByDescending(f => f.CountOfUnits).Take(count).OrderBy(MarshalOrderAdapter.CompanyOf).ToList();
        if (picked.Count < count)
            return 0;
        _chosenCompanies.AddRange(picked);
        return count;
    }

    public int MarshalChooseArchers()
    {
        _chosenCompanies.Clear();
        var archers = _mission.PlayerTeam?.FormationsIncludingEmpty.Where(f => f.CountOfUnits > 0 && f.QuerySystem.IsRangedFormationReadOnly)
            .OrderByDescending(f => f.CountOfUnits).FirstOrDefault();
        if (archers == null)
            return 0;
        _chosenCompanies.Add(archers);
        return archers.CountOfUnits;
    }

    public void MarshalSelect(int first, int count)
    {
        NoteWrite("CommandTrialHost.MarshalSelect");
        var controller = PlayerController;
        if (controller == null)
            return;
        controller.ClearSelectedFormations();
        foreach (var formation in _chosenCompanies.Skip(first).Take(count))
            controller.SelectFormation(formation);
    }

    public void MarshalHoldQueue(bool held) => MarshalViewOfMission?.TrialHoldQueue(held);

    public void MarshalHoldMark(bool held) => MarshalViewOfMission?.TrialHoldMark(held);

    public void MarshalHoldWidths(bool held) => MarshalViewOfMission?.TrialHoldWidths(held);

    public void MarshalReleaseKeys()
    {
        MarshalHoldQueue(false);
        MarshalHoldMark(false);
        MarshalHoldWidths(false);
    }

    public bool MarshalQueueArmed => MarshalHooks.QueueArmed;

    public bool MarshalMarkArmed => MarshalHooks.MarkArmed;

    public bool MarshalWidthsArmed => MarshalHooks.KeepWidthsHeld;

    public bool MarshalPlanPoint(int slot, int company, float forward, float right)
    {
        if (company >= _chosenCompanies.Count)
            return false;
        var formation = _chosenCompanies[company];
        var direction = formation.Direction;
        var at = formation.CurrentPosition;
        _points[slot] = GroundPosition(at.x + direction.x * forward + direction.y * right, at.y + direction.y * forward - direction.x * right);
        return true;
    }

    public void MarshalMoveTo(int slot)
    {
        NoteWrite("CommandTrialHost.MarshalMoveTo");
        if (_points.TryGetValue(slot, out var point))
            PlayerController?.SetOrderWithPosition(OrderType.Move, point);
    }

    public FrontDrag? MarshalPlanFrontDrag(float shiftMetres)
    {
        var group = ArrayCompaniesOfChosen();
        var front = MarshalTrialRules.FrontLine(group);
        if (front == null || group.Length < 2)
            return null;
        var shifted = MarshalTrialRules.ShiftAlong(front.Value, shiftMetres);
        if (shifted == null)
            return null;
        var line = shifted.Value;
        _dragBegin = GroundPosition(line.BeginX, line.BeginY);
        _dragEnd = GroundPosition(line.EndX, line.EndY);
        var first = group[0];
        var stagger = System.Math.Abs((group[1].X - first.X) * first.DirX + (group[1].Y - first.Y) * first.DirY);
        return new FrontDrag(stagger, line.ShiftX, line.ShiftY);
    }

    public TrialDrag? MarshalPlanArrivalDrag()
    {
        var group = ArrayCompaniesOfChosen();
        var speed = _chosenCompanies.Count == 0 ? 0f : _chosenCompanies.Min(f => f.CachedMovementSpeed);
        var plan = MarshalTrialRules.PlanArrival(group, speed, ManagedParameters.Instance.GetManagedParameter(ManagedParametersEnum.BipedalRadius));
        if (plan == null)
            return null;
        _dragBegin = GroundPosition(plan.Value.BeginX, plan.Value.BeginY);
        _dragEnd = GroundPosition(plan.Value.EndX, plan.Value.EndY);
        return plan;
    }

    public void MarshalDrag()
    {
        NoteWrite("CommandTrialHost.MarshalDrag");
        PlayerController?.SetOrderWithTwoPositions(OrderType.MoveToLineSegment, _dragBegin, _dragEnd);
    }

    public void MarshalStandGround()
    {
        NoteWrite("CommandTrialHost.MarshalStandGround");
        PlayerController?.SetOrder(OrderType.StandYourGround);
    }

    public float MarshalDistanceToPoint(int company, int slot) =>
        company < _chosenCompanies.Count && _points.TryGetValue(slot, out var point) ? _chosenCompanies[company].CurrentPosition.Distance(point.AsVec2) : float.NaN;

    public bool MarshalSettled(int company)
    {
        var formation = company < _chosenCompanies.Count ? _chosenCompanies[company] : null;
        return formation != null && formation.CountOfUnitsWithoutDetachedOnes > 0
            && (!formation.OrderPositionIsValid || formation.CurrentPosition.Distance(formation.OrderPosition) <= MarshalTuning.ArrivalSlack);
    }

    public float MarshalAhead(int from, int to)
    {
        if (from >= _chosenCompanies.Count || to >= _chosenCompanies.Count)
            return float.NaN;
        var origin = _chosenCompanies[from];
        var direction = origin.Direction;
        var offset = _chosenCompanies[to].CurrentPosition - origin.CurrentPosition;
        return offset.x * direction.x + offset.y * direction.y;
    }

    public Vec3f? MarshalOrderedOffset(int from, int to)
    {
        if (from >= _chosenCompanies.Count || to >= _chosenCompanies.Count)
            return null;
        var a = MarshalArrayLayout.Read(_chosenCompanies[from]);
        var b = MarshalArrayLayout.Read(_chosenCompanies[to]);
        return new Vec3f(b.X - a.X, b.Y - a.Y, 0f);
    }

    public Vec3f? MarshalOffset(int from, int to)
    {
        if (from >= _chosenCompanies.Count || to >= _chosenCompanies.Count)
            return null;
        var a = _chosenCompanies[from].CurrentPosition;
        var b = _chosenCompanies[to].CurrentPosition;
        return new Vec3f(b.x - a.x, b.y - a.y, 0f);
    }

    public Vec3f? MarshalOrderPoint(int company)
    {
        if (company >= _chosenCompanies.Count)
            return null;
        var place = MarshalArrayLayout.Read(_chosenCompanies[company]);
        return new Vec3f(place.X, place.Y, 0f);
    }

    public bool MarshalInLine(int company) =>
        company < _chosenCompanies.Count && _chosenCompanies[company].ArrangementOrder.OrderEnum == ArrangementOrder.ArrangementOrderEnum.Line;

    public float MarshalSpeed(int company) => company < _chosenCompanies.Count ? _chosenCompanies[company].CachedMovementSpeed : float.NaN;

    public int MarshalQueuedSteps(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.Queue.CountOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? 0 : 0;

    public int MarshalStepsCaptured => MarshalSessionOfMission?.StepsCaptured ?? 0;

    public int MarshalStepsFinished => MarshalSessionOfMission?.StepsFinished.Finished ?? 0;

    public int MarshalStepsByPatience => MarshalSessionOfMission?.StepsFinished.ByPatience ?? 0;

    public int MarshalStepsByArrival => MarshalSessionOfMission?.StepsFinished.ByArrival ?? 0;

    public int MarshalArraysSolved => MarshalSessionOfMission?.ArraysSolved ?? 0;

    public string MarshalMarchConclusion => MarshalSessionOfMission?.MarchConclusion ?? "none";

    public int MarshalMarchesConcluded => MarshalSessionOfMission?.MarchesConcluded ?? 0;

    public int MarshalChooseEnemy()
    {
        _enemyCompany = _mission.PlayerEnemyTeam?.FormationsIncludingEmpty.Where(f => f.CountOfUnits > 0).OrderByDescending(f => f.CountOfUnits).FirstOrDefault();
        return _enemyCompany?.CountOfUnits ?? 0;
    }

    public int MarshalChooseNearestEnemy()
    {
        var at = _chosenCompanies.Count == 0 ? default : _chosenCompanies[0].CurrentPosition;
        _enemyCompany = _chosenCompanies.Count == 0
            ? null
            : _mission.PlayerEnemyTeam?.FormationsIncludingEmpty.Where(f => f.CountOfUnits > 0).OrderBy(f => f.CurrentPosition.DistanceSquared(at)).FirstOrDefault();
        return _enemyCompany?.CountOfUnits ?? 0;
    }

    public float MarshalPlanApproach(int slot, int company, float reachMetres)
    {
        if (company >= _chosenCompanies.Count || _enemyCompany == null || _enemyCompany.CountOfUnits <= 0)
            return float.NaN;
        var own = _chosenCompanies[company];
        var from = own.CurrentPosition;
        var to = _enemyCompany.CurrentPosition;
        var gap = from.Distance(to);
        var walk = MarshalTrialRules.ApproachWalk(gap, reachMetres, own.CachedMovementSpeed, _enemyCompany.CachedMovementSpeed);
        if (float.IsNaN(walk) || gap < 0.001f)
            return float.NaN;
        var share = walk / gap;
        _points[slot] = GroundPosition(from.x + (to.x - from.x) * share, from.y + (to.y - from.y) * share);
        return walk;
    }

    public void MarshalWatchBegin(int company)
    {
        if (company < _chosenCompanies.Count)
            MarshalFireTable.BeginWatch(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]));
    }

    public void MarshalWatchEnd() => MarshalFireTable.EndWatch();

    public int MarshalWatchShots => MarshalFireTable.Watched.Shots;

    public int MarshalWatchShooters => MarshalFireTable.Watched.Shooters;

    public int MarshalAside(int company) => CensusOfChosen(company).Aside;

    public int MarshalIdle(int company) => CensusOfChosen(company).Idle;

    public void MarshalChargeEnemy()
    {
        NoteWrite("CommandTrialHost.MarshalChargeEnemy");
        if (_enemyCompany != null)
            PlayerController?.SetOrderWithFormation(OrderType.Charge, _enemyCompany);
    }

    public float MarshalEnemyDistance(int company) =>
        company < _chosenCompanies.Count && _enemyCompany != null && _enemyCompany.CountOfUnits > 0
            ? _chosenCompanies[company].CurrentPosition.Distance(_enemyCompany.CurrentPosition)
            : float.NaN;

    public bool MarshalMarkedEnemy(int company) =>
        company < _chosenCompanies.Count && _enemyCompany != null && _chosenCompanies[company].TargetFormation == _enemyCompany;

    public string MarshalOrderName(int company) =>
        company < _chosenCompanies.Count ? _chosenCompanies[company].GetReadonlyMovementOrderReference().OrderEnum.ToString() : "none";

    public MarkSample? MarshalMarkRead(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.MarkReadOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) : null;

    public MarkVerdict? MarshalMarkJudged(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.MarkJudgedOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) : null;

    public void MarshalPressFire() => MarshalViewOfMission?.TrialPressFire();

    public void MarshalPressWord() => MarshalViewOfMission?.TrialPressWord();

    public FireMode MarshalFireMode(int company) =>
        company < _chosenCompanies.Count ? MarshalFireTable.ModeOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) : FireMode.Free;

    public int MarshalHolding(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.Fire.HoldingOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? 0 : 0;

    public VolleyVerdict? MarshalHoldVerdict => MarshalSessionOfMission?.Fire.LastHold;

    public VolleyVerdict? MarshalReleaseVerdict => MarshalSessionOfMission?.Fire.LastRelease;

    public int MarshalOutlinesPainted => MarshalSessionOfMission?.OutlinesPainted ?? 0;

    public int MarshalMassedWords => MarshalSessionOfMission?.Fire.MassedWords ?? 0;

    public int MarshalReleasesJudged => MarshalSessionOfMission?.Fire.ReleasesJudged ?? 0;

    public MassedWord? MarshalLastMassed => MarshalSessionOfMission?.Fire.LastMassed;

    public int MarshalDrawing(int company) => CensusOfChosen(company).Drawing;

    // The player's Transfer order, as the order panel gives it. Formation.TransferUnits is the AI's split: it passes isPlayerOrder false (OrderController.cs:1628), and
    // TransferUnitsAux returns at once for a company the player owns (Formation.cs:2157, IsSplittableByAI at :314, IsAIOwned false for a player general at :326-342).
    // The order passes true (OrderController.cs:942), and an empty target copies the source's control (Formation.cs:2169, :2714).
    private void PlayerTransfer(Formation source, Formation target, int count)
    {
        var controller = PlayerController;
        if (controller == null)
            return;
        controller.ClearSelectedFormations();
        controller.SelectFormation(source);
        controller.SetOrderWithFormationAndNumber(OrderType.Transfer, target, count);
    }

    public int MarshalSplitInfantry()
    {
        NoteWrite("CommandTrialHost.MarshalSplitInfantry");
        var source = PureInfantry();
        var team = _mission.PlayerTeam;
        var target = team?.FormationsIncludingEmpty.FirstOrDefault(f => f.CountOfUnits == 0 && f != _mission.MainAgent?.Formation);
        if (source == null || target == null || source.CountOfUnits < 2 * MarshalTuning.HoldControlMinBlows)
            return 0;
        PlayerTransfer(source, target, source.CountOfUnits / 2);
        _chosenCompanies.Clear();
        _chosenCompanies.Add(source);
        _chosenCompanies.Add(target);
        return _chosenCompanies.Count;
    }

    public bool MarshalControllable(int company) => company < _chosenCompanies.Count && _chosenCompanies[company].CountOfUnits > 0 && !_chosenCompanies[company].IsAIControlled;

    public void MarshalPressHold() => MarshalViewOfMission?.TrialPressHold();

    public bool MarshalHeld(int company) => company < _chosenCompanies.Count && MarshalFireTable.HoldOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]));

    public int MarshalBlows(int company) => company < _chosenCompanies.Count ? MarshalFireTable.BlowsOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) : 0;

    public int MarshalStrikeBlows(int company) => company < _chosenCompanies.Count ? MarshalFireTable.StrikeBlowsOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) : 0;

    public int MarshalFlagOff(int company)
    {
        var off = 0;
        if (company < _chosenCompanies.Count)
            _chosenCompanies[company].ApplyActionOnEachUnit(agent => off += (agent.GetAgentFlags() & AgentFlag.CanAttack) == 0 ? 1 : 0);
        return off;
    }

    public int MarshalEnemiesNear(int company, float metres)
    {
        if (company >= _chosenCompanies.Count || _mission.PlayerEnemyTeam == null)
            return 0;
        var at = _chosenCompanies[company].CurrentPosition;
        var near = 0;
        foreach (var enemy in _mission.PlayerEnemyTeam.FormationsIncludingEmpty)
            enemy.ApplyActionOnEachUnit(agent => near += agent.Position.AsVec2.Distance(at) <= metres ? 1 : 0);
        return near;
    }

    public HoldVerdict? MarshalHoldLineVerdict => MarshalSessionOfMission?.Fire.HoldLine.LastVerdict;

    public void MarshalOrderShieldWall()
    {
        NoteWrite("CommandTrialHost.MarshalOrderShieldWall");
        PlayerController?.SetOrder(OrderType.ArrangementCloseOrder);
    }

    public HoldCensus MarshalHoldCensus(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.Fire.HoldLine.CensusOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? default : default;

    public bool MarshalStrikePassed(int company) =>
        company < _chosenCompanies.Count && (MarshalSessionOfMission?.Fire.HoldLine.StrikePassed(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? false);

    public int MarshalSlotCloser(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.Fire.HoldLine.SlotCloserOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? 0 : 0;

    public int MarshalSlotFarther(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.Fire.HoldLine.SlotFartherOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? 0 : 0;

    public int MarshalSlotSteered(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.Fire.HoldLine.SlotSteeredOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? 0 : 0;

    public bool MarshalSlotFailed(int company) =>
        company < _chosenCompanies.Count && (MarshalSessionOfMission?.Fire.HoldLine.SlotFailed(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? false);

    public bool MarshalSquareLaidOut(int company) =>
        company < _chosenCompanies.Count && MarshalSessionOfMission?.LatestSquare is { } square && square.Company == MarshalOrderAdapter.CompanyOf(_chosenCompanies[company]) && square.LaidOut;

    public int MarshalClamps => (int)Math.Min(int.MaxValue, Outcomes.Counter(MarshalChecks.ClampCounterId).Passes);

    public float MarshalOrderGap(int company)
    {
        var formation = company < _chosenCompanies.Count ? _chosenCompanies[company] : null;
        return formation == null || !formation.OrderPositionIsValid ? 0f : formation.CurrentPosition.Distance(formation.OrderPosition);
    }

    public int MarshalChoosePureInfantry()
    {
        _chosenCompanies.Clear();
        var infantry = PureInfantry();
        if (infantry == null)
            return 0;
        _chosenCompanies.Add(infantry);
        return infantry.CountOfUnits;
    }

    public void MarshalOrderSquare()
    {
        NoteWrite("CommandTrialHost.MarshalOrderSquare");
        PlayerController?.SetOrder(OrderType.ArrangementSchiltron);
    }

    public bool MarshalPlainSquare(int company) => PlainSquareOf(company) != null;

    public string MarshalArrangementName(int company) => company < _chosenCompanies.Count ? _chosenCompanies[company].Arrangement?.GetType().Name ?? "none" : "none";

    public float MarshalSquareSide(int company) => PlainSquareOf(company)?.Width ?? float.NaN;

    public float MarshalSquareMinimum(int company) => PlainSquareOf(company)?.MinimumWidth ?? float.NaN;

    public float MarshalSquareMaximum(int company) => PlainSquareOf(company)?.MaximumWidth ?? float.NaN;

    public float MarshalUnitWidth(int company) => company < _chosenCompanies.Count ? _chosenCompanies[company].UnitDiameter + _chosenCompanies[company].Interval : float.NaN;

    public int MarshalUnits(int company) => company < _chosenCompanies.Count ? _chosenCompanies[company].CountOfUnits : 0;

    public int MarshalPlaced(int company) => PlainSquareOf(company)?.PositionedUnitCount ?? 0;

    public void MarshalPlanSideDrag(int company, float side)
    {
        if (company >= _chosenCompanies.Count)
            return;
        var formation = _chosenCompanies[company];
        var at = formation.CurrentPosition;
        var direction = formation.Direction;
        var halfX = direction.y * side / 2f;
        var halfY = -direction.x * side / 2f;
        _dragBegin = GroundPosition(at.x - halfX, at.y - halfY);
        _dragEnd = GroundPosition(at.x + halfX, at.y + halfY);
    }

    public bool MarshalKillOne(int company)
    {
        Agent? last = null;
        if (company < _chosenCompanies.Count)
            _chosenCompanies[company].ApplyActionOnEachUnit(agent => last = agent);
        if (last == null)
            return false;
        Kill(last);
        return true;
    }

    public int MarshalMoveOneRider()
    {
        NoteWrite("CommandTrialHost.MarshalMoveOneRider");
        var infantry = _chosenCompanies.Count == 0 ? null : _chosenCompanies[0];
        var riders = _mission.PlayerTeam?.FormationsIncludingEmpty.Where(f => f.CountOfUnits > 0 && f.QuerySystem.CavalryUnitRatioReadOnly > 0.5f)
            .OrderByDescending(f => f.CountOfUnits).FirstOrDefault();
        if (infantry == null || riders == null)
            return 0;
        PlayerTransfer(riders, infantry, 1);
        return infantry.CountOfUnits;
    }

    public SpacingReading? MarshalSpacingVerdict => MarshalSessionOfMission?.SpacingVerdict;

    public string MarshalSpacingWhy => MarshalSpacingPatch.WhyNot;

    // The count behind the ratio, taken fresh: GetCachedValueUnlessTooOld returns the stored ratio whatever its age (QueryData`1.cs:73-76).
    public int MarshalRiders(int company) => company < _chosenCompanies.Count
        ? _chosenCompanies[company].GetCountOfUnitsBelongingToPhysicalClass(FormationClass.Cavalry, false)
            + _chosenCompanies[company].GetCountOfUnitsBelongingToPhysicalClass(FormationClass.HorseArcher, false)
        : 0;

    public SpacingRead? MarshalSpacingOfChosen(int company) => company < _chosenCompanies.Count ? MarshalOrderAdapter.SpacingOf(_chosenCompanies[company]) : null;

    // The significance read goes first: its non-ReadOnly accessor evaluates an expired ratio (QueryData`1.cs:18-36), and HasAnyMountedUnit reads the stored ratio as it stands (Formation.cs:404-414).
    public bool MarshalHasAnyRider(int company)
    {
        if (company >= _chosenCompanies.Count)
            return false;
        _ = _chosenCompanies[company].CalculateHasSignificantNumberOfMounted;
        return _chosenCompanies[company].HasAnyMountedUnit;
    }

    public bool MarshalHasSignificantRiders(int company) => company < _chosenCompanies.Count && _chosenCompanies[company].CalculateHasSignificantNumberOfMounted;

    // Every soldier is infantry: a company is ranged when its ranged ratio passes its infantry ratio, so an even mix passes a class test (QueryLibrary.cs reads each soldier).
    private Formation? PureInfantry()
    {
        var hero = _mission.MainAgent;
        return _mission.PlayerTeam?.FormationsIncludingEmpty
            .Where(f => f.CountOfUnits > 0 && !(hero != null && hero.Formation == f)
                && f.GetCountOfUnitsBelongingToPhysicalClass(FormationClass.Infantry, false) == f.CountOfUnits)
            .OrderByDescending(f => f.CountOfUnits).FirstOrDefault();
    }

    private IFormationArrangement? PlainSquareOf(int company)
    {
        var arrangement = company < _chosenCompanies.Count ? _chosenCompanies[company].Arrangement : null;
        return arrangement != null && arrangement.GetType() == typeof(SquareFormation) ? arrangement : null;
    }

    private FireCensus CensusOfChosen(int company) =>
        company < _chosenCompanies.Count ? MarshalSessionOfMission?.Fire.CensusOf(MarshalOrderAdapter.CompanyOf(_chosenCompanies[company])) ?? default : default;

    private ArrayCompany[] ArrayCompaniesOfChosen() => _chosenCompanies.Select(MarshalArrayLayout.Read).ToArray();

    private WorldPosition GroundPosition(float x, float y) =>
        new(_mission.Scene, new Vec3(x, y, _adapter.GroundHeight(x, y, GroundProbeStartHeight), -1f));
}
