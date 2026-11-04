using System;
using System.Globalization;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// The numbers and judgments the field trial's passes rest on (build-spec section 12), with no engine type so a test pins each one.
/// The client's script measures; these decide.
/// </summary>
internal static class SeatTrialRules
{
    /// <summary>Mission seconds into the deployment at which the trial finishes it (once the refusal step has reported).</summary>
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
    internal const int FramesFloor = 100;
    internal const float HeightToleranceMetres = 1f;
    internal const float HeightToleranceShare = 0.1f;
    internal const float NearHeroMetres = 10f;

    /// <summary>The view moved the distance the flight arithmetic predicts, within <see cref="FlightTolerance"/> of it; a distance that is not a positive number proves nothing.</summary>
    internal static bool FlewExpected(float expected, float moved)
    {
        if (!IsReal(expected) || !IsReal(moved) || expected <= 0f)
            return false;
        return Math.Abs(moved - expected) <= FlightTolerance * expected;
    }

    /// <summary>The hero's speed over the window is under <see cref="HeroSpeedLimit"/>.</summary>
    internal static bool HeroStood(float speed) => IsReal(speed) && speed < HeroSpeedLimit;

    /// <summary>More than <see cref="FramesFloor"/> frames were owned, counted from the entry.</summary>
    internal static bool OwnedEnoughFrames(int frames) => frames > FramesFloor;

    /// <summary>The height above the ground after a later entry is the one before the leave, within <see cref="HeightToleranceMetres"/> or <see cref="HeightToleranceShare"/> of it, whichever is larger.</summary>
    internal static bool HeightKept(float before, float after)
    {
        if (!IsReal(before) || !IsReal(after))
            return false;
        return Math.Abs(after - before) <= Math.Max(HeightToleranceMetres, HeightToleranceShare * before);
    }

    /// <summary>The camera is within <see cref="NearHeroMetres"/> of the hero: the engine's own view of him, third person or mounted.</summary>
    internal static bool NearHero(float distance) => IsReal(distance) && distance <= NearHeroMetres;

    /// <summary>The line a step writes: <c>[SeatTrial] step=&lt;name&gt; result=&lt;PASS|FAIL&gt; &lt;facts&gt;</c>. The facts are <c>key=value</c> words.</summary>
    internal static string Line(string step, bool pass, string facts) =>
        "[SeatTrial] step=" + step + " result=" + (pass ? "PASS" : "FAIL") + (facts.Length > 0 ? " " + facts : "");

    /// <summary>A number for a fact: one decimal, the invariant culture, and <c>nan</c> for anything that is not a real number.</summary>
    internal static string Num(float value) => IsReal(value) ? value.ToString("F1", CultureInfo.InvariantCulture) : "nan";

    private static bool IsReal(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
