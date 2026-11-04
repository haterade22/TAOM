using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// What the client may touch of a running Long Defeat run: the view, the shop's buy and the horn. <see cref="Result"/> outlives
/// <see cref="Clear"/> because the end panel and the unattended driver read it after the mission has gone.
/// </summary>
internal sealed class LongDefeatSession
{
    private SurvivalRun? _run;
    private RunResult? _result;
    private int _lostInClearedWaves;

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

    public RunView? GetView() => _run?.GetView();

    public BuyResult TryBuy(string offerId) => _run?.TryBuy(offerId) ?? BuyResult.RunOver;

    public StartResult TryStartWave() => _run?.TryStartWave() ?? StartResult.RunOver;

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
        _run = run;
        _result = null;
    }

    internal void Clear()
    {
        _result = _run?.Result ?? _result;
        _lostInClearedWaves = LostInClearedWaves;
        _run = null;
    }
}
