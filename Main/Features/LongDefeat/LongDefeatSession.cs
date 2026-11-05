using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// What the client may touch of a running Long Defeat run: the view, the shop's buy and the horn. <see cref="Result"/> outlives
/// <see cref="Clear"/> because the end panel and the unattended driver read it after the mission has gone.
/// </summary>
internal sealed class LongDefeatSession
{
    /// <summary>The speeds, in the order the key steps through them: the levels of <see cref="FordsGameSpeed"/>.</summary>
    internal static readonly int[] SpeedLevels = { 1, 2, 4, 10 };

    private SurvivalRun? _run;
    private RunResult? _result;
    private int _lostInClearedWaves;
    private IReadOnlyList<BoonCard> _boons = Array.Empty<BoonCard>();
    private readonly FordsGameSpeed _speed = new();

    public bool IsActive => _run != null;

    /// <summary>Where the mission faulted (an exception in its start or a tick), or null. Outlives <see cref="Clear"/>, like <see cref="Result"/>; the next <c>Fill</c> resets it.</summary>
    public string? Fault { get; private set; }

    public RunResult? Result => _run?.Result ?? _result;

    public int LostInClearedWaves => _run?.Stats.LostInClearedWaves ?? _lostInClearedWaves;

    /// <summary>Mission seconds the director has ticked, for the unattended driver's live limit. Outlives <see cref="Clear"/>; the next <c>Fill</c> resets it.</summary>
    public float LiveSeconds { get; private set; }

    /// <summary>Where the run's JSON report was written, or null when none was. Outlives <see cref="Clear"/>; the next <c>Fill</c> resets it.</summary>
    public string? ReportPath { get; private set; }

    /// <summary>The fortress's best run as the director's save left it, for the end panel. Outlives <see cref="Clear"/>; the next <c>Fill</c> resets it.</summary>
    public BestRun? Best { get; private set; }

    /// <summary>The mission speed the player chose: 1, 2, 4 or 10. Held here, not in the run, so a wave or an intermission never changes it; 1 once the run has a <see cref="Result"/>, because a fight with no rules must not run fast behind the end panel; the next <c>Fill</c> resets it to 1.</summary>
    public int Speed => Result != null ? 1 : _speed.Current;

    /// <summary>The highest speed set since the last <c>Fill</c>, for the run report. Outlives <see cref="Clear"/>.</summary>
    public int MaxSpeed { get; private set; } = 1;

    public RunView? GetView() => _run?.GetView();

    public BuyResult TryBuy(string offerId) => _run?.TryBuy(offerId) ?? BuyResult.RunOver;

    public StartResult TryStartWave() => _run?.TryStartWave() ?? StartResult.RunOver;

    public PickResult TryPickBoon(string boonId) => _run?.TryPickBoon(boonId) ?? PickResult.RunOver;

    public RerollResult TryReroll() => _run?.TryReroll() ?? RerollResult.RunOver;

    /// <summary>The boons taken, for the end panel. Outlives <see cref="Clear"/>; the next <c>Fill</c> resets it.</summary>
    public IReadOnlyList<BoonCard> BoonsTaken => _run?.OwnedCards ?? _boons;

    internal void CycleSpeed()
    {
        _speed.Cycle();
        MaxSpeed = Math.Max(MaxSpeed, _speed.Current);
    }

    /// <summary>Sets a speed; a value that is no level changes nothing.</summary>
    internal void SetSpeed(int speed)
    {
        _speed.Set(speed);
        MaxSpeed = Math.Max(MaxSpeed, _speed.Current);
    }

    internal void RecordFault(string where) => Fault = where;

    internal void AddLiveSeconds(float dt)
    {
        if (dt > 0f && !float.IsInfinity(dt))
            LiveSeconds += dt;
    }

    internal void RecordReport(string path) => ReportPath = path;

    internal void RecordBest(BestRun best) => Best = best;

    internal void Fill(SurvivalRun run)
    {
        Fault = null;
        LiveSeconds = 0f;
        ReportPath = null;
        Best = null;
        _speed.Set(1);
        MaxSpeed = 1;
        _run = run;
        _result = null;
        _boons = Array.Empty<BoonCard>();
    }

    internal void Clear()
    {
        _result = _run?.Result ?? _result;
        _lostInClearedWaves = LostInClearedWaves;
        _boons = BoonsTaken;
        _run = null;
    }
}
