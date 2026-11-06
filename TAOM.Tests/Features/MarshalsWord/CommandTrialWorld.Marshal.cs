using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Marshal's Word's half of the stand-in: companies that walk to an order point, a queue that holds a Move behind a walk, an array that keeps
/// or loses its offsets, a march, archers that hold and loose, a mark and the outlines. Each knob breaks one fact of one step, and a world
/// with every knob at its default is a feature that works.
/// </summary>
internal sealed partial class CommandTrialWorld
{
    internal sealed class FakeCompany
    {
        internal float X;
        internal float Y;
        internal float Units = 40;
        internal bool Line = true;
        internal float Speed = 4f;
        internal float? TargetX;
        internal float? TargetY;
        internal string Order = "Stop";
        internal readonly List<(float X, float Y)> Queue = new();
        internal bool WaitStep;
        internal bool Running;
        internal bool Ranged;
    }

    internal readonly List<FakeCompany> Companies = new()
    {
        new FakeCompany { X = 0f, Y = 0f, Units = 40, Ranged = true },
        new FakeCompany { X = 40f, Y = 0f, Units = 30 },
    };
    internal bool MarshalSessionLive = true;
    internal bool MarshalSessionFaulted;
    internal bool QueueKeyArms = true;
    internal bool QueueCaptures = true;
    internal bool DirectOrderClearsTheQueue = true;
    internal bool ArrayKeepsTheOffsets = true;
    internal bool ArrayIsReported = true;
    internal bool PlanExists = true;
    internal string MarchEnds = "pass";
    internal bool MarchNeverEnds;
    internal bool MarkKeyArms = true;
    internal bool MarkTakes = true;
    internal bool MarkIsRead = true;
    internal bool MarkIsJudged = true;
    internal int MarkSampled = 8;
    internal int MarkOnTarget = 6;
    internal MarkContext? MarkContextOverride;

    /// <summary>The enemy company the mark steps wait for starts this many metres away and closes at <see cref="EnemyClosingSpeed"/> a second.</summary>
    internal float EnemyStartMetres = 100f;

    internal float EnemyClosingSpeed = 5f;
    internal bool WidthsKeyArms = true;
    internal bool ArchersExist = true;
    internal bool ArchersHold = true;
    internal bool HoldVerdictPasses = true;
    internal bool WordLoosesThem = true;
    internal bool FireKeyWorks = true;
    internal bool OutlinesPaint = true;

    /// <summary>What the archers do at free fire in the control window: this many shots a second, from this many different soldiers.</summary>
    internal float FreeShotsPerSecond = 6f;

    internal int FreeShooters = 12;

    /// <summary>The census numbers the volley step reads beside the holders.</summary>
    internal int IdleCount = 21;

    internal int AsideCount = 3;
    internal bool OutlinesClearWhenTheStepCloses = true;
    internal bool StepsEndByPatience;
    internal int EnemyUnits = 90;
    internal bool PhantomQueuedStep;
    internal float ArrayShiftScale = 1f;

    /// <summary>The host plans a shorter drag than the step asked for.</summary>
    internal float FrontDragShiftScale = 1f;
    internal int? VerdictHolding;
    internal bool StaleReleaseVerdict;
    internal bool WordIsLost;
    internal bool MarkAlsoCharges;
    internal bool StepsEndOtherwise;
    internal bool PanelShutsWhenTheKeyIsHeld;

    /// <summary>The engine's default: an open order panel asks for a quarter of the mission's speed (MissionGauntletSingleplayerOrderUIHandler.cs:215-218).</summary>
    internal bool SlowDownOnOrder = true;

    /// <summary>The solver computes and reports the layout, and the controller never applies it.</summary>
    internal bool ArrayLayoutIsNeverApplied;

    private readonly List<int> _chosen = new();
    private readonly Dictionary<int, (float X, float Y)> _points = new();
    private readonly HashSet<int> _selected = new();
    private bool _queueHeld;
    private bool _markHeld;
    private bool _widthsHeld;
    private bool _enemyChosen;
    private int _finished;
    private int _byPatience;
    private int _byArrival;
    private int _captured;
    private int _arrays;
    private int _concluded;
    private string _conclusion = "none";
    private float _marchLeft = float.NaN;
    private int _outlines;
    private bool _markedEnemy;
    private float _markedAt = float.NaN;
    private float _missionClock;
    private (float X, float Y) _dragShift;
    private readonly Dictionary<int, FireMode> _modes = new();
    private VolleyVerdict? _holdVerdict;
    private VolleyVerdict? _releaseVerdict;
    private float _holdSince = float.NaN;
    private float _wordAt = float.NaN;
    private int _held;
    private bool _watching;
    private float _watchedShots;

