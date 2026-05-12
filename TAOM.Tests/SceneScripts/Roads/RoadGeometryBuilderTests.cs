// Tests for TAOM's clean-room reimplementation; behavioural inspiration: Alliance
// mod (GPL v3). See docs/scene-scripts/ATTRIBUTION.md.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.SceneScripts.Roads;

namespace TAOM.Tests.SceneScripts.Roads;

[TestClass]
public class RoadGeometryBuilderTests
{
    private static RoadSampleFrame Sample(float x, float distance)
    {
        // Straight road along +X axis: origin moves with x, side is constant +Y unit vector.
        return new RoadSampleFrame(new Vec3(x, 0f, 0f), new Vec3(0f, 1f, 0f), distance);
    }

    private static readonly RoadSampleFrame[] _twoSampleStraight =
    {
        Sample(0f, 0f),
        Sample(10f, 10f),
    };

    private static readonly RoadSampleFrame[] _threeSampleStraight =
    {
        Sample(0f, 0f),
        Sample(5f, 5f),
        Sample(10f, 10f),
    };

    [TestMethod]
    public void Build_TwoSamples_ProducesTwoTriangles()
    {
        var triangles = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 2f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        Assert.AreEqual(2, triangles.Count, "1 quad = 2 triangles");
    }

    [TestMethod]
    public void Build_ThreeSamples_ProducesFourTriangles()
    {
        var triangles = RoadGeometryBuilder.Build(
            _threeSampleStraight, halfWidth: 2f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        Assert.AreEqual(4, triangles.Count, "2 quads = 4 triangles");
    }

    [TestMethod]
    public void Build_SingleSample_ReturnsEmpty()
    {
        var triangles = RoadGeometryBuilder.Build(
            new[] { Sample(0f, 0f) }, halfWidth: 2f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        Assert.AreEqual(0, triangles.Count);
    }

    [TestMethod]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        var triangles = RoadGeometryBuilder.Build(
            new RoadSampleFrame[0], halfWidth: 2f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        Assert.AreEqual(0, triangles.Count);
    }

    [TestMethod]
    public void Build_NullInput_ReturnsEmpty()
    {
        var triangles = RoadGeometryBuilder.Build(
            null, halfWidth: 2f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        Assert.AreEqual(0, triangles.Count);
    }

    [TestMethod]
    public void Build_HalfWidth2_VerticesSpreadAcrossSideBy4Units()
    {
        var triangles = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 2f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        // Sample 0 at origin (0,0,0) with side +Y → left=(0,2,0), right=(0,-2,0)
        // Spread along Y across all vertices should be exactly 4.
        var allPoints = triangles
            .SelectMany(t => new[] { t.P1, t.P2, t.P3 })
            .ToList();
        float maxY = allPoints.Max(p => p.y);
        float minY = allPoints.Min(p => p.y);

        Assert.AreEqual(4f, maxY - minY, 0.0001f);
    }

    [TestMethod]
    public void Build_ElevationOffset05_AllVerticesLiftedBy05InZ()
    {
        var triangles = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0.5f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        var allPoints = triangles.SelectMany(t => new[] { t.P1, t.P2, t.P3 });

        Assert.IsTrue(allPoints.All(p => Math.Abs(p.z - 0.5f) < 0.0001f));
    }

    [TestMethod]
    public void Build_AlongU_FirstSampleU0_LastSampleUEqualsRepeatU()
    {
        var triangles = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 3f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        var allUVs = triangles.SelectMany(t => new[] { t.UV1, t.UV2, t.UV3 }).ToList();
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.X - 0f) < 0.0001f), "Some vertex must be at U=0");
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.X - 3f) < 0.0001f), "Some vertex must be at U=repeatU=3");
    }

    [TestMethod]
    public void Build_AlongU_VsAcrossWidth_VEdgesAt0AndRepeatV()
    {
        var triangles = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 2f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        var allUVs = triangles.SelectMany(t => new[] { t.UV1, t.UV2, t.UV3 }).ToList();
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.Y - 0f) < 0.0001f), "Some vertex at V=0 (left edge)");
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.Y - 2f) < 0.0001f), "Some vertex at V=repeatV=2 (right edge)");
    }

    [TestMethod]
    public void Build_AlongV_FlipsRoleOfUVAxes()
    {
        var triangles = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 2f, repeatV: 3f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongV, flipFaces: false);

        var allUVs = triangles.SelectMany(t => new[] { t.UV1, t.UV2, t.UV3 }).ToList();
        // AlongV: V ramps with path progress 0→repeatV=3; U is across width 0 or repeatU=2.
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.Y - 0f) < 0.0001f));
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.Y - 3f) < 0.0001f));
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.X - 0f) < 0.0001f));
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.X - 2f) < 0.0001f));
    }

    [TestMethod]
    public void Build_FlipFaces_ReversesVertexOrderingInEachTriangle()
    {
        var normal = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);
        var flipped = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 1f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: true);

        Assert.AreEqual(normal.Count, flipped.Count);
        // Same set of vertex positions appears in both; only winding differs.
        var normalPoints = normal.SelectMany(t => new[] { t.P1, t.P2, t.P3 }).OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToList();
        var flippedPoints = flipped.SelectMany(t => new[] { t.P1, t.P2, t.P3 }).OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToList();
        CollectionAssert.AreEqual(normalPoints, flippedPoints);

        // The ordering inside the first triangle should differ.
        var first = normal[0];
        var flipFirst = flipped[0];
        bool sameOrder = first.P1.Equals(flipFirst.P1) && first.P2.Equals(flipFirst.P2) && first.P3.Equals(flipFirst.P3);
        Assert.IsFalse(sameOrder, "FlipFaces should produce a different triangle vertex ordering");
    }

    [TestMethod]
    public void Build_InvertU_MirrorsAroundRepeatU()
    {
        var noInvert = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 3f, repeatV: 1f, invertU: false, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);
        var inverted = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 3f, repeatV: 1f, invertU: true, invertV: false,
            rotateUV: false, flowDirection: FlowAxis.AlongU, flipFaces: false);

        // Each U in noInvert maps to (repeatU - U) in inverted: pairs (0, 3) and (3, 0).
        var noInvertUs = noInvert.SelectMany(t => new[] { t.UV1.X, t.UV2.X, t.UV3.X }).Distinct().OrderBy(u => u).ToList();
        var invertedUs = inverted.SelectMany(t => new[] { t.UV1.X, t.UV2.X, t.UV3.X }).Distinct().OrderBy(u => u).ToList();
        // Set should be the same (reflection around 1.5) but each individual position should be mirrored.
        Assert.AreEqual(2, noInvertUs.Count);
        Assert.AreEqual(2, invertedUs.Count);
        // U=0 in original should map to U=3 in inverted, U=3 → U=0.
        // Sums of each set should both be 3 (0+3 and 3+0).
        Assert.AreEqual(3f, noInvertUs.Sum(), 0.0001f);
        Assert.AreEqual(3f, invertedUs.Sum(), 0.0001f);
    }

    [TestMethod]
    public void Build_RotateUV_SwapsUAndV()
    {
        var triangles = RoadGeometryBuilder.Build(
            _twoSampleStraight, halfWidth: 1f, elevationOffset: 0f,
            repeatU: 2f, repeatV: 3f, invertU: false, invertV: false,
            rotateUV: true, flowDirection: FlowAxis.AlongU, flipFaces: false);

        var allUVs = triangles.SelectMany(t => new[] { t.UV1, t.UV2, t.UV3 }).ToList();
        // Without rotation, AlongU + repeatU=2 + repeatV=3 yields U∈{0,2}, V∈{0,3}.
        // With rotateUV, axes swap → V∈{0,2}, U∈{0,3}.
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.Y - 2f) < 0.0001f), "After rotate, V picks up the old U value");
        Assert.IsTrue(allUVs.Any(uv => Math.Abs(uv.X - 3f) < 0.0001f), "After rotate, U picks up the old V value");
    }

    [TestMethod]
    public void ComputeEdgeUVs_AlongU_LeftEdgeAt0WhenNoInvert()
    {
        RoadGeometryBuilder.ComputeEdgeUVs(
            0.5f, repeatU: 1f, repeatV: 1f,
            invertU: false, invertV: false, rotateUV: false,
            FlowAxis.AlongU, out var left, out var right);

        Assert.AreEqual(0.5f, left.X, 0.0001f, "Left U lerps with progress");
        Assert.AreEqual(0f, left.Y, 0.0001f, "Left edge V=0");
        Assert.AreEqual(0.5f, right.X, 0.0001f, "Right U lerps with progress");
        Assert.AreEqual(1f, right.Y, 0.0001f, "Right edge V=repeatV");
    }
}
