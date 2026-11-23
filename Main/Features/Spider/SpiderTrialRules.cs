using System.Collections.Generic;
using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.Spider;

/// <summary>
/// One read of the player's spider riders: the riders seen on a spider so far that still live, how many of those are still on a living
/// spider, the highest speed in metres per second of a living spider this tick (0 when none lives), the seconds the spiders
/// have slid so far (<see cref="SpiderSlideMeter"/>) and the seconds of fast movement the meter has judged so far.
/// </summary>
internal readonly record struct SpiderReading(int Alive, int Mounted, float TopSpeed, float SlideSeconds, float JudgedSeconds);

/// <summary>
/// The times and the pass rule of the spider gait step of the command trial (#1541): the rebuilt walk, trot, canter, gallop and stand
/// clips were bound in the game data and nobody had seen them move. The step checks what a number can: the spiders stand while held,
/// and run when charged. A person reads the pictures.
/// </summary>
internal static class SpiderTrialRules
{
    /// <summary>Real seconds the step waits for a living spider rider: the riders are built from the mission tick after deployment.</summary>
    internal const float SpiderWaitSeconds = 20f;

    /// <summary>Real seconds the held army stands before the charge.</summary>
    internal const float StandSeconds = 6f;

    /// <summary>The last seconds of the stand: <c>stand</c> is the highest mount speed in them, because the riders still walk into their ranks when the Stop order comes.</summary>
    internal const float StandWindowSeconds = 2f;

    /// <summary>Real seconds the army charges. The highest mount speed over them is <c>top</c>.</summary>
    internal const float ChargeSeconds = 14f;

    /// <summary>Real seconds the army is held again after the charge.</summary>
    internal const float StopSeconds = 6f;

    /// <summary>The last seconds of the stop wait: <c>stop</c> is the highest mount speed in them, after the spiders had time to slow down.</summary>
    internal const float StopWindowSeconds = 2f;

    /// <summary>Real seconds after the charge order at which the two charge pictures are taken. The stand picture is at the end of the stand, the stop picture at the end of the stop.</summary>
    internal static readonly IReadOnlyList<float> ChargePictureSeconds = new[] { 3f, 8f };

    /// <summary>The four pictures of the step: stand, two in the charge, stop.</summary>
    internal const int PictureCount = 4;

    /// <summary>The speed the charged spiders must reach: a spider that never gets above a slow walk did not take the charge.</summary>
    internal const float TopAtLeastMetresPerSecond = 3f;

    /// <summary>The speed the held spiders may show. A stand clip that slides the mount along would show here.</summary>
    internal const float StandAtMostMetresPerSecond = 1.5f;

    /// <summary>The seconds, summed over all the spiders, that fast spiders may show with legs that hardly move.</summary>
    internal const float SlideAtMostSeconds = 2f;

    /// <summary>
    /// The seconds of fast movement the slide meter must have judged for each rider. A run in which no leg could be read, the
    /// readings came too far apart, no spider reached the meter's speed, or one spider of many was read, gives a slide of 0 that
    /// measured too little. A charge of <see cref="ChargeSeconds"/> gives each spider about 13.
    /// </summary>
    internal const float JudgedAtLeastSecondsPerRider = 2f;

    /// <summary>The tip bone of each of the eight legs, front to back: the feet of the gait gate (<c>tools/check_gait_stride.py</c>).</summary>
    internal static readonly IReadOnlyList<string> LegTipBones = new[]
    {
        "joint44_l", "joint44_r", "joint38_l", "joint38_r", "joint32_l", "joint32_r", "joint26_l", "joint26_r",
    };

    /// <summary>
    /// <paramref name="stop"/> and <paramref name="mounted"/> are not in the rule: the riders may be in a fight by the end, and the facts show them to a person.
    /// A speed, a slide or a judged time that is no number fails.
    /// </summary>
    internal static bool Passes(int riders, float stand, float top, int pictures, float slide, float judged) =>
        riders >= 1
        && FiniteFloatValidator.IsFiniteAtLeast(top, TopAtLeastMetresPerSecond)
        && FiniteFloatValidator.IsFiniteAtMost(stand, StandAtMostMetresPerSecond)
        && FiniteFloatValidator.IsFiniteAtMost(slide, SlideAtMostSeconds)
        && FiniteFloatValidator.IsFiniteAtLeast(judged, JudgedAtLeastSecondsPerRider * riders)
        && pictures == PictureCount;

    internal static string Facts(int riders, int mounted, float stand, float top, float stop, int pictures, float slide, float judged) =>
        "riders=" + Whole(riders) + " mounted=" + Whole(mounted) + " stand=" + Tenth(stand) + " top=" + Tenth(top)
        + " stop=" + Tenth(stop) + " pictures=" + Whole(pictures) + " slide=" + Tenth(slide) + " judged=" + Tenth(judged);

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Tenth(float value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
