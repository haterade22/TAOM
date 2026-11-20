using System.Threading;

namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// One soldier's stamina state for one battle. THREAD CONTRACT: the mission tick (main thread) is the only writer of
/// every member except <see cref="MarkRemoved"/>, which an engine callback may call from any thread. Other threads
/// (the stat models, the AI input component, the HUD) read only <see cref="Band"/>, <see cref="Fraction"/> and
/// <see cref="Removed"/>, which are published with Volatile. Holds no engine type.
/// </summary>
public sealed class StaminaCell
{
    private int _band;
    private int _fractionMilli = 1000;
    private int _removed;

    public StaminaCell(float bornAt) => BornAt = bornAt;

    /// <summary>Mission time when the cell was made: a young soldier's stats are not refreshed.</summary>
    public float BornAt { get; }

    /// <summary>Any thread.</summary>
    public StaminaBand Band => (StaminaBand)Volatile.Read(ref _band);

    /// <summary>Stamina as a share of the pool, 0 to 1. Any thread.</summary>
    public float Fraction => Volatile.Read(ref _fractionMilli) / 1000f;

    /// <summary>Any thread: the soldier left the mission, and the tick drops the cell on its next pass.</summary>
    public bool Removed => Volatile.Read(ref _removed) != 0;

    /// <summary>Main thread only: a band the dev console forced; null lets the rules decide.</summary>
    public StaminaBand? Forced { get; set; }

    /// <summary>Main thread only: the lock path the tick last applied to the soldier.</summary>
    public StaminaLockPath AppliedPath { get; set; }

    /// <summary>Main thread only. A fraction that is not a number publishes as 0.</summary>
    public void Publish(StaminaBand band, float fraction)
    {
        var clamped = !(fraction > 0f) ? 0f : fraction > 1f ? 1f : fraction;
        Volatile.Write(ref _fractionMilli, (int)(clamped * 1000f + 0.5f));
        Volatile.Write(ref _band, (int)band);
    }

    public void MarkRemoved() => Volatile.Write(ref _removed, 1);
}
