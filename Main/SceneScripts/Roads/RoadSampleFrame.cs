// Behavioral inspiration: Alliance mod's CS_Road sample data (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using TaleWorlds.Library;

namespace TAOM.SceneScripts.Roads;

public readonly struct RoadSampleFrame
{
    public RoadSampleFrame(Vec3 origin, Vec3 side, float distance)
    {
        Origin = origin;
        Side = side;
        Distance = distance;
    }

    public Vec3 Origin { get; }
    public Vec3 Side { get; }
    public float Distance { get; }
}
