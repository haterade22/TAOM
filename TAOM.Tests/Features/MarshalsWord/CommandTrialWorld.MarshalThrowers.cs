using System;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.MarshalsWord;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The stand-in for throwers on an advance: a company of forty javelin soldiers walks up a line toward an enemy company a hundred metres ahead, and the real
/// <see cref="MarshalThrowerDriver"/> runs against a fake adapter that reads this world. Each knob breaks one fact of one step, and a world with every knob at its
/// default is a feature that works: the Advance walks to the stand, the soldiers throw while they stand, and the company goes in after the release.
/// </summary>
internal sealed partial class CommandTrialWorld
{
    private const int ThrowerCompany = 1;

    /// <summary>No company holds enough throwing soldiers.</summary>
    internal bool NoThrowers;

    /// <summary>The rule works: the Advance walks to the stand point. Off, the company walks to two metres of the enemy as the base game does.</summary>
    internal bool ThrowersStand = true;

    /// <summary>The soldiers throw while the company stands.</summary>
    internal bool ThrowersThrow = true;

    /// <summary>The enemy is on the company before it can stand: the gap falls to melee distance a second after the order.</summary>
    internal bool ThrowersClose;

    /// <summary>The company stops this many metres short of its stand point, so it never arrives.</summary>
    internal float ThrowerStopsShortMetres;

    /// <summary>The company walks this many metres past its stand point after it arrives.</summary>
    internal float ThrowerOvershootMetres;

    /// <summary>The off switch reaches nothing, so the rule goes on working in the control step.</summary>
    internal bool ThrowerOffSwitchIsIgnored;

    /// <summary>The company does not walk at all, so a control never closes.</summary>
    internal bool ThrowerNeverCloses;

    /// <summary>The enemy walks at this many metres a second toward the company from the order on, and stops with its median 4 m from the company's front.</summary>
    internal float ThrowerEnemySpeed;

    internal bool ThrowerPostfixMoves = true;
    internal bool ThrowerShotsAreCounted = true;
    internal bool ThrowerMeleeFollows = true;
    internal bool ThrowerTimerRuns = true;
    internal bool ThrowerFaultsThePostfix;

    /// <summary>The company is put on hold fire the moment the Advance is given, so the stand rules refuse it.</summary>
    internal bool ThrowerRefusedAtTheOrder;

    internal float ThrowerThrowsPerSecond = 10f;

    /// <summary>Throws a second while the company approaches its point and is not yet standing.</summary>
    internal float ThrowerApproachThrowsPerSecond;

    /// <summary>Throws a second after the release.</summary>
    internal float ThrowerReleasedThrowsPerSecond;

    /// <summary>The company stays where it stood after the release.</summary>
    internal bool ThrowerHoldsAfterRelease;
    internal float ThrowerRange = 80f;

    /// <summary>The driver's clock runs at this share of the world's, so a stand that should have ended is still going when the step gives up.</summary>
    internal float ThrowerClockFactor = 1f;

    /// <summary>The timer calls per mission second, and the ticks each call takes (a tick is a microsecond at the stand-in's clock rate of one million).</summary>
    internal int ThrowerCostCallsPerSecond = 200;

    internal long ThrowerCostTicksPerCall = 10;

    /// <summary>One call of this many microseconds, added once beside the ordinary ones.</summary>
    internal long ThrowerOneSlowCallMicros;

    private readonly FakeMarshalThrowers _throwerEngine = new();
    private readonly RecordingOutcomeLog _throwerLog = new();
    private MarshalThrowerDriver? _throwerDriver;
    private bool _throwersOff;
    private bool _throwerOrdered;
    private bool _throwerFaultLatched;
    private float _throwerY;
    private float _throwerEnemyY = 100f;
    private float _throwerOrderedAt;
    private float _throwerClock;
    private float _throwerOvershot;
    private double _throwerCarry;
    private double _throwerBlowCarry;
    private double _throwerCostCarry;
    private int _throwerAmmo = 120;

    /// <summary>The reads that touch the engine's query system or walk a company's soldiers, made outside the mission tick (the AI job overlaps the screen tick).</summary>
    internal readonly System.Collections.Generic.List<string> EngineReadsOutsideTheMissionTick = new();

    internal bool ThrowersAreOff => _throwersOff;

