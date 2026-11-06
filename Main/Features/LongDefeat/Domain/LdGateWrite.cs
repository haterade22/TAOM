using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One visible gate around a fortification write, as the adapter read it before and after: <c>Disabled</c> and <c>Closed</c> (the engine's gate state) after the write, <c>Near</c> the defenders within
/// <see cref="GateRules.NearRadius"/> of it after a repair (R3), and <c>Touched</c> whether the write acted on it at all (a repair leaves a gate that is whole alone).
/// </summary>
internal readonly record struct LdGateChange(LdGateFacts Before, LdGateFacts After, bool Disabled, bool Closed, int Near, bool Touched);

/// <summary>
/// What a gate write did: every visible gate's change, how many of the hidden gates the boot broke still stand broken (<c>HiddenTotal</c> of them, <c>HiddenStanding</c> not destroyed: a repair must leave it 0), and
/// whether the vanilla reset the repair rests on was found by name (D17, R10).
/// </summary>
internal sealed record LdGateWrite(IReadOnlyList<LdGateChange> Gates, int HiddenTotal, int HiddenStanding, bool ResetFound)
{
    /// <summary>The gates the write acted on.</summary>
    public IEnumerable<LdGateChange> Touched => Gates.Where(g => g.Touched);

    /// <summary>Which repair path it was: <c>broken</c> when any gate it acted on was destroyed, else <c>damaged</c> (D17).</summary>
    public string Path => Touched.Any(g => g.Before.Destroyed) ? "broken" : "damaged";
}

/// <summary>The fixed distances of the gate checks.</summary>
internal static class GateRules
{
    /// <summary>R3: a defender standing this close to a gate when a repair closes it may be caught in the door; the repair line counts them.</summary>
    public const float NearRadius = 4f;

    /// <summary>The attackers sampled this far behind the repaired gate's plane count as having come through it (approved plan, S4 row).</summary>
    public const float BehindDepth = 3f;

    /// <summary>How far either side of the gate's centre the plane test reaches: the door's own width, a first guess, so an attacker on a ladder elsewhere on the wall is not counted as coming through the gate.</summary>
    public const float HalfWidth = 5f;

    /// <summary>How far above or below the gate's own height an attacker may stand and still count as in the doorway: a first guess, enough for a slope behind the door and well under the wall walk above it (test S2 saw up to 57 attackers on the walls).</summary>
    public const float HeightBand = 4f;
}

/// <summary>
/// A repaired gate under watch until the next clear or the run's end (<c>gate-attacked</c>): the wave the repair precedes, the hit points the gates held when the watch last began (the repair, or a gate level
/// bought after it), how many of the 5-second samples found an attacker behind a standing gate's plane, and how many samples were taken. Main thread.
/// </summary>
internal sealed class GateWatch
{
    public GateWatch(int wave, float baseline, string path)
    {
        Wave = wave;
        Baseline = baseline;
        Path = path;
    }

    public int Wave { get; }

    public string Path { get; }

    /// <summary>The summed hit points of the visible gates right after the repair, or after a later level write (which keeps their share).</summary>
    public float Baseline { get; private set; }

    public int Samples { get; private set; }

    public int Through { get; private set; }

    public void Rebase(float baseline) => Baseline = baseline;

    /// <summary>One 5-second sample. A negative <paramref name="behind"/> is the field saying it could not measure (no keep, no plane, no standing gate): it is not counted, so <c>samples=</c> is the number of real readings.</summary>
    public void Sample(int behind)
    {
        if (behind < 0)
            return;

        Samples++;
        Through += behind;
    }
}
