// Behavioral inspiration: Alliance mod's CS_Road triangle output (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using TaleWorlds.Library;

namespace TAOM.SceneScripts.Roads;

public readonly struct RoadTriangle
{
    public RoadTriangle(Vec3 p1, Vec2 uv1, Vec3 p2, Vec2 uv2, Vec3 p3, Vec2 uv3)
    {
        P1 = p1; UV1 = uv1;
        P2 = p2; UV2 = uv2;
        P3 = p3; UV3 = uv3;
    }

    public Vec3 P1 { get; }
    public Vec2 UV1 { get; }
    public Vec3 P2 { get; }
    public Vec2 UV2 { get; }
    public Vec3 P3 { get; }
    public Vec2 UV3 { get; }
}
