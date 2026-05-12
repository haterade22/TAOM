// Behavioral inspiration: Alliance mod's CS_Road StepCurve parser (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.SceneScripts.Roads;

public static class StepCurveParser
{
    public const float MinStep = 0.1f;
    public const float MinPercent = 0f;
    public const float MaxPercent = 100f;

    private static readonly IReadOnlyList<StepKey> _defaultCurve = new[]
    {
        new StepKey(MinPercent, 1f),
        new StepKey(MaxPercent, 1f),
    };

    public static IReadOnlyList<StepKey> DefaultCurve => _defaultCurve;

    public static IReadOnlyList<StepKey> Parse(string input)
    {
        return TryParse(input, out var result) ? result : _defaultCurve;
    }

    public static bool TryParse(string input, out IReadOnlyList<StepKey> curve)
    {
        curve = _defaultCurve;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var keys = new List<StepKey>();
        foreach (var pair in input.Split(','))
        {
            var cleaned = pair.Trim().Trim('{', '}').Trim();
            if (cleaned.Length == 0) continue;

            var colonIndex = cleaned.IndexOf(':');
            if (colonIndex <= 0 || colonIndex == cleaned.Length - 1) continue;

            var percentText = cleaned.Substring(0, colonIndex).Trim();
            var stepText = cleaned.Substring(colonIndex + 1).Trim();

            if (!float.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent)) continue;
            if (!float.TryParse(stepText, NumberStyles.Float, CultureInfo.InvariantCulture, out float step)) continue;

            if (float.IsNaN(percent) || float.IsInfinity(percent)) continue;
            if (float.IsNaN(step) || float.IsInfinity(step)) continue;

            if (percent < MinPercent) percent = MinPercent;
            if (percent > MaxPercent) percent = MaxPercent;
            if (step < MinStep) step = MinStep;

            keys.Add(new StepKey(percent, step));
        }

        if (keys.Count == 0) return false;

        keys.Sort((a, b) => a.Percent.CompareTo(b.Percent));
        curve = keys;
        return true;
    }
}
