using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// A run on a field scene with no walls (#1105 slice 2): where each side starts. A null place takes the scene's own spawn path for that side. A plan with no
/// <see cref="FieldPlaces"/> is a siege run. <paramref name="AttackersBehind"/> is a second start place for the attackers of a defence, on the far side of the
/// defenders, so a new group never has to appear where the fight has moved to.
/// </summary>
internal sealed record FieldPlaces(MusterFrame? Defenders, MusterFrame? Attackers, MusterFrame? AttackersBehind = null)
{
    /// <summary>
    /// The start place of the attackers' next group: of the two places, the one farther from the mean point of the standing <paramref name="defenders"/>.
    /// With one place, or with nobody standing, it is <see cref="Attackers"/>.
    /// </summary>
    public MusterFrame? AttackersFartherFrom(IReadOnlyList<LdPoint> defenders)
    {
        if (Attackers == null || AttackersBehind == null)
            return Attackers ?? AttackersBehind;

        double x = 0d, y = 0d;
        var count = 0;
        foreach (var point in defenders)
        {
            if (!FiniteFloatValidator.IsFinite(point.X) || !FiniteFloatValidator.IsFinite(point.Y))
                continue;

            x += point.X;
            y += point.Y;
            count++;
        }

        if (count == 0)
            return Attackers;

        x /= count;
        y /= count;
        return Square(AttackersBehind.X - x, AttackersBehind.Y - y) > Square(Attackers.X - x, Attackers.Y - y) ? AttackersBehind : Attackers;
    }

    private static double Square(double dx, double dy) => dx * dx + dy * dy;
}
