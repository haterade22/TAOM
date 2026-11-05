using System;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The stand-in for Massed fire, hold the line, the hollow square and the spacing repair. Each knob breaks one fact of one step, and a world with every knob at its default
/// is a feature that works. The hold's blows and the square's counts follow the rules the engine follows: a flag that works stops the blows, a plain square keeps its side.
/// </summary>
internal sealed partial class CommandTrialWorld
{
    // ---- Massed ----

    internal bool MassedGivesWord = true;
    internal int MassedHoldingAtWord = 25;
    internal int MassedEligibleAtWord = 30;
    internal bool MassedReleaseLoosesThem = true;

    /// <summary>The word's count rises when it is given, and its numbers are named only when its release window is judged.</summary>
    internal bool MassedNumbersComeWithTheRelease;

    private int _massedWords;
    private int _releasesJudged;
    private MassedWord? _lastMassed;
    private float _massedAt = float.NaN;
    private MassedWord? _unnamedWord;

    public int MarshalMassedWords => _massedWords;

    public int MarshalReleasesJudged => _releasesJudged;

    public MassedWord? MarshalLastMassed => _lastMassed;

    public int MarshalDrawing(int company) => MarshalFireMode(company) == FireMode.Free ? 0 : 4;

    private void AdvanceMassed()
    {
        if (!_modes.TryGetValue(0, out var mode) || mode != FireMode.Massed || !MassedGivesWord || _held < MarshalTuning.VolleyMinHolding || !float.IsNaN(_massedAt))
            return;
        _held = MassedHoldingAtWord;
        _massedAt = Time;
        _massedWords++;
        var word = new MassedWord(MassedHoldingAtWord, MassedEligibleAtWord, 0, Time);
        if (MassedNumbersComeWithTheRelease)
            _unnamedWord = word;
        else
            _lastMassed = word;
        _wordAt = Time;
    }

    private void NameTheWordOfTheRelease()
    {
        _lastMassed ??= _unnamedWord;
        _unnamedWord = null;
    }

    // ---- hold the line ----

    internal bool SplitWorks = true;

    /// <summary>The order was the AI's split, which the engine refuses for a company the player owns (Formation.cs:2157), so the control company stays empty.</summary>
    internal bool SplitLeavesTheControlEmpty;
    internal bool CompaniesAreControllable = true;
    internal bool HoldFlagWorks = true;
    internal bool HoldFlagIsRestored = true;
    internal bool HoldKeyWorks = true;

    /// <summary>The shield wall order reaches the held company.</summary>
    internal bool ShieldWallOrderWorks = true;

    /// <summary>The soldiers qualify for the line once the wall stands under Stop.</summary>
    internal bool LineQualifies = true;

    internal float QualifyAfterSeconds = 1f;
    internal int EnemiesNearCount = 40;

    /// <summary>The enemy reaches the line this long after the hold went on; nothing sends for it, because a charge ends the hold.</summary>
    internal float EnemiesArriveAfterSeconds = 10f;

    internal float ControlBlowsPerSecond = 3f;

    /// <summary>The control fights at a trickle for this long after the enemy arrives, as the control of run 20261105-010922 did before its fight began.</summary>
    internal float ControlQuietSeconds;

    /// <summary>The control's fight ends this long after the enemy arrives, as the production control's did (run 20261104-225226): a window opened later reads it quiet.</summary>
    internal float ControlFightSeconds = 12f;

    internal float HeldBlowsPerSecondWithoutTheFlag = 3f;
    internal int FlagOffCount = 30;

    /// <summary>The driver closes its judged window, as the feature's own check does.</summary>
    internal bool DriverJudgesHoldLine = true;

    internal bool StrikeBackWorks = true;

    /// <summary>No soldier of the line ever lands a blow inside a strike back window.</summary>
    internal bool StrikeBlowsNever;

    /// <summary>The first wounded soldiers die blowless, and the line lands its blows this long after the first wound, as the soldiers of a later wound would.</summary>
    internal float LaterWoundStrikesAfterSeconds = float.NaN;

