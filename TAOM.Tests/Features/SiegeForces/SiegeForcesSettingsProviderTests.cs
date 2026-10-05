using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.SiegeForces;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The picker's MCM switch has no value to validate (a bool), so the rules here are about wiring: the provider reads
/// the live setting, and the fallback used before MCM has built its settings is the compiled default (on), so a
/// player who has never opened Mod Options still gets the picker. MCM persists per property: the default is never
/// flipped, only renamed (csharp-architecture.md; the ShaderPrecompilation trap).
/// </summary>
[TestClass]
public class SiegeForcesSettingsProviderTests
{
    [TestMethod]
    public void From_TheSettingOn_IsEnabled()
    {
        Assert.IsTrue(SiegeForcesSettingsProvider.From(new TaomSettings { EnableSiegeTroopPicker = true }));
    }

    [TestMethod]
    public void From_TheSettingOff_IsDisabled()
    {
        Assert.IsFalse(SiegeForcesSettingsProvider.From(new TaomSettings { EnableSiegeTroopPicker = false }));
    }

    [TestMethod]
    public void From_NoSettingsYet_FallsBackToOn()
    {
        // MCM's Instance is null until its provider is up; the picker must not be silently off for that window.
        Assert.IsTrue(SiegeForcesSettingsProvider.From(null));
    }

    [TestMethod]
    public void TheCompiledDefault_IsOn_AndMatchesTheFallback()
    {
        Assert.AreEqual(SiegeForcesSettingsProvider.From(null), new TaomSettings().EnableSiegeTroopPicker);
        Assert.IsTrue(new TaomSettings().EnableSiegeTroopPicker);
    }

    [TestMethod]
    public void PickerEnabled_WithoutMcm_IsOn()
    {
        // The instance property reads TaomSettings.Instance, which is null in a test host.
        Assert.IsTrue(new SiegeForcesSettingsProvider().PickerEnabled);
    }
}
