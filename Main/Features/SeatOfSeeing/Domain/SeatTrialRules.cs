using System;
using System.Collections.Generic;
using System.Globalization;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What the focus step read: the pixel the cursor stands on and the pixel it asked for, the pointer function's value for it, and the handler's own point.</summary>
internal readonly record struct SeatFocusReading(float PixelX, float PixelY, float WantedX, float WantedY, float ExpectedX, float ExpectedY,
    float FieldX, float FieldY, string Focused);

/// <summary>
/// The numbers and judgments the field trial's passes rest on (build-spec section 12), with no engine type so a test pins each one.
/// The client's script measures; these decide.
/// </summary>
internal static class SeatTrialRules
{
    /// <summary>Real seconds since the script began at which the trial finishes the deployment (once the refusal step has reported).</summary>
    internal const float DeploymentFinishSeconds = 5f;

    /// <summary>Seconds after the press at which <c>enter</c> is judged.</summary>
    internal const float EnterWindowSeconds = 3f;

    /// <summary>Seconds of forward input in <c>fly</c>.</summary>
    internal const float FlySeconds = 2f;

    /// <summary>Seconds in which the hero's speed is measured in <c>hero_stands</c>.</summary>
    internal const float HeroWindowSeconds = 1.5f;

    /// <summary>Seconds after the leave key in which the engine's camera must come back to the hero.</summary>
    internal const float GlideWindowSeconds = 4f;

    internal const float FlightTolerance = 0.25f;
    internal const float HeroSpeedLimit = 0.5f;
    internal const int FramesPerSecondFloor = 30;
    internal const float HeightToleranceMetres = 1f;
    internal const float HeightToleranceShare = 0.1f;
    internal const float NearHeroMetres = 10f;

    /// <summary>Real seconds a step waits for the cursor to show or hide, and for a pressed keep to be taken.</summary>
    internal const float ShortWaitSeconds = 1.5f;

    /// <summary>
    /// Frames of the step's own waits in which a panel closed with no player key must be open again. The session reopens on its next screen
    /// tick and the step resumes after the session in the same tick (SeatView.cs:81-84), so one frame is the usual answer, and view order is
    /// not fixed, so three is the limit. A waiting step is resumed once per screen tick (<c>CommandTrialScript.OnScreenTick</c>) and a step
    /// parked on the mission-tick marker once per mission tick (<c>CommandTrialScript.OnMissionTick</c>).
    /// </summary>
    internal const int ReopenFrameLimit = 3;

    /// <summary>Real seconds of lift input before <c>leave_glide</c> measures its height, so the height it leaves at is not the entry height.</summary>
    internal const float LiftSeconds = 0.5f;

    /// <summary>The flight's displacement points along the view's forward direction when its dot with it is at least this.</summary>
    internal const float ForwardDotFloor = 0.9f;

    /// <summary>Real seconds the panel must stay closed after the player's own close.</summary>
    internal const float StayClosedSeconds = 1f;

    /// <summary>Real seconds the view orbits a target before its distance is judged.</summary>
    internal const float OrbitSettleSeconds = 2f;

    /// <summary>The camera may stand this far from the orbit distance on either side, from a target that moves.</summary>
    internal const float KeptToleranceMetres = 2f;

    /// <summary>The handler's point may differ from the pointer function's by this many pixels, and the cursor from the pixel asked for.</summary>
    internal const float PixelToleranceUnits = 2f;

    /// <summary>The view moved the distance the flight arithmetic predicts, within <see cref="FlightTolerance"/> of it; a distance that is not a positive number proves nothing.</summary>
    internal static bool FlewExpected(float expected, float moved)
    {
        if (!IsReal(expected) || !IsReal(moved) || expected <= 0f)
            return false;
        return Math.Abs(moved - expected) <= FlightTolerance * expected;
    }

    /// <summary>The hero's speed over the window is under <see cref="HeroSpeedLimit"/>.</summary>
    internal static bool HeroStood(float speed) => IsReal(speed) && speed < HeroSpeedLimit;

    /// <summary>More than <see cref="FramesPerSecondFloor"/> owned frames per real second since the press; a window that is not a positive number proves nothing.</summary>
    internal static bool OwnedEnoughFrames(int frames, float seconds) =>
        IsReal(seconds) && seconds > 0f && frames > FramesPerSecondFloor * seconds;