    /// <summary>The flag reads off and the soldiers strike all the same, as a flag the native side ignores would.</summary>
    internal bool HeldCompanyStillStrikes;

    /// <summary>The soldiers' flags come back, and the driver's own strike check never passes.</summary>
    internal bool StrikeCheckStaysSilent;
    internal bool TheLineIsNeverWounded;
    internal float WoundedAfterSeconds = 3f;
    internal bool SlotsAreKept = true;
    internal int FarSoldiersWhenSlotsAreLost = 12;

    /// <summary>The slot clamp never replaces an input, as a build without it would.</summary>
    internal bool ClampSteersNobody;

    /// <summary>The steered soldiers end their reads farther from their slot, as a clamp with the wrong sign would.</summary>
    internal bool ClampWalksTheWrongWay;

    /// <summary>Steered hold reads a second once the clamp acts, split between the two sides by the clamp's sign.</summary>
    internal float SteeredReadsPerSecond = 10f;

    /// <summary>The driver's own slot check fails this long after the clamp first steers, as a later window of the line can.</summary>
    internal float DriverSlotFailsAfterSteeringSeconds = float.NaN;

    /// <summary>Seconds after the enemy arrives before the clamp replaces its first input.</summary>
    internal float ClampStartsAfterSeconds = 0.5f;

    /// <summary>Inputs the process had already replaced before the step began; the counter is the process's, not the mission's.</summary>
    internal int ClampsBeforeTheStep = 100;

    /// <summary>A charge gives the flag back to every soldier.</summary>
    internal bool ChargeRestoresTheFlag = true;

    /// <summary>Seconds, real, between the charge order and the flags reading on.</summary>
    internal float RestoreLagSeconds = 0.7f;

    private readonly bool[] _holdOn = new bool[8];
    private float _holdAt = float.NaN;
    private bool _shieldWallStands;
    private float _chargedAt = float.NaN;
    private float _heldBlows;
    private float _strikeBlows;
    private bool _strikeLatched;
    private float _controlBlows;
    private bool _flagRestoredNow;
    private float _baselineAt = float.NaN;
    private float _baselineHeld;
    private float _baselineControl;
    private HoldVerdict? _driverVerdict;
    private float _clampsMade;
    private float _closerReads;
    private float _fartherReads;
    private float _firstClampAt = float.NaN;

    /// <summary>The world time at which the clamp first replaced an input, or NaN.</summary>
    internal float FirstClampAt => _firstClampAt;

    public int MarshalSplitInfantry()
    {
        Note("marshal-split");
        if (!SplitWorks)
            return 0;
        var source = Companies.FindIndex(c => !c.Ranged);
        if (source < 0)
            return 0;
        Companies[source].Units = SplitLeavesTheControlEmpty ? Companies[source].Units : Companies[source].Units / 2;
        Companies.Add(new FakeCompany { X = Companies[source].X + 10f, Y = Companies[source].Y, Units = SplitLeavesTheControlEmpty ? 0 : Companies[source].Units });
        _chosen.Clear();
        _chosen.Add(source);
        _chosen.Add(Companies.Count - 1);
        return _chosen.Count;
    }

    public bool MarshalControllable(int company) => CompaniesAreControllable && company < _chosen.Count && Companies[_chosen[company]].Units > 0;

    public void MarshalOrderShieldWall()
    {
        Note("marshal-wall");
        if (ShieldWallOrderWorks && _selected.Contains(_chosen[0]))
            _shieldWallStands = true;
    }

    public void MarshalPressHold()
    {
        if (!HoldKeyWorks)
            return;
        var first = _selected.Count > 0 ? _selected.First() : -1;
        if (first < 0)
            return;
        _holdOn[first] = !_holdOn[first];
        _flagRestoredNow = !_holdOn[first];
        _holdAt = _holdOn[first] ? Time : float.NaN;
    }

    public bool MarshalHeld(int company) => company < _chosen.Count && _holdOn[_chosen[company]];