    /// <summary>Whether the trial still holds key 540, 541 or 542; a step that ends must leave none held.</summary>
    internal bool AnyKeyHeld => _queueHeld || _markHeld || _widthsHeld;

    public bool MarshalLive => MarshalSessionLive;

    public bool MarshalFaulted => MarshalSessionFaulted;

    private void MarshalFrame(float dt)
    {
        var slowed = dt * (SlowDownOnOrder && PanelOpen ? 0.25f : 1f);
        _missionClock += slowed;
        foreach (var company in Companies)
            Walk(company, slowed);
        AdvanceQueues();
        if (!float.IsNaN(_marchLeft) && (_marchLeft -= dt) <= 0f)
            EndMarch();
        _outlines = !MarshalSessionLive ? 0 : (_selected.Count > 0 && PanelOpen && OutlinesPaint) ? 30 : (OutlinesClearWhenTheStepCloses ? 0 : _outlines);
        AdvanceArchers(dt);
        AdvanceMassed();
        AdvanceHold(dt);
        AdvanceThrowers(dt);
        if (_watching && ArchersExist && MarshalFireMode(0) == FireMode.Free)
            _watchedShots += FreeShotsPerSecond * dt;
    }

    private static void Walk(FakeCompany company, float dt)
    {
        if (company.TargetX == null || company.TargetY == null)
            return;
        var dx = company.TargetX.Value - company.X;
        var dy = company.TargetY.Value - company.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        var step = company.Speed * dt;
        if (length <= step)
        {
            company.X = company.TargetX.Value;
            company.Y = company.TargetY.Value;
            return;
        }

        company.X += dx / length * step;
        company.Y += dy / length * step;
    }

    private static bool At(FakeCompany company) =>
        company.TargetX != null && Math.Sqrt(Math.Pow(company.X - company.TargetX.Value, 2) + Math.Pow(company.Y - company.TargetY!.Value, 2)) <= MarshalTuning.ArrivalSlack;

    private void AdvanceQueues()
    {
        foreach (var company in Companies)
        {
            if (company.Running && At(company))
            {
                company.Running = false;
                _finished++;
                if (StepsEndByPatience)
                    _byPatience++;
                else if (!StepsEndOtherwise)
                    _byArrival++;
            }

            if (company.WaitStep && (At(company) || company.TargetX == null))
            {
                company.WaitStep = false;
                _finished++;
                if (!StepsEndOtherwise)
                    _byArrival++;
            }

            if (!company.WaitStep && !company.Running && company.Queue.Count > 0)
            {
                var next = company.Queue[0];
                company.Queue.RemoveAt(0);
                company.TargetX = next.X;
                company.TargetY = next.Y;
                company.Order = "Move";
                company.Running = true;
            }
        }
    }

    private void EndMarch()
    {
        _marchLeft = float.NaN;
        _concluded++;
        _conclusion = MarchEnds;
    }

    private void AdvanceArchers(float dt)
    {
        if (!ArchersExist || !_modes.TryGetValue(0, out var mode) || mode == FireMode.Free)
        {
            _held = 0;
            _holdSince = float.NaN;
            return;
        }

        if (ArchersHold)
            _held = 12;
        if (float.IsNaN(_holdSince) && _held >= MarshalTuning.VolleyMinHolding)
            _holdSince = Time;
        if (_holdVerdict == null && !float.IsNaN(_holdSince) && Time - _holdSince >= MarshalTuning.HoldWindowSeconds)
            _holdVerdict = new VolleyVerdict(VolleyCheck.Hold, HoldVerdictPasses, VerdictHolding ?? _held, HoldVerdictPasses ? 0 : 4);
        if (!float.IsNaN(_wordAt) && Time - _wordAt >= MarshalTuning.WordJitter + MarshalTuning.ReleaseWindowPadSeconds)
        {
            _releaseVerdict = new VolleyVerdict(VolleyCheck.Release, WordLoosesThem, _held, WordLoosesThem ? _held : 1);
            _releasesJudged++;
            NameTheWordOfTheRelease();
            _wordAt = float.NaN;
        }
    }

