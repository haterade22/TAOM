using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Fail-direction pin for the enlistment trace toggle (#840). The trace ships OFF, so a missing MCM
/// instance resolves to OFF too: MCM-absent behaviour must equal MCM-present-at-default behaviour.
/// The compiled default and the <c>??</c> fallback flip together;
/// <see cref="CompiledDefault_AndProviderFallback_Agree"/> makes changing only one of them fail, and it
/// is symmetric, so it cannot tell you which posture is current. Two tests name the posture:
/// <see cref="ResolveEnabled_NullSetting_ReturnsFalse"/> and <see cref="IsEnabled_NoMcmInstance_DefaultsFalse"/>.
/// </summary>
[TestClass]
public class EnlistmentDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void ResolveEnabled_NullSetting_ReturnsFalse()
    {
        // Missing settings resolve to off: the routine trace is for a bug report, not for playing.
        Assert.IsFalse(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(null));
    }

    [TestMethod]
    public void ResolveEnabled_True_ReturnsTrue()
    {
        // Kills a vacuous constant implementation: the seam must pass the player's choice through.
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

    [TestMethod]
    public void CompiledDefault_AndProviderFallback_Agree()
    {
        // The MCM default and the null-instance fallback are two independent literals that encode
        // the SAME decision. If they drift, a player with MCM loaded and a player without it get
        // different behaviour at the shipped default.
        Assert.AreEqual(
            new TaomSettings().EnableEnlistmentTrace,
            EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(null),
            "TaomSettings.EnableEnlistmentTrace' compiled default and ResolveEnabled's `??` fallback must encode the " +
            "same posture. Flip both or neither.");
    }

    [TestMethod]
    public void EnableEnlistmentDiagnostics_Property_IsGone()
    {
        Assert.IsNull(
            typeof(TaomSettings).GetProperty("EnableEnlistmentDiagnostics"),
            "A saved value of the old name (true for players who confirmed a settings change while it was on, " +
            "v2.0.24 to v2.0.28 and the public 1.4.8 build) is what the rename exists to leave behind; do not bring the name back.");
    }
}