    public int MarshalBlows(int company) => (int)(company == 0 ? _heldBlows : _controlBlows);

    public int MarshalStrikeBlows(int company) => company == 0 ? (int)_strikeBlows : 0;

    // Qualified and the flag off follow the line: the wall under Stop, a charge ending both after the restore lag.
    private bool LineStands(int company)
    {
        if (company != 0 || company >= _chosen.Count || !_holdOn[_chosen[0]] || !LineQualifies || !_shieldWallStands)
            return false;
        if (Companies[_chosen[0]].Order == "Charge")
            return !ChargeRestoresTheFlag ? true : !float.IsNaN(_chargedAt) && Time - _chargedAt < RestoreLagSeconds;
        return Time - _holdAt >= QualifyAfterSeconds;
    }

    public int MarshalFlagOff(int company) =>
        company == 0 && LineStands(0) && HoldFlagWorks ? FlagOffCount
        : company == 0 && !HoldFlagIsRestored && _flagRestoredNow ? FlagOffCount
        : 0;

    public int MarshalEnemiesNear(int company, float metres) =>
        !float.IsNaN(_holdAt) && Time - _holdAt >= EnemiesArriveAfterSeconds ? EnemiesNearCount : 0;

    public HoldVerdict? MarshalHoldLineVerdict => _driverVerdict;

    public HoldCensus MarshalHoldCensus(int company)
    {
        if (company != 0 || company >= _chosen.Count || !_holdOn[_chosen[0]] || float.IsNaN(_holdAt))
            return default;
        var units = (int)Companies[_chosen[0]].Units;
        var qualified = LineStands(0) ? units : 0;
        var arrived = Time - _holdAt - EnemiesArriveAfterSeconds;
        var woundedAge = arrived - WoundedAfterSeconds;
        var wounded = !TheLineIsNeverWounded && woundedAge >= 0f && qualified > 0;
        var struck = wounded && woundedAge < MarshalTuning.StrikeBackSeconds;
        var settled = struck && woundedAge >= 2f * (float)MarshalTuning.HoldReadSeconds;
        return new HoldCensus(Company: _chosen[0], Units: units, Qualified: qualified, FlagOff: MarshalFlagOff(0), Anchored: qualified,
            Far: arrived > 0f && !SlotsAreKept ? FarSoldiersWhenSlotsAreLost : 0, WoundedEver: wounded ? 5 : 0, Struck: struck ? 5 : 0, StruckSettled: settled ? 5 : 0,
            StruckFlagOn: settled && StrikeBackWorks ? 5 : 0, SlotMax: 0.5f, SlotMean: 0.1f, Order: Companies[_chosen[0]].Order, WhyNot: qualified > 0 ? "" : "arrangement");
    }

    public bool MarshalStrikePassed(int company) => _strikeLatched;

    public int MarshalClamps => ClampsBeforeTheStep + (int)_clampsMade;

    public int MarshalSlotCloser(int company) => company == 0 ? (int)_closerReads : 0;

    public int MarshalSlotFarther(int company) => company == 0 ? (int)_fartherReads : 0;

    public int MarshalSlotSteered(int company) => company == 0 ? (int)(_closerReads + _fartherReads) : 0;

    public bool MarshalSlotFailed(int company) =>
        company == 0 && MarshalHoldCensus(0).WoundedEver > 0 && ((ClampWalksTheWrongWay && _closerReads + _fartherReads >= MarshalTuning.SlotMinSteered)
            || (!float.IsNaN(_firstClampAt) && !float.IsNaN(DriverSlotFailsAfterSteeringSeconds) && Time - _firstClampAt >= DriverSlotFailsAfterSteeringSeconds));

    public bool MarshalSquareLaidOut(int company) => SquareLaidOut && MarshalPlainSquare(company);

    public float MarshalOrderGap(int company) => company < _chosen.Count && !MarshalSettled(company) ? 40f : 0f;

