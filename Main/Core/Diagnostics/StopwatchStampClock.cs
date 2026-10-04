using System.Diagnostics;

namespace TAOM.Core.Diagnostics;

/// <summary>The real <see cref="IStampClock"/>: <see cref="Stopwatch"/>'s high-resolution timestamp.</summary>
public sealed class StopwatchStampClock : IStampClock
{
    /// <summary>For callers built outside the container (the patch applier SubModule.cs constructs itself).</summary>
    public static readonly StopwatchStampClock Instance = new();

    public long Now => Stopwatch.GetTimestamp();

    public long Frequency => Stopwatch.Frequency;
}
