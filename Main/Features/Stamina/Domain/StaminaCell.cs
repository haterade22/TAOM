using System.Threading;

namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// One soldier's stamina state for one battle. THREAD CONTRACT: the mission tick (main thread) is the only writer of
/// every member except <see cref="MarkRemoved"/> and <see cref="AddPending"/>, which an engine callback may call from
/// any thread. Other threads (the stat models, the AI input component, the HUD, the hit callbacks) read only
/// <see cref="Band"/>, <see cref="Fraction"/>, <see cref="Removed"/> and <see cref="Tracked"/>, which are published
/// with Volatile or a volatile field. Holds no engine type.
/// </summary>
public sealed class StaminaCell
{
    private int _band;
    private int _fractionMilli = 1000;
    private int _removed;
    private int _pendingMilli;

    public StaminaCell(float bornAt) => BornAt = bornAt;

    /// <summary>Mission time when the cell was made: a young soldier's stats are not refreshed.</summary>
    public float BornAt { get; }

    /// <summary>Any thread.</summary>
    public StaminaBand Band => (StaminaBand)Volatile.Read(ref _band);

    /// <summary>Stamina as a share of the pool, 0 to 1. Any thread.</summary>
    public float Fraction => Volatile.Read(ref _fractionMilli) / 1000f;

    /// <summary>Any thread: the soldier left the mission, and the tick drops the cell on its next pass.</summary>
    public bool Removed => Volatile.Read(ref _removed) != 0;

    // Everything below is main thread only.

    /// <summary>A band the dev console forced; null lets the rules decide. While forced, nothing drains or comes back.</summary>
    public StaminaBand? Forced { get; set; }

    /// <summary>The lock path the tick last applied to the soldier.</summary>
    public StaminaLockPath AppliedPath { get; set; }

    /// <summary>The tick worked out the soldier's pool and factors: done once, on its first look at a soldier this peer owns.</summary>
    public bool Enrolled { get; set; }

    /// <summary>The pool in points; 0 until the tick first looks at the soldier.</summary>
    public float Max { get; set; }

    public float Current { get; set; }

    /// <summary>On every cost: the MCM cost scale times the soldier's own factor.</summary>
    public float CostFactor { get; set; } = 1f;

    /// <summary>The soldier's own cost factor, from the weight of his armour; set once, with his pool.</summary>
    public float OwnCostFactor { get; set; } = 1f;

    /// <summary>His people does not tire: he stays Fresh, pays nothing and is never locked.</summary>
    public bool Exempt { get; set; }

    /// <summary>On regeneration: the soldier's own factor (his people).</summary>
    public float RegenFactor { get; set; } = 1f;

    /// <summary>Mission time until which the soldier's own last action pauses regeneration.</summary>
    public float RegenPausedUntil { get; set; }

    /// <summary>Mission time of the tick's last full look; negative before the first.</summary>
    public float LastVisitAt { get; set; } = -1f;

    private volatile bool _tracked;

    /// <summary>
    /// The last look found the soldier alive, owned by this peer and subject to stamina: only then are his actions
    /// polled, his stopped blows charged and his bar drawn. The tick writes it. A hit callback reads it on any thread,
    /// where a stale read costs or spares one blow.
    /// </summary>
    public bool Tracked
    {
        get => _tracked;
        set => _tracked = value;
    }

    /// <summary>The published band was decided by the rules, not by the console or an exemption. Main thread.</summary>
    public bool BandByRules { get; set; }

    /// <summary>The soldier's present exhaustion was counted, so his recovery will be. Main thread.</summary>
    public bool EntryCounted { get; set; }

    public int LastAction0 { get; set; } = -1;

    public int LastAction1 { get; set; } = -1;

    /// <summary>The engine action type on each channel at the last change, as its number.</summary>
    public int Type0 { get; set; }

    public int Type1 { get; set; }

    /// <summary>Mission time a bow draw began, for a player's bow only; not a number when none is held.</summary>
    public float DrawSince { get; set; } = float.NaN;

    /// <summary>For the battle's summary.</summary>
    public bool EverFought { get; set; }

    public bool EverExhausted { get; set; }

    public float ExhaustedSince { get; set; }

    public bool StuckReported { get; set; }

    public bool TiredWarned { get; set; }

    /// <summary>Main thread only. A fraction that is not a number publishes as 0.</summary>
    public void Publish(StaminaBand band, float fraction)
    {
        var clamped = !(fraction > 0f) ? 0f : fraction > 1f ? 1f : fraction;
        Volatile.Write(ref _fractionMilli, (int)(clamped * 1000f + 0.5f));
        Volatile.Write(ref _band, (int)band);
    }

    public void MarkRemoved() => Volatile.Write(ref _removed, 1);

    /// <summary>
    /// Any thread: a cost an engine callback found (a stopped blow), in points. Kept as whole thousandths, because
    /// .NET Framework has no atomic add for a float. A cost that is not a positive finite number adds nothing.
    /// </summary>
    public void AddPending(float cost)
    {
        if (!(cost > 0f) || cost > 1000f)
            return;
        Interlocked.Add(ref _pendingMilli, (int)(cost * 1000f + 0.5f));
    }

    /// <summary>Main thread: the costs added since the last call, in points.</summary>
    public float TakePending() => Interlocked.Exchange(ref _pendingMilli, 0) / 1000f;
}
