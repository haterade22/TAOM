using System.Diagnostics;
using System.Threading;

namespace TAOM.Features.Stamina;

/// <summary>
/// One battle's stamina counters, for <c>taom.print_stamina</c> and the engine spikes of #1497. Any thread may add:
/// the AI strip counts from the AI input callback.
/// </summary>
public sealed class StaminaTelemetry
{
    private long _aiStrips;
    private long _aiStripsOffMain;
    private long _playerStrips;
    private long _refreshes;
    private long _refreshBatches;
    private long _refreshTicks;
    private long _maxBatchTicks;

    public long AiStrips => Interlocked.Read(ref _aiStrips);

    public long PlayerStrips => Interlocked.Read(ref _playerStrips);

    public long Refreshes => Interlocked.Read(ref _refreshes);

    public void CountAiStrip(bool onMainThread)
    {
        Interlocked.Increment(ref _aiStrips);
        if (!onMainThread)
            Interlocked.Increment(ref _aiStripsOffMain);
    }

    public void CountPlayerStrip() => Interlocked.Increment(ref _playerStrips);

    /// <summary>Main thread: one frame's batch of stat recomputes and the stopwatch ticks it took.</summary>
    public void NoteRefreshBatch(int refreshed, long stopwatchTicks)
    {
        Interlocked.Add(ref _refreshes, refreshed);
        Interlocked.Increment(ref _refreshBatches);
        Interlocked.Add(ref _refreshTicks, stopwatchTicks);
        if (stopwatchTicks > Interlocked.Read(ref _maxBatchTicks))
            Interlocked.Exchange(ref _maxBatchTicks, stopwatchTicks);
    }

    public string Report()
    {
        var refreshes = Refreshes;
        var totalMs = Interlocked.Read(ref _refreshTicks) * 1000.0 / Stopwatch.Frequency;
        var maxMs = Interlocked.Read(ref _maxBatchTicks) * 1000.0 / Stopwatch.Frequency;
        var perCallUs = refreshes == 0 ? 0.0 : totalMs * 1000.0 / refreshes;
        return $"input strips: AI {AiStrips} ({Interlocked.Read(ref _aiStripsOffMain)} off the main thread), player {PlayerStrips}; stat refreshes: {refreshes} in {Interlocked.Read(ref _refreshBatches)} batch(es), "
               + $"{perCallUs:0.0} us each on average, slowest batch {maxMs:0.000} ms";
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _aiStrips, 0);
        Interlocked.Exchange(ref _aiStripsOffMain, 0);
        Interlocked.Exchange(ref _playerStrips, 0);
        Interlocked.Exchange(ref _refreshes, 0);
        Interlocked.Exchange(ref _refreshBatches, 0);
        Interlocked.Exchange(ref _refreshTicks, 0);
        Interlocked.Exchange(ref _maxBatchTicks, 0);
    }
}