    // ---- companies ----

    public int MarshalChooseCompanies(int count)
    {
        _chosen.Clear();
        if (Companies.Count < count)
            return 0;
        for (var i = 0; i < count; i++)
            _chosen.Add(i);
        return count;
    }

    public int MarshalChooseArchers()
    {
        _chosen.Clear();
        if (!ArchersExist)
            return 0;
        var index = Companies.FindIndex(c => c.Ranged);
        if (index < 0)
            return 0;
        _chosen.Add(index);
        return (int)Companies[index].Units;
    }

    public void MarshalSelect(int first, int count)
    {
        Note("marshal-select");
        _selected.Clear();
        foreach (var index in _chosen.Skip(first).Take(count))
            _selected.Add(index);
    }

    public void MarshalHoldQueue(bool held)
    {
        _queueHeld = held;
        // The static the patch reads clears at the next screen tick after the panel closes, so a read between the two sees a key still armed over a shut panel.
        if (held && PanelShutsWhenTheKeyIsHeld)
            _panelIsOpen = false;
    }

    public void MarshalHoldMark(bool held)
    {
        _markHeld = held;
        if (held && PanelShutsWhenTheKeyIsHeld)
            _panelIsOpen = false;
    }

    public void MarshalHoldWidths(bool held) => _widthsHeld = held;

    public void MarshalReleaseKeys()
    {
        _queueHeld = false;
        _markHeld = false;
        _widthsHeld = false;
    }

    public bool MarshalQueueArmed => _queueHeld && QueueKeyArms && (PanelOpen || PanelShutsWhenTheKeyIsHeld);

    public bool MarshalMarkArmed => _markHeld && MarkKeyArms && (PanelOpen || PanelShutsWhenTheKeyIsHeld);

    public bool MarshalWidthsArmed => _widthsHeld && WidthsKeyArms && PanelOpen;

    public bool MarshalPlanPoint(int slot, int company, float forward, float right)
    {
        var c = Companies[_chosen[company]];
        _points[slot] = (c.X + forward, c.Y + right);
        return true;
    }

    public void MarshalMoveTo(int slot)
    {
        Note("marshal-move");
        var point = _points[slot];
        var captured = MarshalQueueArmed && QueueCaptures;
        if (captured)
            _captured++;
        foreach (var index in _selected)
        {
            var company = Companies[index];
            if (captured)
            {
                company.WaitStep |= company.Queue.Count == 0 && !company.Running && company.TargetX != null && !At(company);
                company.Queue.Add(point);
                continue;
            }

            _markedEnemy = false;
            if (DirectOrderClearsTheQueue)
                company.Queue.Clear();
            company.WaitStep = false;
            company.Running = false;
            company.TargetX = point.X;
            company.TargetY = point.Y;
            company.Order = "Move";
        }

        if (!captured)
            TheEngineClosesThePanel();
    }

    // A direct order reaches the player's controller's OnOrderIssued, whose panel listener ends by closing the panel unless it was opened by
    // hold (MissionOrderTroopControllerVM.cs:827). A captured order never reaches it, so the panel stays open then.
    private void TheEngineClosesThePanel() => _panelIsOpen = false;

    public FrontDrag? MarshalPlanFrontDrag(float shiftMetres)
    {
        var a = Companies[_chosen[0]];
        var b = Companies[_chosen[1]];
        if (Math.Abs(b.Y - a.Y) + Math.Abs(b.X - a.X) <= 0f)
            return null;
        // The stand-in faces +X, so the front runs along Y, to the group's right (-Y).
        _dragShift = (0f, -shiftMetres * FrontDragShiftScale);
        return new FrontDrag(Math.Abs(a.X - b.X), _dragShift.X, _dragShift.Y);
    }

    /// <summary>Whether the arrival plan was made while a chosen company was still on its way somewhere.</summary>
    internal bool PlannedWhileWalking;