    private void AdvanceHold(float dt)
    {
        if (_chosen.Count < 1 || float.IsNaN(_holdAt) || Time - _holdAt < EnemiesArriveAfterSeconds)
            return;
        var sinceArrival = Time - _holdAt - EnemiesArriveAfterSeconds;
        if (!ClampSteersNobody && sinceArrival >= ClampStartsAfterSeconds)
        {
            _clampsMade += 20f * dt;
            _firstClampAt = float.IsNaN(_firstClampAt) ? Time : _firstClampAt;
            _closerReads += (ClampWalksTheWrongWay ? 0.2f : 0.8f) * SteeredReadsPerSecond * dt;
            _fartherReads += (ClampWalksTheWrongWay ? 0.8f : 0.2f) * SteeredReadsPerSecond * dt;
        }

        if (sinceArrival <= ControlFightSeconds)
            _controlBlows += (sinceArrival < ControlQuietSeconds ? 0.5f : ControlBlowsPerSecond) * dt;
        if (!LineStands(0) || !HoldFlagWorks || HeldCompanyStillStrikes)
            _heldBlows += HeldBlowsPerSecondWithoutTheFlag * dt;
        else if (StrikeBackWorks && !StrikeBlowsNever && LineLandsABlow(sinceArrival))
            _strikeBlows += dt;
        _strikeLatched |= MarshalHoldCensus(0).StruckSettled > 0 && StrikeBackWorks && !StrikeCheckStaysSilent;
        AdvanceDriverWindow();
    }

    private bool LineLandsABlow(float sinceArrival) =>
        float.IsNaN(LaterWoundStrikesAfterSeconds)
            ? MarshalHoldCensus(0).Struck > 0
            : sinceArrival - WoundedAfterSeconds >= LaterWoundStrikesAfterSeconds;

    // The driver's own window: a baseline once the flags read off and the control has struck, the verdict a proof length later. A control too quiet to prove anything takes a new baseline.
    private void AdvanceDriverWindow()
    {
        if (!DriverJudgesHoldLine || (_driverVerdict != null && _driverVerdict.Value.Proof != HoldProof.NoControl) || _controlBlows <= 0f)
            return;
        if (float.IsNaN(_baselineAt))
        {
            if (MarshalFlagOff(0) < MarshalTuning.HoldMinFlagOff)
                return;
            _baselineAt = Time;
            _baselineHeld = _heldBlows;
            _baselineControl = _controlBlows;
            return;
        }

        if (Time - _baselineAt < MarshalTuning.HoldProofSeconds)
            return;
        var held = (int)(_heldBlows - _baselineHeld);
        var control = (int)(_controlBlows - _baselineControl);
        _driverVerdict = new HoldVerdict(MarshalHoldRules.Judge(MarshalFlagOff(0), held, control), MarshalFlagOff(0), held, control, Time - _baselineAt);
        if (_driverVerdict.Value.Proof == HoldProof.NoControl)
            _baselineAt = float.NaN;
    }

    // ---- the hollow square ----

    internal bool SquareIsPlain = true;
    internal bool SquareKeepsItsSide = true;

    /// <summary>The soldiers take their places from the mixed layout and not from the square, as a company of melee and ranged soldiers does in a field battle.</summary>
    internal bool SquareLaidOut;

    /// <summary>The drag changes nothing: the square keeps the side it formed with, as a build with no drag handling would.</summary>
    internal bool SquareDragDoesNothing;

    /// <summary>The side the square forms with; the minimum plus a metre when unset.</summary>
    internal float? SquareFormedSide;
    internal bool SquarePlacesEveryone = true;
    internal float SquareMinimum = 10f;
    internal float SquareMaximum = 30f;

    private bool _squareOrdered;
    private float _squareSide = float.NaN;
    private float _plannedSide = float.NaN;

    public int MarshalChoosePureInfantry()
    {
        _chosen.Clear();
        var index = Companies.FindIndex(c => !c.Ranged);
        if (index < 0)
            return 0;
        _chosen.Add(index);
        return (int)Companies[index].Units;
    }

    public void MarshalOrderSquare()
    {
        Note("marshal-square");
        _squareOrdered = true;
        _squareSide = SquareFormedSide ?? SquareMinimum + 1f;
    }

