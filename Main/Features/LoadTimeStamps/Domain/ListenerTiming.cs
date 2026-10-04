namespace TAOM.Features.LoadTimeStamps.Domain;

/// <summary>
/// One campaign handler as plain values ("Type.Method", its assembly, whether it is TAOM's) and what
/// its calls cost during one dispatch. The adapter creates it before installing the handler's timing
/// wrapper, and the wrapper records each call on it; the dispatch runs on one thread.
/// </summary>
public sealed class ListenerTiming
{
    public ListenerTiming(string handler, string assembly, bool isTaom)
    {
        Handler = handler;
        Assembly = assembly;
        IsTaom = isTaom;
    }

    public string Handler { get; }

    public string Assembly { get; }

    public bool IsTaom { get; }

    public int Calls { get; private set; }

    public long Ticks { get; private set; }

    /// <summary>The slowest call's ticks; the first one on a tie.</summary>
    public long MaxTicks { get; private set; }

    /// <summary>The slowest call's int argument (a PartialFollowUp round), or -1.</summary>
    public int MaxArgument { get; private set; } = -1;

    /// <summary>One call of the handler: its ticks and its int argument (-1 for a one-argument event).</summary>
    public void Record(long ticks, int argument)
    {
        Calls++;
        Ticks += ticks;
        if (Calls == 1 || ticks > MaxTicks)
        {
            MaxTicks = ticks;
            MaxArgument = argument;
        }
    }
}
