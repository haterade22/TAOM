using System;

namespace TAOM.Features.SignatureStrikes.Domain;

/// <summary>
/// Mission time of an attacker's last strike of each <see cref="StrikeKind"/>, as a value. The
/// roster entry holds one and every <see cref="StrikeContext"/> carries a copy, so a stamp made
/// after a context was built never leaks into a decision already in flight. <c>default</c> is
/// "never struck" for every kind: NaN, the service's never sentinel, because a zero would read
/// as a strike at mission time 0 and hold the first real one behind a whole cooldown.
///
/// The backing array is never written after construction (<see cref="With"/> copies), so a copy
/// of this struct can share it safely.
/// </summary>
public readonly struct StrikeKindTimes
{
    private static readonly int KindCount = Enum.GetValues(typeof(StrikeKind)).Length;

    private readonly float[]? _times;

    private StrikeKindTimes(float[] times) => _times = times;

    /// <summary>NaN when this attacker has never struck with <paramref name="kind"/>.</summary>
    public float Get(StrikeKind kind)
    {
        var index = (int)kind;
        return _times != null && index >= 0 && index < _times.Length ? _times[index] : float.NaN;
    }

    /// <summary>A copy with <paramref name="kind"/> stamped at <paramref name="missionTime"/>. An
    /// undefined kind value returns this unchanged.</summary>
    public StrikeKindTimes With(StrikeKind kind, float missionTime)
    {
        var index = (int)kind;
        if (index < 0 || index >= KindCount)
            return this;

        var next = new float[KindCount];
        for (var i = 0; i < KindCount; i++)
            next[i] = Get((StrikeKind)i);
        next[index] = missionTime;
        return new StrikeKindTimes(next);
    }
}
