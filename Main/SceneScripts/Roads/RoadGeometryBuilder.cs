// Behavioral inspiration: Alliance mod's CS_Road mesh-building algorithm (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using System.Collections.Generic;
using TaleWorlds.Library;

namespace TAOM.SceneScripts.Roads;

public static class RoadGeometryBuilder
{
    public static IReadOnlyList<RoadTriangle> Build(
        IReadOnlyList<RoadSampleFrame> samples,
        float halfWidth,
        float elevationOffset,
        float repeatU,
        float repeatV,
        bool invertU,
        bool invertV,
        bool rotateUV,
        FlowAxis flowDirection,
        bool flipFaces)
    {
        var triangles = new List<RoadTriangle>();
        if (samples == null || samples.Count < 2) return triangles;

        var elevation = new Vec3(0f, 0f, elevationOffset);

        var lefts = new Vec3[samples.Count];
        var rights = new Vec3[samples.Count];
        var leftUVs = new Vec2[samples.Count];
        var rightUVs = new Vec2[samples.Count];

        float totalDistance = samples[samples.Count - 1].Distance - samples[0].Distance;
        float baseDistance = samples[0].Distance;

        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var center = sample.Origin + elevation;
            var offset = sample.Side * halfWidth;
            lefts[i] = center + offset;
            rights[i] = center - offset;

            float t = totalDistance > 0f
                ? (sample.Distance - baseDistance) / totalDistance
                : 0f;

            ComputeEdgeUVs(t, repeatU, repeatV, invertU, invertV, rotateUV, flowDirection,
                out leftUVs[i], out rightUVs[i]);
        }

        for (int i = 0; i < samples.Count - 1; i++)
        {
            EmitQuad(lefts[i], rights[i], lefts[i + 1], rights[i + 1],
                leftUVs[i], rightUVs[i], leftUVs[i + 1], rightUVs[i + 1],
                flipFaces, triangles);
        }

        return triangles;
    }

    internal static void ComputeEdgeUVs(
        float t,
        float repeatU,
        float repeatV,
        bool invertU,
        bool invertV,
        bool rotateUV,
        FlowAxis flowDirection,
        out Vec2 leftUV,
        out Vec2 rightUV)
    {
        float leftU, leftV, rightU, rightV;
        if (flowDirection == FlowAxis.AlongU)
        {
            leftU = rightU = t * repeatU;
            leftV = 0f;
            rightV = repeatV;
        }
        else
        {
            leftV = rightV = t * repeatV;
            leftU = 0f;
            rightU = repeatU;
        }

        if (rotateUV)
        {
            Swap(ref leftU, ref leftV);
            Swap(ref rightU, ref rightV);
        }
        if (invertU)
        {
            leftU = repeatU - leftU;
            rightU = repeatU - rightU;
        }
        if (invertV)
        {
            leftV = repeatV - leftV;
            rightV = repeatV - rightV;
        }

        leftUV = new Vec2(leftU, leftV);
        rightUV = new Vec2(rightU, rightV);
    }

    private static void EmitQuad(
        Vec3 leftA, Vec3 rightA, Vec3 leftB, Vec3 rightB,
        Vec2 leftUVA, Vec2 rightUVA, Vec2 leftUVB, Vec2 rightUVB,
        bool flipFaces,
        List<RoadTriangle> triangles)
    {
        if (!flipFaces)
        {
            triangles.Add(new RoadTriangle(
                leftA, leftUVA,
                rightA, rightUVA,
                rightB, rightUVB));
            triangles.Add(new RoadTriangle(
                leftA, leftUVA,
                rightB, rightUVB,
                leftB, leftUVB));
        }
        else
        {
            triangles.Add(new RoadTriangle(
                leftA, leftUVA,
                rightB, rightUVB,
                rightA, rightUVA));
            triangles.Add(new RoadTriangle(
                leftA, leftUVA,
                leftB, leftUVB,
                rightB, rightUVB));
        }
    }

    private static void Swap(ref float a, ref float b)
    {
        float tmp = a;
        a = b;
        b = tmp;
    }
}
