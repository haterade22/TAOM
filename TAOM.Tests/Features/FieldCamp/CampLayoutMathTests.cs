using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCamp.Visuals;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Pins the pure layout maths behind the camp visuals. The layout stores nothing between shows:
/// determinism of the index-seeded noise is what keeps a camp's tents in the same spots across
/// re-shows and save loads, and the jitter bands are what keep tents out of the command tent and
/// barricades in a closed ring. A change to any of these silently rearranges every player camp.
/// </summary>
[TestClass]
public class CampLayoutMathTests
{
    private const int SampleCount = 1000;

    // --- noise ---

    [TestMethod]
    public void Noise_SameIndex_ReturnsSameValue()
    {
        for (int i = -5; i < 50; i++)
            Assert.AreEqual(CampLayoutMath.Noise(i), CampLayoutMath.Noise(i), 0f,
                "Noise must be deterministic per index; the layout stores nothing between shows.");
    }

    [TestMethod]
    public void Noise_FirstThousandIndices_StayInUnitRange()
    {
        for (int i = 0; i < SampleCount; i++)
        {
            float value = CampLayoutMath.Noise(i);
            Assert.IsTrue(value >= 0f && value < 1f, $"Noise({i}) = {value} escaped [0, 1).");
        }
    }

    [TestMethod]
    public void Noise_AcrossIndices_ActuallyVaries()
    {
        // A constant function satisfies the range test; the jitter only works if values spread.
        float first = CampLayoutMath.Noise(0);
        bool varies = false;
        for (int i = 1; i < 20 && !varies; i++)
            varies = Math.Abs(CampLayoutMath.Noise(i) - first) > 0.01f;
        Assert.IsTrue(varies, "Noise returned (near-)identical values for 20 consecutive indices.");
    }

    // --- tent count scaling ---

    [TestMethod]
    public void ScaledTentCount_TinyParty_ClampsToMinimum()
    {
        // Field camp band is [2, 10]: even a solo party pitches a recognizable camp.
        Assert.AreEqual(2, CampLayoutMath.ScaledTentCount(0, 2, 10));
        Assert.AreEqual(2, CampLayoutMath.ScaledTentCount(15, 2, 10));
    }

    [TestMethod]
    public void ScaledTentCount_HugeParty_ClampsToMaximum()
    {
        Assert.AreEqual(10, CampLayoutMath.ScaledTentCount(1000, 2, 10));
        // Fortified band is [7, 18].
        Assert.AreEqual(18, CampLayoutMath.ScaledTentCount(1000, 7, 18));
    }

    [TestMethod]
    public void ScaledTentCount_MidBandParty_ScalesByTroopsPerTent()
    {
        Assert.AreEqual(8, CampLayoutMath.TroopsPerSmallTent);
        Assert.AreEqual(5, CampLayoutMath.ScaledTentCount(40, 2, 10));
        Assert.AreEqual(10, CampLayoutMath.ScaledTentCount(80, 7, 18));
    }

    [TestMethod]
    public void ScaledTentCount_FortifiedMinimum_BeatsFieldMaximumFloor()
    {
        // A small party's fortified camp still shows 7 tents: the fortified silhouette must read
        // as bigger than a field camp regardless of roster size.
        Assert.AreEqual(7, CampLayoutMath.ScaledTentCount(8, 7, 18));
    }

    // --- ring geometry ---

    [TestMethod]
    public void TentSlotAngle_Jitter_StaysWithinHalfRadianOfBaseSlot()
    {
        const int count = 10;
        for (int i = 0; i < count; i++)
        {
            float baseAngle = (float)(2.0 * Math.PI * i / count);
            float jitter = Math.Abs(CampLayoutMath.TentSlotAngle(i, count) - baseAngle);
            Assert.IsTrue(jitter <= 0.5f + 1e-4f,
                $"Tent slot {i} drifted {jitter} rad from its base slot; neighbours would overlap.");
        }
    }

    [TestMethod]
    public void TentSlotDistance_AllSlots_StayInsideJitterBand()
    {
        const float radius = 0.9f;
        for (int i = 0; i < SampleCount; i++)
        {
            float distance = CampLayoutMath.TentSlotDistance(i, radius);
            Assert.IsTrue(distance >= radius * 0.65f - 1e-4f && distance <= radius * 1.25f + 1e-4f,
                $"Tent slot {i} distance {distance} escaped the [0.65r, 1.25r] band.");
        }
    }

    [TestMethod]
    public void TentSlotFacing_PointsRoughlyBackAtCenter()
    {
        const int count = 10;
        for (int i = 0; i < count; i++)
        {
            float expected = CampLayoutMath.TentSlotAngle(i, count) + (float)Math.PI;
            float wobble = Math.Abs(CampLayoutMath.TentSlotFacing(i, count) - expected);
            Assert.IsTrue(wobble <= 0.4f + 1e-4f,
                $"Tent slot {i} faces {wobble} rad off center; door-away tents look wrong.");
        }
    }

    [TestMethod]
    public void BarricadeSlotAngle_IsEvenlySpacedWithNoJitter()
    {
        const int count = 8;
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual((float)(2.0 * Math.PI * i / count), CampLayoutMath.BarricadeSlotAngle(i, count), 1e-5f,
                "Barricade angles must be exact: angular jitter opens gaps in the defensive ring.");
        }
    }

    [TestMethod]
    public void BarricadeSlotDistance_AllSlots_StayInsideTightBand()
    {
        const float radius = 1.8f;
        for (int i = 0; i < SampleCount; i++)
        {
            float distance = CampLayoutMath.BarricadeSlotDistance(i, radius);
            Assert.IsTrue(distance >= radius * 0.9f - 1e-4f && distance <= radius * 1.1f + 1e-4f,
                $"Barricade slot {i} distance {distance} escaped the [0.9r, 1.1r] band.");
        }
    }
}
