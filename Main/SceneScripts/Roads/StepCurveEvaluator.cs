// Behavioral inspiration: Alliance mod's CS_Road StepCurve evaluation (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using System;
using System.Collections.Generic;

namespace TAOM.SceneScripts.Roads;

public static class StepCurveEvaluator
{
    public static float Evaluate(IReadOnlyList<StepKey> curve, float percent)
    {
        if (curve == null || curve.Count == 0) return 1f;
        if (curve.Count == 1) return curve[0].Step;

        if (percent <= curve[0].Percent) return curve[0].Step;
        if (percent >= curve[curve.Count - 1].Percent) return curve[curve.Count - 1].Step;

        for (int i = 0; i < curve.Count - 1; i++)
        {
            var a = curve[i];
            var b = curve[i + 1];
            if (percent < a.Percent || percent > b.Percent) continue;

            float span = b.Percent - a.Percent;
            if (span <= 0f) return a.Step;

            float t = (percent - a.Percent) / span;
            return a.Step + (b.Step - a.Step) * t;
        }

        return curve[curve.Count - 1].Step;
    }
}
