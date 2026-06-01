using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class BattleLoadStallWatchdogTests
{
    [TestMethod]
    public void ShouldFire_WindowOpenPastThresholdAndNotFired_ReturnsTrue()
        => Assert.IsTrue(BattleLoadStallWatchdog.ShouldFire(true, 50d, 45d, false));

    [TestMethod]
    public void ShouldFire_ExactlyThreshold_ReturnsTrue()
        => Assert.IsTrue(BattleLoadStallWatchdog.ShouldFire(true, 45d, 45d, false));

    [TestMethod]
    public void ShouldFire_BelowThreshold_ReturnsFalse()
        => Assert.IsFalse(BattleLoadStallWatchdog.ShouldFire(true, 10d, 45d, false));

    [TestMethod]
    public void ShouldFire_AlreadyFired_ReturnsFalse()
        => Assert.IsFalse(BattleLoadStallWatchdog.ShouldFire(true, 99d, 45d, true));

    [TestMethod]
    public void ShouldFire_WindowClosed_ReturnsFalse()
        => Assert.IsFalse(BattleLoadStallWatchdog.ShouldFire(false, 999d, 45d, false));
}
