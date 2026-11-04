using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The keep's capture bar, with the arithmetic of StagedSiege's CaptureRule and this mode's own numbers (StagedSiegeTunables is
/// too wide to build for this). Attackers fill it when there are enough of them and they outnumber the defenders by the
/// dominance ratio; defenders drain it the same way; anything between holds it.
/// </summary>
internal static class KeepHoldRule
{
    internal static float Step(float progress, int attackers, int defenders, float dt, RunRules r)
    {
        if (float.IsNaN(progress) || float.IsInfinity(progress))
            progress = 0f;
        if (!(dt > 0f))
            return progress;

        if (attackers >= r.KeepMinAttackers && attackers >= r.KeepDominance * defenders)
        {
            var next = progress + dt / r.KeepHoldSeconds;
            return next >= 1f ? 1f : next > progress ? next : progress;
        }

        if (defenders >= 1 && defenders >= r.KeepDominance * attackers)
        {
            var next = progress - dt / r.KeepDecaySeconds;
            return next <= 0f ? 0f : next < progress ? next : progress;
        }

        return progress;
    }
}
