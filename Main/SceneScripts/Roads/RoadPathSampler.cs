// Behavioral inspiration: Alliance mod's CS_Road sampling loop (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.SceneScripts.Roads;

public static class RoadPathSampler
{
    public static List<float> SampleDistances(
        IReadOnlyList<StepKey> curve,
        float totalDistance,
        float minStep)
    {
        var distances = new List<float>();

        if (!FiniteFloatValidator.IsFinite(totalDistance) || totalDistance <= 0f) return distances;
        if (!FiniteFloatValidator.IsFinite(minStep) || minStep <= 0f) minStep = 0.1f;

        float d = 0f;
        while (d < totalDistance)
        {
            distances.Add(d);
            float percent = d / totalDistance * 100f;
            float step = StepCurveEvaluator.Evaluate(curve, percent);
            if (!FiniteFloatValidator.IsFinite(step) || step < minStep) step = minStep;
            d += step;
        }

        distances.Add(totalDistance);
        return distances;
    }
}
