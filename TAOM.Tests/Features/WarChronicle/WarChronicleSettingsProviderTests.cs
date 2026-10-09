using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.WarChronicle;

namespace TAOM.Tests.Features.WarChronicle;

[TestClass]
public class WarChronicleSettingsProviderTests
{
    [TestMethod]
    public void WarEffectStrength_NoMcm_IsOne()
    {
        // TaomSettings.Instance is null in the test host: the compiled default holds.
        Assert.AreEqual(1f, new WarChronicleSettingsProvider().WarEffectStrength);
    }

    [TestMethod]
    public void WarEffectStrength_DefaultSetting_IsOne()
    {
        var sut = new WarChronicleSettingsProvider(new TaomSettings());

        Assert.AreEqual(1f, sut.WarEffectStrength);
    }

    [DataTestMethod]
    [DataRow(0f, 0f)]
    [DataRow(0.5f, 0.5f)]
    [DataRow(2f, 2f)]
    [DataRow(3.5f, 2f)]
    [DataRow(-1f, 0f)]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(float.NegativeInfinity, 1f)]
    public void WarEffectStrength_IsClampedAndNonFiniteFallsBackToOne(float stored, float expected)
    {
        var mcm = new TaomSettings { WarEffectStrength = stored };
        var sut = new WarChronicleSettingsProvider(mcm);

        Assert.AreEqual(expected, sut.WarEffectStrength);
    }

    [TestMethod]
    public void WarEffectStrength_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new WarChronicleSettingsProvider(mcm);
        Assert.AreEqual(1f, sut.WarEffectStrength);

        mcm.WarEffectStrength = 0.25f;

        Assert.AreEqual(0.25f, sut.WarEffectStrength);
    }

    [TestMethod]
    public void WarRallyEnabled_NoMcm_IsOn()
    {
        Assert.IsTrue(new WarChronicleSettingsProvider().WarRallyEnabled);
    }

    [TestMethod]
    public void WarRallyEnabled_DefaultSetting_IsOn()
    {
        Assert.IsTrue(new WarChronicleSettingsProvider(new TaomSettings()).WarRallyEnabled);
    }

    [TestMethod]
    public void WarRallyEnabled_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new WarChronicleSettingsProvider(mcm);
        Assert.IsTrue(sut.WarRallyEnabled);

        mcm.WarRallyEnabled = false;

        Assert.IsFalse(sut.WarRallyEnabled);
    }
}
