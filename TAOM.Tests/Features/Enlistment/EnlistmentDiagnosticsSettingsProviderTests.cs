using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Fail-direction pin for the enlistment diagnostics toggle. The polarity here is DELIBERATELY
/// inverted against <c>BattleLoadDiagnosticsSettingsProvider</c>, which resolves a missing MCM
/// instance to <c>true</c> ("diagnose now"). Copying that posture here would re-enable the
/// [EnlistDiag] flood for every player whose MCM failed to load, which is precisely the volume
/// problem the toggle exists to remove — so an absent setting must resolve to OFF.
///
/// <see cref="EnlistmentDiagnosticsSettingsProvider.ResolveEnabled"/> is a pure seam that never
/// touches the MCM static, so the fail direction stays pinned regardless of whether
/// <c>TaomSettings.Instance</c> is reachable from the MSTest host.
/// </summary>
[TestClass]
public class EnlistmentDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void ResolveEnabled_NullSetting_ReturnsFalse()
    {
        // The whole guard: MCM absent / not yet initialized must mean OFF, never ON.
        Assert.IsFalse(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(null));
    }

    [TestMethod]
    public void ResolveEnabled_True_ReturnsTrue()
    {
        // Kills a vacuous hard-coded-false implementation: the seam must actually pass the
        // player's choice through, not just always answer "off".
        Assert.IsTrue(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(true));
    }

    [TestMethod]
    public void ResolveEnabled_False_ReturnsFalse()
    {
        Assert.IsFalse(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(false));
    }

    [TestMethod]
    public void IsEnabled_NoMcmInstance_DefaultsFalse()
    {
        // TaomSettings.Instance is null in the test host (MCM v5 isn't loaded), so the provider
        // falls back to the compiled default. Same shape as NameplateFadeSettingsProviderTests.
        var sut = new EnlistmentDiagnosticsSettingsProvider();

        Assert.IsFalse(sut.IsEnabled);
    }
}
