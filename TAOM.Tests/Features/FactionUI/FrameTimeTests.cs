using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.UI;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. The themed screens' animations finish on a <c>progress &gt;= 1</c> test, which a NaN
/// never passes, so a NaN frame time from the engine must hold the animations rather than poison them.
/// </summary>
[TestClass]
public class FrameTimeTests
{
    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-0.016f)]
    public void Sanitize_ANonFiniteOrNegativeFrameTime_HoldsTheAnimations(float dt)
    {
        Assert.AreEqual(0f, FrameTime.Sanitize(dt));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.016f)]
    [DataRow(0.5f)]
    public void Sanitize_AFiniteNonNegativeFrameTime_IsKept(float dt)
    {
        Assert.AreEqual(dt, FrameTime.Sanitize(dt));
    }
}
