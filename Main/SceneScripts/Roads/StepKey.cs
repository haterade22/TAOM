// Behavioral inspiration: Alliance mod's CS_Road StepCurve key struct (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
namespace TAOM.SceneScripts.Roads;

public readonly struct StepKey
{
    public StepKey(float percent, float step)
    {
        Percent = percent;
        Step = step;
    }

    public float Percent { get; }
    public float Step { get; }
}
