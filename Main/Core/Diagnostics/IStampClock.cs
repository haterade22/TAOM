namespace TAOM.Core.Diagnostics;

/// <summary>A monotonic timestamp source; ticks per second is Frequency. Faked in tests.</summary>
public interface IStampClock
{
    long Now { get; }
    long Frequency { get; }
}
