namespace TAOM.Features.LongDefeat.Coop;

/// <summary>
/// The fixed values a Long Defeat co-op session (#1280) shares with BannerlordCoop. TAOM.dll names no Coop type, so
/// TAOM.CoopCompat's tests check <see cref="MaxPayloadBytes"/> against Coop's own value; <see cref="ModId"/> is TAOM's.
/// </summary>
public static class LongDefeatCoopContract
{
    /// <summary>The mod id a Long Defeat session is hosted under. Every member opens the mission only for this id.</summary>
    public const string ModId = "TAOM.LongDefeat";

    /// <summary>Coop's <c>AttemptHostSession.MaxPayloadBytes</c>: the largest payload a session can carry.</summary>
    public const int MaxPayloadBytes = 8192;
}