    /// <summary>
    /// The height above the ground after a later entry is the one before the leave, within <see cref="HeightToleranceMetres"/> or
    /// <see cref="HeightToleranceShare"/> of it, whichever is larger, and the height before the leave was not the entry height: a re-entry
    /// that dropped the last flight comes back at the entry height, so a height that never left it would pass for the wrong reason.
    /// </summary>
    internal static bool HeightKept(float before, float after, float entryHeight)
    {
        if (!IsReal(before) || !IsReal(after) || !IsReal(entryHeight))
            return false;
        var tolerance = Math.Max(HeightToleranceMetres, HeightToleranceShare * before);
        return Math.Abs(before - entryHeight) > tolerance && Math.Abs(after - before) <= tolerance;
    }

    /// <summary>The dot of the displacement's direction with the view's forward direction (both in the ground plane); NaN when either has no length or is not a real number.</summary>
    internal static float ForwardDot(float dx, float dy, float forwardX, float forwardY)
    {
        if (!IsReal(dx) || !IsReal(dy) || !IsReal(forwardX) || !IsReal(forwardY))
            return float.NaN;
        var moved = (float)Math.Sqrt(dx * dx + dy * dy);
        var look = (float)Math.Sqrt(forwardX * forwardX + forwardY * forwardY);
        return moved > 0f && look > 0f ? (dx * forwardX + dy * forwardY) / (moved * look) : float.NaN;
    }

    /// <summary>The flight went along the view's forward direction: a dot of <see cref="ForwardDotFloor"/> or more. A flight backward or sideways by the right distance fails.</summary>
    internal static bool FlewForward(float dot) => IsReal(dot) && dot >= ForwardDotFloor;

    /// <summary>The camera is within <see cref="NearHeroMetres"/> of the hero: the engine's own view of him, third person or mounted.</summary>
    internal static bool NearHero(float distance) => IsReal(distance) && distance <= NearHeroMetres;

    /// <summary>The camera stands about the orbit distance from the kept target: within the tolerance on both sides, for a target that moves. A camera collapsed onto the target is not near it.</summary>
    internal static bool KeptNearTarget(float distance, float orbitDistance) =>
        IsReal(distance) && IsReal(orbitDistance) && Math.Abs(distance - orbitDistance) <= KeptToleranceMetres;

    /// <summary>The cursor sits where the step asked, within <see cref="PixelToleranceUnits"/> on each axis.</summary>
    internal static bool CursorPlaced(float wantedX, float wantedY, float pixelX, float pixelY) =>
        Within(wantedX, pixelX) && Within(wantedY, pixelY);

    /// <summary>The focus handler's reference point is the pointer function's value for the pixel read back.</summary>
    internal static bool FocusFollowed(SeatFocusReading reading) =>
        Within(reading.ExpectedX, reading.FieldX) && Within(reading.ExpectedY, reading.FieldY);

    /// <summary>The panel is open again within <see cref="ReopenFrameLimit"/> frames of the step's own waits after a close with no player key.</summary>
    internal static bool ReopenedPromptly(int frames) => frames >= 0 && frames <= ReopenFrameLimit;

    /// <summary>The company with the fewest units that holds no hero, or -1; the trial kills every unit of it.</summary>
    internal static int SmallestKillable(IReadOnlyList<(int Units, bool HoldsHero)> companies)
    {
        var best = -1;
        for (var i = 0; i < companies.Count; i++)
            if (companies[i].Units > 0 && !companies[i].HoldsHero && (best < 0 || companies[i].Units < companies[best].Units))
                best = i;
        return best;
    }

    /// <summary>The company with the most units (the first of equals), or -1; the trial keeps to it without killing anyone.</summary>
    internal static int Largest(IReadOnlyList<(int Units, bool HoldsHero)> companies)
    {
        var best = -1;
        for (var i = 0; i < companies.Count; i++)
            if (companies[i].Units > 0 && (best < 0 || companies[i].Units > companies[best].Units))
                best = i;
        return best;
    }

    /// <summary>The line a step writes: <c>[SeatTrial] step=&lt;name&gt; result=&lt;PASS|FAIL&gt; &lt;facts&gt;</c>. The facts are <c>key=value</c> words.</summary>
    internal static string Line(string step, bool pass, string facts) =>
        "[SeatTrial] step=" + step + " result=" + (pass ? "PASS" : "FAIL") + (facts.Length > 0 ? " " + facts : "");

    /// <summary>A number for a fact: one decimal, the invariant culture, and <c>nan</c> for anything that is not a real number.</summary>
    internal static string Num(float value) => IsReal(value) ? value.ToString("F1", CultureInfo.InvariantCulture) : "nan";

    private static bool Within(float expected, float actual) =>
        IsReal(expected) && IsReal(actual) && Math.Abs(expected - actual) <= PixelToleranceUnits;

    private static bool IsReal(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
