using System;
using TAOM.Core.Diagnostics;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// A hand-driven clock: at the default Frequency of 1000 one tick is one millisecond. Set
/// <see cref="ThrowOnRead"/> to make that read of <see cref="Now"/> (1-based) throw.
/// </summary>
internal sealed class FakeStampClock : IStampClock
{
    private long _now;
    private int _reads;

    public long Now
    {
        get
        {
            _reads++;
            if (_reads == ThrowOnRead) throw new InvalidOperationException("clock gone");
            return _now;
        }
        set => _now = value;
    }

    public long Frequency { get; set; } = 1000;

    public int? ThrowOnRead { get; set; }

    public void Advance(long ticks) => _now += ticks;
}
