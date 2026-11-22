using System.Globalization;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The death rule's verdicts of #1079 (phase4-plan.md section 6, Slice 4): <c>rise</c> and <c>general-down</c>. Pure: the director reads the field, these turn the numbers into pass, fail or skip. A gate on a
/// float is written as the requirement, so a NaN fails it, and every word of a detail is <c>name=value</c> (the log sanitiser glues a bare word to the value before it).
/// </summary>
internal static class LongDefeatLordChecks
{
    /// <summary>How far, on the ground, the risen lord may stand from the rise place on the tick after the spawn.</summary>
    internal const float RiseRadius = 2f;

    /// <summary>How far above or below the rise place the risen lord may stand: room for a body to settle onto the floor, and not enough to pass a lord who came up on another floor of the keep.</summary>
    internal const float RiseHeight = 3f;

    /// <summary>
    /// The lord rose and the tick after, once adoption ran, the field read him back: the main agent is his own body (<c>main_is_lord</c>), within <see cref="RiseRadius"/> on the ground and <see cref="RiseHeight"/>
    /// in height of the rise place (none to judge when the engine's own frame placed him, <paramref name="place"/> null), driven by the controller the mode expects (<c>Player</c> for a person, <c>AI</c> under
    /// autoplay), no other body of the player's side is <c>Player</c>-controlled, and the general and the order owner are the lord. The hand-back is named, never judged: <c>1</c>, <c>noop</c>, <c>dead</c> or <c>none</c> (R24).
    /// </summary>
    internal static CheckVerdict Rise(string at, bool autoplay, LdRise rise, LdLordFacts lord, LdPlace? place, LdControlRead control, bool generalIsLord, bool ownerIsLord)
    {
        var want = autoplay ? "AI" : "Player";
        var mainIsLord = lord.Present && lord.Own;
        var measured = place != null && lord.Present;
        var dist = measured ? place!.FlatDistanceTo(lord.At) : float.NaN;
        var dz = measured ? System.Math.Abs(lord.At.Z - place!.Z) : float.NaN;
        var placed = place == null || (dist <= RiseRadius && dz <= RiseHeight);
        var ok = rise.Spawned && mainIsLord && placed && control.Controller == want && control.OtherPlayerBodies == 0 && generalIsLord && ownerIsLord;
        var distText = place != null && dist >= 0f ? dist.ToString("F1", CultureInfo.InvariantCulture) : "none";
        var dzText = place != null && dz >= 0f ? dz.ToString("F1", CultureInfo.InvariantCulture) : "none";
        return LongDefeatChecks.Verdict(ok, $"at={at} main_is_lord={Bit(mainIsLord)} dist={distText} dz={dzText} controller={control.Controller} want={want} others={control.OtherPlayerBodies.ToString(CultureInfo.InvariantCulture)} "
            + $"handed_back={HandBackText(rise.HandBack)} was_using={Bit(rise.WasUsing)} general_is_lord={Bit(generalIsLord)} owner_is_lord={Bit(ownerIsLord)}");
    }

    /// <summary>The engine refused the lord's spawn: the soldier, if one was taken, stays the player's, and the next clear orders the rise again.</summary>
    internal static CheckVerdict RiseRefused(string at) => new(CheckResult.Fail, $"at={at} reason=spawn-refused handed_back=none");

    /// <summary>A rise or a fall whose read-back was still owed when the run ended: nothing was read, so nothing is judged.</summary>
    internal static CheckVerdict RunEnded(string what) => new(CheckResult.Skip, $"{what} reason=run-ended");

    /// <summary>
    /// The tick after the lord's body fell with no main agent: the team's general is a standing hero or nobody, never the dead body, and a hero in the seat is the one the run chose (<paramref name="want"/>,
    /// the seat it last handed out). Nobody in the seat passes when the run chose nobody, or when the chosen hero no longer stands by the director's own books (<paramref name="wantStands"/> false: he fell
    /// on the same tick, or he stands with the other side); with him standing an empty seat is a write that never landed. With a main agent standing already (a take-over beat the read)
    /// there is nothing to judge, so it is a skip.
    /// </summary>
    internal static CheckVerdict GeneralDown(bool hasMain, LdSeatRead seat, LdGeneralSeat want, bool wantStands)
    {
        var text = SeatText(seat);
        var wanted = want.Kind == GeneralSeatKind.Hero ? "hero:" + want.HeroId : want.Kind == GeneralSeatKind.Lord ? "lord" : "none";
        if (hasMain)
            return new CheckVerdict(CheckResult.Skip, $"seat={text} want={wanted} reason=has-main");

        var rightHero = seat.Kind == LdSeatKind.Hero && want.Kind == GeneralSeatKind.Hero && string.Equals(seat.HeroId, want.HeroId, System.StringComparison.Ordinal);
        var stands = want.Kind == GeneralSeatKind.Hero ? $" stands={Bit(wantStands)}" : "";
        return LongDefeatChecks.Verdict((seat.Kind == LdSeatKind.None && !wantStands) || rightHero, $"seat={text} want={wanted}{stands}");
    }

    internal static string HandBackText(LdHandBack handBack) => handBack switch
    {
        LdHandBack.Done => "1",
        LdHandBack.Noop => "noop",
        LdHandBack.Dead => "dead",
        _ => "none",
    };

    internal static string SeatText(LdSeatRead seat) => seat.Kind switch
    {
        LdSeatKind.Hero => "hero:" + seat.HeroId,
        LdSeatKind.Main => "main",
        LdSeatKind.Stale => "stale",
        _ => "none",
    };

    private static string Bit(bool value) => value ? "1" : "0";
}
