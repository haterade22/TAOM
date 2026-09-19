using TAOM.Core.Validation;

namespace TAOM.Features.Elephant;

/// <summary>
/// Running extremes for one howdah's end-of-battle summary line (#627). A non-finite value from the engine is counted
/// in <see cref="InvalidValues"/> and never folded in, so one NaN cannot erase a real minimum or maximum. Extremes read
/// NaN until the first finite sample, which the log prints as "n/a".
/// </summary>
public sealed class HowdahRunStats
{
    public int Ticks { get; private set; }
    public int Samples { get; private set; }
    public int InvalidValues { get; private set; }
    public float MinClearance { get; private set; } = float.NaN;
    public float MaxDrift { get; private set; } = float.NaN;
    public float MaxCarriedSpeed { get; private set; } = float.NaN;

    public void CountTick() => Ticks++;

    public void RecordSample(float clearance, float drift, float carriedSpeed)
    {
        Samples++;
        MinClearance = Fold(MinClearance, clearance, keepLower: true);
        MaxDrift = Fold(MaxDrift, drift, keepLower: false);
        MaxCarriedSpeed = Fold(MaxCarriedSpeed, carriedSpeed, keepLower: false);
    }

    private float Fold(float current, float value, bool keepLower)
    {
        if (!FiniteFloatValidator.IsFinite(value))
        {
            InvalidValues++;
            return current;
        }
        if (float.IsNaN(current)) return value;
        return keepLower ? (value < current ? value : current) : (value > current ? value : current);
    }
}
