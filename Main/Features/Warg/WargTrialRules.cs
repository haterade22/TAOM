using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.Warg;

/// <summary>
/// One read of the player's warg riders: the riders seen on a warg so far that still live, how many still sit on a living warg, the
/// highest speed in metres per second of a living warg this tick, the highest speed of a living horse under a rider of the same
/// team (the control: when horses are slow too, the fault is the order or the battle, not the warg), the highest speed limit the
/// engine set on a living warg (<c>DrivenProperty.MountSpeed</c>), and the agent scale of a living warg (0 when none lives).
/// </summary>
internal readonly record struct WargReading(int Alive, int Mounted, float WargTop, float HorseTop, float WargLimit, float WargScale);

/// <summary>
/// The times and the pass rule of the warg gait step of the command trial (#1563): players reported AI warg riders that walk
/// everywhere. The step holds the army, charges it and reads how fast the wargs go beside the army's horsemen. It proves the speed
/// only. Which gait clip plays is not readable from managed code: a moving mount's channel 0 holds <c>act_none</c>, because the
/// native movement node plays the gaits (run 20261123-143843). The node picks the pace from the speed against the pace
/// limits of the clip records, and that speed is the agent's divided by its scale (read on native v1.5.6, not read again on
/// v1.5.7: docs/reference/lotrlome-warg-gait-changes.md), so the facts carry the scale, and a person judges the legs.
/// A PASS means the wargs ran. It never means the gaits are right: the step passed before the records were fixed.
/// </summary>
internal static class WargTrialRules
{
    /// <summary>Real seconds the step waits for a living warg rider: the riders are built from the mission tick after deployment.</summary>
    internal const float WargWaitSeconds = 20f;

    /// <summary>Real seconds the held army stands before the charge; <c>stand</c> is the highest warg speed in its last two seconds.</summary>
    internal const float StandSeconds = 6f;

    internal const float StandWindowSeconds = 2f;

    /// <summary>Real seconds the army charges. The highest warg and horse speeds come from them.</summary>
    internal const float ChargeSeconds = 14f;

    /// <summary>A warg that never gets above a fast walk did not run.</summary>
    internal const float TopAtLeastMetresPerSecond = 6f;

    /// <summary>A top speed that is no number fails.</summary>
    internal static bool Passes(int riders, float top) =>
        riders >= 1 && FiniteFloatValidator.IsFiniteAtLeast(top, TopAtLeastMetresPerSecond);

    internal static string Facts(int riders, int mounted, float stand, float top, float limit, float horse, float scale) =>
        "riders=" + Whole(riders) + " mounted=" + Whole(mounted) + " stand=" + Tenth(stand) + " top=" + Tenth(top) + " limit=" + Tenth(limit)
        + " horse=" + Tenth(horse) + " scale=" + scale.ToString("F2", CultureInfo.InvariantCulture);

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Tenth(float value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
