using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapLoadDiagnostics;

namespace TAOM.Tests.Features.MapLoadDiagnostics;

/// <summary>
/// The engine lowers the loading window from the main menu, party screen and character creation on
/// every frame, and clears the flag whether or not it was up. Only a true-to-false change is a real
/// lower worth a caller chain; everything else is the per-frame no-op that filled 84 MB logs.
/// </summary>
[TestClass]
public class LoadingWindowTraceGateTests
{
    [TestMethod]
    public void IsRealLower_WindowWasUpAndIsNowDown_ReturnsTrue()
        => Assert.IsTrue(LoadingWindowTraceGate.IsRealLower(wasActive: true, isActiveNow: false));

    [TestMethod]
    public void IsRealLower_WindowWasAlreadyDown_ReturnsFalse()
        => Assert.IsFalse(LoadingWindowTraceGate.IsRealLower(wasActive: false, isActiveNow: false));

    [TestMethod]
    public void IsRealLower_WindowWasUpAndStayedUp_ReturnsFalse()
        => Assert.IsFalse(LoadingWindowTraceGate.IsRealLower(wasActive: true, isActiveNow: true));

    [TestMethod]
    public void IsRealLower_WindowWasDownAndIsNowUp_ReturnsFalse()
        => Assert.IsFalse(LoadingWindowTraceGate.IsRealLower(wasActive: false, isActiveNow: true));
}