    private void NoteRead(string read)
    {
        if (!InMissionTick)
            EngineReadsOutsideTheMissionTick.Add(read);
    }

    private ThrowerCensus ThrowerCensusNow() => new(40, (_throwerAmmo + 2) / 3, _throwerAmmo);

    public ThrowerCensus MarshalChooseThrowers()
    {
        NoteRead("MarshalChooseThrowers");
        _chosen.Clear();
        if (NoThrowers)
            return default;
        _chosen.Add(ThrowerCompany);
        if (_throwerOrdered)
            NewThrowerGround();
        if (_throwerDriver == null)
            StartThrowerDriver();
        return ThrowerCensusNow();
    }

    // Each thrower step runs in its own battle in the game: a step that chooses after an earlier step's Advance starts on fresh thrower ground.
    private void NewThrowerGround()
    {
        _throwerDriver = null;
        _throwerOrdered = false;
        _throwerFaultLatched = false;
        _throwerY = 0f;
        _throwerEnemyY = 100f;
        _throwerOvershot = 0f;
        _throwerCarry = 0.0;
        _throwerBlowCarry = 0.0;
        _throwerCostCarry = 0.0;
        _throwerAmmo = 120;
    }

    public float MarshalThrowerRange(int company)
    {
        NoteRead("MarshalThrowerRange");
        return ThrowerRange;
    }

    public void MarshalAdvance()
    {
        Note("marshal-advance");
        _throwerOrdered = true;
        _throwerOrderedAt = Time;
        Companies[ThrowerCompany].Order = "Advance";
        _throwerEngine.Companies[ThrowerCompany] = new ThrowerCompany(true, true, false);
        if (ThrowerRefusedAtTheOrder)
            _throwerEngine.GatesOf[ThrowerCompany] = _throwerEngine.GatesOf[ThrowerCompany] with { HoldFire = true };
        _throwerDriver?.OnAdvanceGiven(new[] { ThrowerCompany });
    }

    public ThrowerFacts? MarshalThrowerRun(int company) => _throwerDriver?.FactsOf(ThrowerCompany);

    public string MarshalThrowerWhy(int company)
    {
        NoteRead("MarshalThrowerWhy");
        return MarshalThrowerRules.WhyNot(_throwerEngine.GatesOf[ThrowerCompany] with { Range = ThrowerRange }, ThrowerCensusNow());
    }

    public ThrowerCostRead MarshalThrowerCostRead => MarshalThrowerCost.Read();

    public int MarshalThrowerPublished => _throwerDriver?.Published ?? 0;

    public bool MarshalThrowerFaulted => MarshalHooks.ThrowerFault != null;

    public ThrowerCensus MarshalThrowerCensus(int company)
    {
        NoteRead("MarshalThrowerCensus");
        return ThrowerCensusNow();
    }

    public float MarshalFrontGap(int company) =>
        EnemyUnits > 0 ? MarshalThrowerRules.Gap(new GroundPoint(0f, _throwerY), new GroundPoint(0f, _throwerEnemyY), 6f) : float.NaN;

    public GroundPoint? MarshalCompanyAt(int company) => new GroundPoint(0f, _throwerY);

    public void MarshalThrowersOff(bool off)
    {
        Note("marshal-throwers-off");
        _throwersOff = off && !ThrowerOffSwitchIsIgnored;
    }

    private void StartThrowerDriver()
    {
        MarshalFireTable.Begin();
        MarshalThrowerCost.Reset();
        _throwerEngine.Standard(ThrowerCompany);
        _throwerEngine.Companies[ThrowerCompany] = new ThrowerCompany(true, false, false);
        _throwerEngine.Ranges[ThrowerCompany] = ThrowerRange;
        _throwerDriver = new MarshalThrowerDriver(_throwerEngine, _throwerLog, new ListLogger(), "1");
    }

