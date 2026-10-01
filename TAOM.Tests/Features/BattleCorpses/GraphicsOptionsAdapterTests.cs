using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Features.BattleCorpses;

/// <summary>
/// The ragdoll option arrives from native as a float. (int)NaN and (int)±Infinity are int.MinValue on
/// net472, so the cast is guarded: every degenerate value must come back as the -1 the advisor reads as
/// "unknown", never as a number it would compare.
/// </summary>
[TestClass]
public class GraphicsOptionsAdapterTests
{
    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-0.1f)]
    [DataRow(5.1f)]
    [DataRow(-1f)]
    public void ToOptionIndex_DegenerateOrOutOfRange_IsUnknown(float raw)
    {
        Assert.AreEqual(-1, GraphicsOptionsAdapter.ToOptionIndex(raw, 5));
    }

    [DataTestMethod]
    [DataRow(0f, 0)]
    [DataRow(3f, 3)]
    [DataRow(5f, 5)]
    [DataRow(2.9999f, 3)]
    public void ToOptionIndex_InRange_RoundsToTheIndex(float raw, int expected)
    {
        Assert.AreEqual(expected, GraphicsOptionsAdapter.ToOptionIndex(raw, 5));
    }
}