    public TrialDrag? MarshalPlanArrivalDrag()
    {
        PlannedWhileWalking = Enumerable.Range(0, _chosen.Count).Any(company => !MarshalSettled(company));
        return PlanExists && TheSecondStandsInTheColumn() ? new TrialDrag(0f, 0f, 10f, 0f, 40f, 100f) : null;
    }

    /// <summary>Two companies that stand closer than the column have no walk at about 2.5 to 1 and 17 s long, so the stand-in finds no plan for them.</summary>
    private bool TheSecondStandsInTheColumn()
    {
        var first = Companies[_chosen[0]];
        var second = Companies[_chosen[1]];
        return Math.Abs(second.X - (first.X - MarshalTrialRules.ColumnBehindMetres)) <= MarshalTuning.ArrivalSlack
            && Math.Abs(second.Y - (first.Y + MarshalTrialRules.ColumnSideMetres)) <= MarshalTuning.ArrivalSlack;
    }

    public void MarshalDrag()
    {
        Note("marshal-drag");
        ApplySquareDrag();
        TheEngineClosesThePanel();
        _marchLeft = MarchNeverEnds ? float.NaN : 30f;
        var arrangeSideBySide = !ArrayKeepsTheOffsets;
        foreach (var index in _selected)
        {
            var company = Companies[index];
            // The solver keeps the array the player last ordered (6.2): a company on its way keeps its destination, a company that stands keeps its place.
            if (ArrayLayoutIsNeverApplied)
                continue;
            company.TargetX = arrangeSideBySide ? Companies[_selected.First()].X + (index - _selected.First()) * 20f : (company.TargetX ?? company.X) + _dragShift.X * ArrayShiftScale;
            company.TargetY = arrangeSideBySide ? Companies[_selected.First()].Y : (company.TargetY ?? company.Y) + _dragShift.Y * ArrayShiftScale;
        }

        if (ArrayIsReported)
            _arrays++;
    }

    public void MarshalStandGround()
    {
        Note("marshal-stand");
        TheEngineClosesThePanel();
        foreach (var index in _selected)
        {
            _markedEnemy = false;
            Companies[index].Order = "Stop";
            Companies[index].TargetX = null;
            Companies[index].TargetY = null;
        }
    }

    public float MarshalDistanceToPoint(int company, int slot)
    {
        var c = Companies[_chosen[company]];
        if (!_points.TryGetValue(slot, out var p))
            return float.NaN;
        return (float)Math.Sqrt((c.X - p.X) * (c.X - p.X) + (c.Y - p.Y) * (c.Y - p.Y));
    }

    /// <summary>The archers' walk never ends: the order point is not reached, whatever the time.</summary>
    internal bool ArchersCannotSettle;

    public bool MarshalSettled(int company)
    {
        var c = Companies[_chosen[company]];
        return !(ArchersCannotSettle && c.Ranged) && (c.TargetX == null || At(c));
    }

    public float MarshalAhead(int from, int to) => Companies[_chosen[to]].X - Companies[_chosen[from]].X;

    public Vec3f? MarshalOrderedOffset(int from, int to)
    {
        var a = Companies[_chosen[from]];
        var b = Companies[_chosen[to]];
        return new Vec3f((b.TargetX ?? b.X) - (a.TargetX ?? a.X), (b.TargetY ?? b.Y) - (a.TargetY ?? a.Y), 0f);
    }

    public Vec3f? MarshalOrderPoint(int company)
    {
        var c = Companies[_chosen[company]];
        return new Vec3f(c.TargetX ?? c.X, c.TargetY ?? c.Y, 0f);
    }

    public Vec3f? MarshalOffset(int from, int to)
    {
        var a = Companies[_chosen[from]];
        var b = Companies[_chosen[to]];
        return new Vec3f(b.X - a.X, b.Y - a.Y, 0f);
    }

    public bool MarshalInLine(int company) => Companies[_chosen[company]].Line;

    public float MarshalSpeed(int company) => Companies[_chosen[company]].Speed;

    public int MarshalQueuedSteps(int company) =>
        Companies[_chosen[company]].Queue.Count + (Companies[_chosen[company]].WaitStep ? 1 : 0) + (Companies[_chosen[company]].Running ? 1 : 0) + (PhantomQueuedStep ? 1 : 0);

    public int MarshalStepsCaptured => _captured;

    public int MarshalStepsFinished => _finished;

