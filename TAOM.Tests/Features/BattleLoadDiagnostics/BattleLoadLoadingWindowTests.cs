using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class BattleLoadLoadingWindowTests
{
    [TestInitialize]
    public void Setup() => BattleLoadLoadingWindow.Close();

    [TestCleanup]
    public void Cleanup() => BattleLoadLoadingWindow.Close();

    [TestMethod]
    public void IsOpen_WhenClosed_ReturnsFalse()
    {
        BattleLoadLoadingWindow.Close();
        Assert.IsFalse(BattleLoadLoadingWindow.IsOpen);
    }

    [TestMethod]
    public void IsOpen_AfterEnter_ReturnsTrue()
    {
        BattleLoadLoadingWindow.Enter();
        Assert.IsTrue(BattleLoadLoadingWindow.IsOpen);
    }

    [TestMethod]
    public void IsOpen_AfterEnterThenClose_ReturnsFalse()
    {
        BattleLoadLoadingWindow.Enter();
        BattleLoadLoadingWindow.Close();
        Assert.IsFalse(BattleLoadLoadingWindow.IsOpen);
    }

    [TestMethod]
    public void OpenedAtUtc_WhenClosed_ReturnsNull()
    {
        BattleLoadLoadingWindow.Close();
        Assert.IsNull(BattleLoadLoadingWindow.OpenedAtUtc);
    }

    [TestMethod]
    public void OpenedAtUtc_WhenOpen_HasValue()
    {
        BattleLoadLoadingWindow.Enter();
        Assert.IsTrue(BattleLoadLoadingWindow.OpenedAtUtc.HasValue);
    }
}