    private void AdvanceThrowers(float dt)
    {
        if (_throwerDriver == null)
            return;
        _throwerClock += dt * ThrowerClockFactor;
        _throwerEngine.Now = _throwerClock;
        if (_throwerOrdered)
        {
            CloseTheEnemy(dt);
            WalkThrowers(dt);
            ThrowAndFight(dt);
            Measure(dt);
        }

        _throwerEngine.Censuses[ThrowerCompany] = ThrowerCensusNow();
        _throwerEngine.GatesOf[ThrowerCompany] = _throwerEngine.GatesOf[ThrowerCompany] with { Own = new GroundPoint(0f, _throwerY), Target = new GroundPoint(0f, _throwerEnemyY) };
        _throwerDriver.Pass(!_throwersOff);
        LatchTheFault();
    }

    private void CloseTheEnemy(float dt)
    {
        if (ThrowersClose && Time - _throwerOrderedAt >= 1f)
            _throwerEnemyY = _throwerY + 8f;
        if (ThrowerEnemySpeed > 0f)
            _throwerEnemyY = Math.Max(_throwerEnemyY - ThrowerEnemySpeed * dt, Math.Min(_throwerEnemyY, _throwerY + 4f));
    }

    private bool Released() => _throwerDriver?.FactsOf(ThrowerCompany) is { State: ThrowerState.Released };

    private bool Standing() => _throwerDriver?.FactsOf(ThrowerCompany) is { State: ThrowerState.Stand };

    private void WalkThrowers(float dt)
    {
        if (ThrowerHoldsAfterRelease && Released())
            return;
        var point = _throwerEngine.Latest.Length > 0 ? _throwerEngine.Latest[0].Y : float.NaN;
        var goal = _throwerEnemyY - 2f;
        if (ThrowersStand && !_throwersOff && !Released())
            goal = float.IsNaN(point) ? _throwerY : point - ThrowerStopsShortMetres;
        var step = ThrowerNeverCloses ? 0f : Companies[ThrowerCompany].Speed * dt;
        if (ThrowersStand && !_throwersOff && !Released() && _throwerY >= goal && ThrowerOvershootMetres > _throwerOvershot)
        {
            var more = Math.Min(step, ThrowerOvershootMetres - _throwerOvershot);
            _throwerOvershot += more;
            _throwerY += more;
            return;
        }

        _throwerY = Math.Min(goal, _throwerY + step);
    }

    private void ThrowAndFight(float dt)
    {
        var rate = Standing() ? (ThrowersThrow ? ThrowerThrowsPerSecond : 0f) : Released() ? ThrowerReleasedThrowsPerSecond : ThrowerApproachThrowsPerSecond;
        if (rate > 0f && _throwerAmmo > 0)
        {
            _throwerCarry += rate * dt;
            while (_throwerCarry >= 1.0 && _throwerAmmo > 0)
            {
                _throwerCarry -= 1.0;
                _throwerAmmo--;
                if (ThrowerShotsAreCounted)
                    MarshalFireTable.CountAnyShot(ThrowerCompany);
            }
        }

        if (Released() && ThrowerMeleeFollows && MarshalFrontGap(0) <= MarshalTuning.MeleeGapMetres)
        {
            _throwerBlowCarry += 3.0 * dt;
            while (_throwerBlowCarry >= 1.0)
            {
                _throwerBlowCarry -= 1.0;
                MarshalFireTable.CountBlow(ThrowerCompany);
            }
        }
    }

    private void Measure(float dt)
    {
        if (_throwerEngine.Latest.Length == 0)
            return;
        if (ThrowerPostfixMoves)
            MarshalThrowerCost.AddMoved();
        if (!ThrowerTimerRuns)
            return;
        if (ThrowerOneSlowCallMicros > 0)
        {
            MarshalThrowerCost.AddPublished(ThrowerOneSlowCallMicros * System.Diagnostics.Stopwatch.Frequency / 1_000_000L, replaced: false);
            ThrowerOneSlowCallMicros = 0;
        }

        _throwerCostCarry += ThrowerCostCallsPerSecond * dt;
        while (_throwerCostCarry >= 1.0)
        {
            _throwerCostCarry -= 1.0;
            MarshalThrowerCost.AddPublished(ThrowerCostTicksPerCall * System.Diagnostics.Stopwatch.Frequency / 1_000_000L, replaced: false);
        }
    }

    private void LatchTheFault()
    {
        if (!ThrowerFaultsThePostfix || _throwerFaultLatched || Time - _throwerOrderedAt < 1f || !_throwerOrdered)
            return;
        _throwerFaultLatched = true;
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("the postfix threw"));
    }
}