    public int MarshalStepsByArrival => _byArrival;

    public int MarshalStepsByPatience => _byPatience;

    public int MarshalArraysSolved => _arrays;

    public string MarshalMarchConclusion => _conclusion;

    public int MarshalMarchesConcluded => _concluded;

    public int MarshalChooseEnemy()
    {
        _enemyChosen = EnemyUnits > 0;
        return EnemyUnits;
    }

    public int MarshalChooseNearestEnemy() => MarshalChooseEnemy();

    public float MarshalPlanApproach(int slot, int company, float reachMetres)
    {
        if (!_enemyChosen)
            return float.NaN;
        var c = Companies[_chosen[company]];
        var walk = MarshalTrialRules.ApproachWalk(MarshalEnemyDistance(company), reachMetres, c.Speed, EnemyClosingSpeed);
        _points[slot] = (c.X + walk, c.Y);
        return walk;
    }

    public void MarshalWatchBegin(int company)
    {
        _watching = true;
        _watchedShots = 0f;
    }

    public void MarshalWatchEnd() => _watching = false;

    public int MarshalWatchShots => (int)_watchedShots;

    public int MarshalWatchShooters => _watchedShots > 0f ? FreeShooters : 0;

    public int MarshalIdle(int company) => MarshalFireMode(company) != FireMode.Free ? IdleCount : 0;

    public int MarshalAside(int company) => MarshalFireMode(company) != FireMode.Free ? AsideCount : 0;

    public float MarshalEnemyDistance(int company) => _enemyChosen ? Math.Max(10f, EnemyStartMetres - EnemyClosingSpeed * Time) : float.NaN;

    public void MarshalChargeEnemy()
    {
        Note("marshal-charge");
        if (!_enemyChosen)
            return;
        _chargedAt = Time;
        if (MarshalMarkArmed && MarkTakes)
        {
            _markedEnemy = true;
            _markedAt = _missionClock;
            if (MarkAlsoCharges)
            {
                foreach (var marked in _selected)
                    Companies[marked].Order = "Charge";
            }

            return;
        }

        TheEngineClosesThePanel();
        foreach (var index in _selected)
            Companies[index].Order = "Charge";
    }

    public bool MarshalMarkedEnemy(int company) => _markedEnemy;

    public string MarshalOrderName(int company) => Companies[_chosen[company]].Order;

    public MarkSample? MarshalMarkRead(int company)
    {
        if (!_markedEnemy || !MarkIsRead)
            return null;
        var context = MarkContextOverride ?? (Companies[_chosen[company]].Ranged ? MarkContext.RangedOnStop : MarkContext.Other);
        return new MarkSample(0, 1, MarkOnTarget, MarkSampled, context);
    }

    // The watch judges a mark once it has stood MarkSettleSeconds and its next pass comes (mission seconds, so an open panel's slow time counts).
    public MarkVerdict? MarshalMarkJudged(int company)
    {
        var sample = MarshalMarkRead(company);
        if (sample == null || !MarkIsJudged || _missionClock - _markedAt < MarshalTuning.MarkSettleSeconds + MarshalTuning.MarkPassSeconds)
            return null;
        return MarshalMarkRules.Judgeable(sample.Value) ? new MarkVerdict(sample.Value, MarshalMarkRules.Aims(sample.Value)) : null;
    }

    public void MarshalPressFire()
    {
        if (!FireKeyWorks)
            return;
        _modes[0] = MarshalFireRules.NextMode(MarshalFireMode(0));
        _holdVerdict = null;
        _holdSince = float.NaN;
        _massedAt = float.NaN;
    }

    public void MarshalPressWord()
    {
        if (WordIsLost)
            return;
        if (MarshalFireMode(0) == FireMode.OnWord)
            _wordAt = Time;
    }

    public FireMode MarshalFireMode(int company) => _modes.TryGetValue(company, out var mode) ? mode : FireMode.Free;

    public int MarshalHolding(int company) => _held;

    public VolleyVerdict? MarshalHoldVerdict => _holdVerdict;

    public VolleyVerdict? MarshalReleaseVerdict => _releaseVerdict ?? (StaleReleaseVerdict ? new VolleyVerdict(VolleyCheck.Release, true, 12, 12) : null);

    public int MarshalOutlinesPainted => _outlines;
}
