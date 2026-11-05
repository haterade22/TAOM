using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What the focus step read: the pixel the cursor stands on and the pixel it asked for, the pointer function's value for it, and the handler's own point.</summary>
internal readonly record struct SeatFocusReading(float PixelX, float PixelY, float WantedX, float WantedY, float ExpectedX, float ExpectedY,
    float FieldX, float FieldY, string Focused);

/// <summary>What the take step read after the take: whether the player team's order controller names the new main agent, how many formations he owns, and how many of those the AI still runs.</summary>
internal readonly record struct SeatHandOverReading(bool ControllerOwnerIsMain, int OwnedByMain, int StillAi);

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

    /// <summary>Real seconds the trial waits for the hero to be on the AI after the entry, and then lets him fight on in his charging company.</summary>
    internal const float HandOffWaitSeconds = 3f;

    internal const float FightWindowSeconds = 5f;

    /// <summary>Real seconds after the trial writes a morale of 0 on the handed hero before it reads whether he panicked or retreated.</summary>
    internal const float MoraleWindowSeconds = 2.5f;

    /// <summary>The most a hero reads right after the trial wrote a morale of 0: the engine pins a refused zero at this value (CommonAIComponent.cs:57-61).</summary>
    internal const float MoraleFloor = 0.01f;

    /// <summary>The handed hero in a charging company moves more than this many metres in the window; a hero who stands in place moves less.</summary>
    internal const float FoughtOnMetres = 1f;

    /// <summary>
    /// Real seconds <c>hero_falls</c> waits after the kill for the Seat's reaction: the engine clears the main agent inside the kill, and the Seat
    /// lowers the gate and says the fall at its next mission tick (a few milliseconds at game speed). A gate that is still up, or no message, after
    /// this long is a fault.
    /// </summary>
    internal const float FallWaitSeconds = 3f;

    /// <summary>Real seconds the view flies under a pause, and the most the mission clock may move in them.</summary>
    internal const float PauseWindowSeconds = 1.5f;

    internal const float PausedClockLimit = 0.05f;

    /// <summary>Real seconds a time level is held before its ratio is measured, and the window the ratio is measured over.</summary>
    internal const float TimeSettleSeconds = 0.3f;

    internal const float TimeWindowSeconds = 2f;

    /// <summary>A measured ratio of mission seconds to real seconds matches its expectation within this share of it.</summary>
    internal const float RatioTolerance = 0.25f;

    /// <summary>The ratio level 0.5 must read.</summary>
    internal const float SlowTarget = 0.5f;

    /// <summary>Level 10 is the engine's fast forward: it must read more than this.</summary>
    internal const float FastForwardRatioFloor = 5f;

    /// <summary>The ratio of the engine's fast forward with no fixed step and no request: nine more steps of the frame, then the frame itself (MissionState.cs:173, 196).</summary>
    internal const float FastForwardTarget = 10f;

    /// <summary>Real seconds a step waits for the Seat to answer another writer's flag, and for its level to fall when the flag does.</summary>
    internal const float AdoptWaitSeconds = 1f;

    /// <summary>Real seconds the view settles after leaving the Seat, and after the panel opens or closes, before the camera is read.</summary>
    internal const float RiseSettleSeconds = 1f;

    /// <summary>The panel's lift must show at least this share of its height in the camera's Z.</summary>
    internal const float LiftShare = 0.5f;

    /// <summary>The view is back within this many metres of where it was once the panel has closed.</summary>
    internal const float SankBackMetres = 1f;

    /// <summary>The ids of the engine's own time speed requests (the radial menu, the equip wheel, the order panel, the cheat key): a ratio measured with one held is not the Seat's.</summary>
    internal static readonly int[] EngineRequestIds = { 813, 624, 864, 1121 };

    /// <summary>The mission clock stood still: it moved less than <see cref="PausedClockLimit"/> in the window. A reading that is not a number proves nothing.</summary>
    internal static bool PausedMissionStood(float missionSeconds) => IsReal(missionSeconds) && missionSeconds < PausedClockLimit;

    /// <summary>Mission seconds over real seconds read half, within <see cref="RatioTolerance"/> of it.</summary>
    internal static bool SlowRatio(float ratio) => RatioMatches(ratio, SlowTarget);

    /// <summary>The mission seconds one frame of <paramref name="frameSeconds"/> holds at a level: the fixed step of <c>FordsGameSpeed.Frame</c>, and none at 1 or 10.</summary>
    internal static float FixedStepSeconds(int level, float frameSeconds)
    {
        if (!IsReal(frameSeconds))
            return 0f;
        var speed = new FordsGameSpeed();
        speed.Set(level);
        return speed.Frame(frameSeconds).FixedStep;
    }

    /// <summary>A clock still at normal speed would miss the expected ratio by more than <see cref="RatioTolerance"/>; a low frame rate caps the step until 2 and 4 read as 1.</summary>
    internal static bool CanTellFromNormal(float expectedRatio) => IsReal(expectedRatio) && expectedRatio > 0f && !RatioMatches(1f, expectedRatio);

    /// <summary>
    /// At 2 and 4 the ratio matches the expected ratio, the capped steps the frames held over the window, within <see cref="RatioTolerance"/> of it; at 10 it is
    /// above <see cref="FastForwardRatioFloor"/>. An expected ratio that is not a positive number proves nothing.
    /// </summary>
    internal static bool FastRatio(int level, float ratio, float expectedRatio) =>
        level >= 10 ? IsReal(ratio) && ratio > FastForwardRatioFloor : RatioMatches(ratio, expectedRatio);

    /// <summary>Another writer's fast forward reads <see cref="FastForwardTarget"/> within <see cref="RatioTolerance"/>: a fixed step of 2 or 4 left on would read 20 or 40, and a request of 0.5 would read 5.</summary>
    internal static bool AdoptedRatio(float ratio) => RatioMatches(ratio, FastForwardTarget);

    /// <summary>The camera rose at least <see cref="LiftShare"/> of the lift height above where it stood before the panel opened; no height proves nothing.</summary>
    internal static bool LiftSeen(float before, float after, float height) =>
        IsReal(before) && IsReal(after) && IsReal(height) && height > 0f && after - before >= LiftShare * height;

    /// <summary>The camera is within <see cref="SankBackMetres"/> of where it stood before the panel opened.</summary>
    internal static bool SankBack(float before, float after) => IsReal(before) && IsReal(after) && Math.Abs(after - before) <= SankBackMetres;

    /// <summary>The camera target offset is exactly zero on all three axes; the target lock tests it for exact zero, so a small leftover is a failure.</summary>
    internal static bool ExactlyZero(float x, float y, float z) => IsReal(x) && IsReal(y) && IsReal(z) && x == 0f && y == 0f && z == 0f;

    /// <summary>The morale write took effect: a hero with a morale component reads between 0 and <see cref="MoraleFloor"/>; no component reads -1, and a value that is not a number proves nothing.</summary>
    internal static bool AtMoraleFloor(float morale) => IsReal(morale) && morale >= 0f && morale <= MoraleFloor;

    /// <summary>The handed hero fought: he moved more than <see cref="FoughtOnMetres"/>.</summary>
    internal static bool FoughtOn(float moved) => IsReal(moved) && moved > FoughtOnMetres;

    /// <summary>
    /// The take handed the command over: the controller names the new agent, he owns exactly the formations the fallen hero owned (and there was
    /// at least one), and the AI runs none of them.
    /// </summary>
    internal static bool CommandHandedOver(int ownedBefore, SeatHandOverReading after) =>
        after.ControllerOwnerIsMain && ownedBefore > 0 && after.OwnedByMain == ownedBefore && after.StillAi == 0;

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

    private static bool RatioMatches(float ratio, float expected) =>
        IsReal(ratio) && IsReal(expected) && expected > 0f && Math.Abs(ratio - expected) <= RatioTolerance * expected;

    private static bool IsReal(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
