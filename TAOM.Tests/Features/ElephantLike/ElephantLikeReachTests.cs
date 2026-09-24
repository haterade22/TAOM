using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ElephantLike;
using TAOM.Features.MonsterSize;

namespace TAOM.Tests.Features.ElephantLike;

/// <summary>
/// The reach multiplier for a creature whose size lives on its Monster (#646): the mount's live agent scale, which the
/// engine reads back natively per call, so a value that is not finite, not positive or over 10x must fall back to 1
/// rather than zero or blow up the scan (the NaN-gate rule, csharp-architecture.md: NaN has to FAIL the gate); any
/// finite positive scale up to 10x is used as read.
/// </summary>
[TestClass]
public class ElephantLikeReachTests
{
    [DataTestMethod]
    [DataRow(1f)]
    [DataRow(1.1f)]
    [DataRow(1.5f)]
    [DataRow(3f)]
    [DataRow(0.05f)]
    [DataRow(float.Epsilon)]
    [DataRow(ElephantLikeReach.MaxScale)]
    public void Scale_FinitePositiveUpToMax_IsTheAgentScale(float agentScale)
    {
        Assert.AreEqual(agentScale, ElephantLikeReach.Scale(agentScale));
    }

    [TestMethod]
    public void Scale_TheEnginesScaleForEveryAcceptedMonsterSize_IsKept()
    {
        // The engine builds the scale in single precision (Mission.BuildAgent: 0.01f * (float)BodyLength); for the
        // minimum size 10 that is 0.099999994f, one step under 0.1f, which a floor tuned to 0.1 once refused. The loop
        // variable keeps the compiler from folding the product, so this multiplies the way the engine does.
        for (int n = MonsterSizeConfig.MinBodyLength; n <= MonsterSizeConfig.MaxBodyLength; n++)
        {
            float engineScale = 0.01f * (float)n;
            Assert.AreEqual(engineScale, ElephantLikeReach.Scale(engineScale), $"body_length {n}");
        }
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(11f)]
    public void Scale_NonFiniteNotPositiveOrOverMax_IsOne(float agentScale)
    {
        Assert.AreEqual(1f, ElephantLikeReach.Scale(agentScale));
    }
}
