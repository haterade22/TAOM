using System.Collections.Generic;
using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.Spider;

/// <summary>
/// One read of the player's spider riders: the riders seen on a spider so far that still live, how many of those are still on a living
/// spider, and the highest speed in metres per second of a living spider this tick (0 when none lives).
/// </summary>
internal readonly record struct SpiderReading(int Alive, int Mounted, float TopSpeed);

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

    /// <summary>
    /// <paramref name="stop"/> and <paramref name="mounted"/> are not in the rule: the riders may be in a fight by the end, and the facts show them to a person.
    /// A speed that is no number fails.
    /// </summary>
    internal static bool Passes(int riders, float stand, float top, int pictures) =>
        riders >= 1
        && FiniteFloatValidator.IsFiniteAtLeast(top, TopAtLeastMetresPerSecond)
        && FiniteFloatValidator.IsFiniteAtMost(stand, StandAtMostMetresPerSecond)
        && pictures == PictureCount;

    internal static string Facts(int riders, int mounted, float stand, float top, float stop, int pictures) =>
        "riders=" + Whole(riders) + " mounted=" + Whole(mounted) + " stand=" + Tenth(stand) + " top=" + Tenth(top)
        + " stop=" + Tenth(stop) + " pictures=" + Whole(pictures);

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Tenth(float value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
