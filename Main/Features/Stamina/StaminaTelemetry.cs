using System.Diagnostics;
using System.Threading;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Features.Stamina;

/// <summary>
/// One battle's stamina counters, for the 30 s pulse line, the mission-end summary, the outcome checks and
/// <c>taom.print_stamina</c> (#1497). Any thread may add: the AI strip counts from the AI input callback and the
/// block counts from the hit callbacks.
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
    private long _actions;
    private readonly long[] _actionsByKind = new long[(int)StaminaAction.Bash + 1];
    private long _blocks;
    private long _fighters;
    private long _exhaustions;
    private long _exhaustedSoldiers;
    private long _recoveries;
    private long _stuck;

    public long AiStrips => Interlocked.Read(ref _aiStrips);

    public long PlayerStrips => Interlocked.Read(ref _playerStrips);

    public long Refreshes => Interlocked.Read(ref _refreshes);

    /// <summary>Times a soldier became Exhausted.</summary>
    public long Exhaustions => Interlocked.Read(ref _exhaustions);

    /// <summary>Different soldiers who were Exhausted at least once.</summary>
    public long ExhaustedSoldiers => Interlocked.Read(ref _exhaustedSoldiers);

    /// <summary>Different soldiers who paid for an action or a stopped blow at least once.</summary>
    public long Fighters => Interlocked.Read(ref _fighters);

    public long Recoveries => Interlocked.Read(ref _recoveries);

    /// <summary>Soldiers the rules left Exhausted for longer than a recovery can take.</summary>
    public long Stuck => Interlocked.Read(ref _stuck);

    public double SlowestRefreshBatchMs => Interlocked.Read(ref _maxBatchTicks) * 1000.0 / Stopwatch.Frequency;

    public void CountAiStrip(bool onMainThread)
    {
        Interlocked.Increment(ref _aiStrips);
        if (!onMainThread)
            Interlocked.Increment(ref _aiStripsOffMain);
    }

    public void CountPlayerStrip() => Interlocked.Increment(ref _playerStrips);

    public void CountAction(StaminaAction kind)
    {
        Interlocked.Increment(ref _actions);
        if ((uint)kind < (uint)_actionsByKind.Length)
            Interlocked.Increment(ref _actionsByKind[(int)kind]);
    }

    private long Kind(StaminaAction kind) => Interlocked.Read(ref _actionsByKind[(int)kind]);

    public void CountBlock() => Interlocked.Increment(ref _blocks);

    public void CountFighter() => Interlocked.Increment(ref _fighters);

    public void CountExhausted(bool first)
    {
        Interlocked.Increment(ref _exhaustions);
        if (first)
            Interlocked.Increment(ref _exhaustedSoldiers);
    }

    public void CountRecovered() => Interlocked.Increment(ref _recoveries);

    public void CountStuck() => Interlocked.Increment(ref _stuck);

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
        var perCallUs = refreshes == 0 ? 0.0 : totalMs * 1000.0 / refreshes;
        return $"actions={Interlocked.Read(ref _actions)} (swing {Kind(StaminaAction.Swing)}, bow {Kind(StaminaAction.BowShot)}, "
               + $"crossbow {Kind(StaminaAction.CrossbowShot)}, throw {Kind(StaminaAction.Throw)}, jump {Kind(StaminaAction.Jump)}, "
               + $"kick {Kind(StaminaAction.Kick)}, bash {Kind(StaminaAction.Bash)}) blocks={Interlocked.Read(ref _blocks)} fighters={Fighters} "
               + $"enteredExh={Exhaustions} exhaustedSoldiers={ExhaustedSoldiers} recovered={Recoveries} stuck={Stuck}; "
               + $"input strips: AI {AiStrips} ({Interlocked.Read(ref _aiStripsOffMain)} off the main thread), player {PlayerStrips}; "
               + $"stat refreshes: {refreshes} in {Interlocked.Read(ref _refreshBatches)} batch(es), {perCallUs:0.0} us each on average, "
               + $"slowest batch {SlowestRefreshBatchMs:0.000} ms";
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
        Interlocked.Exchange(ref _actions, 0);
        for (var i = 0; i < _actionsByKind.Length; i++)
            Interlocked.Exchange(ref _actionsByKind[i], 0);
        Interlocked.Exchange(ref _blocks, 0);
        Interlocked.Exchange(ref _fighters, 0);
        Interlocked.Exchange(ref _exhaustions, 0);
        Interlocked.Exchange(ref _exhaustedSoldiers, 0);
        Interlocked.Exchange(ref _recoveries, 0);
        Interlocked.Exchange(ref _stuck, 0);
    }
}