    public bool MarshalPlainSquare(int company) => _squareOrdered && SquareIsPlain;

    public string MarshalArrangementName(int company) => !_squareOrdered ? "LineFormation" : SquareIsPlain ? "SquareFormation" : "RectilinearSchiltronFormation";

    public float MarshalSquareSide(int company) => MarshalPlainSquare(company) ? _squareSide : float.NaN;

    public float MarshalSquareMinimum(int company) => MarshalPlainSquare(company) ? SquareMinimum : float.NaN;

    public float MarshalSquareMaximum(int company) => MarshalPlainSquare(company) ? SquareMaximum : float.NaN;

    public float MarshalUnitWidth(int company) => 1f;

    public int MarshalUnits(int company) => company < _chosen.Count ? (int)Companies[_chosen[company]].Units : 0;

    public int MarshalPlaced(int company) => MarshalPlainSquare(company) ? (SquarePlacesEveryone ? MarshalUnits(company) : MarshalUnits(company) - 1) : 0;

    public void MarshalPlanSideDrag(int company, float side) => _plannedSide = side;

    public bool MarshalKillOne(int company)
    {
        Note("marshal-kill");
        Companies[_chosen[company]].Units--;
        if (_squareOrdered && !SquareKeepsItsSide)
            _squareSide += 8f;
        return true;
    }

    private void ApplySquareDrag()
    {
        if (_squareOrdered && !float.IsNaN(_plannedSide) && !SquareDragDoesNothing)
            _squareSide = _plannedSide;
    }

    // ---- the spacing repair ----

    internal bool RiderMoves = true;
    internal string SpacingWhy = "";
    internal bool LimitsReadAsInfantry = true;
    internal bool RiderIsSignificant;

    /// <summary>The order was the AI's transfer, which the engine refuses for a company the player owns (Formation.cs:2157): the company keeps its soldiers and gains no rider.</summary>
    internal bool RiderTransferMovesNobody;

    /// <summary>The soldier who joined is on foot, so no census finds a rider.</summary>
    internal bool TheSoldierWhoJoinedIsOnFoot;

    /// <summary>Mission seconds after the move before the engine's cached ratio, and so its count of riders, reads the rider (QueryData.cs:73-76 returns the cache as it stands).</summary>
    internal float EngineCountsTheRiderAfter;

    private bool _riderMoved;
    private float _riderMovedAt;

    private bool TheRiderIsThere => _riderMoved && !TheSoldierWhoJoinedIsOnFoot;

    public int MarshalMoveOneRider()
    {
        Note("marshal-rider");
        if (!RiderMoves || _chosen.Count == 0)
            return 0;
        if (!RiderTransferMovesNobody)
        {
            _riderMoved = true;
            _riderMovedAt = Time;
            Companies[_chosen[0]].Units++;
        }

        return (int)Companies[_chosen[0]].Units;
    }

    public SpacingReading? MarshalSpacingVerdict => _riderMoved && SpacingWhy.Length == 0 ? (LimitsReadAsInfantry ? SpacingReading.Infantry : SpacingReading.Cavalry) : null;

    public string MarshalSpacingWhy => SpacingWhy;

    public int MarshalRiders(int company) => TheRiderIsThere ? 1 : 0;

    public bool MarshalHasAnyRider(int company) => TheRiderIsThere && Time - _riderMovedAt >= EngineCountsTheRiderAfter;

    public bool MarshalHasSignificantRiders(int company) => MarshalHasAnyRider(company) && RiderIsSignificant;

    public SpacingRead? MarshalSpacingOfChosen(int company)
    {
        var infantry = new SpacingLimits(0f, 0.4f, 0f, 0.38f);
        var cavalry = new SpacingLimits(1.7f, 2.0f, 0.18f, 0.5f);
        return new SpacingRead(company, MarshalUnits(company), MarshalRiders(company), LimitsReadAsInfantry ? infantry : cavalry, infantry, cavalry);
    }
}
