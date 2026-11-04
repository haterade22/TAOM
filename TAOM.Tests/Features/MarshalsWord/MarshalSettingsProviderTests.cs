using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.MarshalsWord;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The three MCM values of stages M1 and M2 (build-spec section 4): their compiled defaults and that the provider reads through to the settings object.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalSettingsProviderTests
{
    [TestMethod]
    public void Defaults_AreAllOn()
    {
        Assert.IsTrue(MarshalSettingsProvider.DefaultEnabled);
        Assert.IsTrue(MarshalSettingsProvider.DefaultKeepArray);
        Assert.IsTrue(MarshalSettingsProvider.DefaultArriveTogether);
    }

    [TestMethod]
    public void TheMcmProperties_StartAtTheCompiledDefaults()
    {
        var settings = new TaomSettings();

        Assert.AreEqual(MarshalSettingsProvider.DefaultEnabled, settings.MarshalEnabled);
        Assert.AreEqual(MarshalSettingsProvider.DefaultKeepArray, settings.MarshalKeepArray);
        Assert.AreEqual(MarshalSettingsProvider.DefaultArriveTogether, settings.MarshalArriveTogether);
    }

    [TestMethod]
    public void EachValue_ReadsThroughToItsOwnProperty()
    {
        var settings = new TaomSettings { MarshalEnabled = false, MarshalKeepArray = true, MarshalArriveTogether = false };
        var sut = new MarshalSettingsProvider(settings);

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.KeepArray);
        Assert.IsFalse(sut.ArriveTogether);

        settings.MarshalKeepArray = false;
        Assert.IsFalse(sut.KeepArray, "the provider keeps the settings object, not a copy of a value");
    }

    [TestMethod]
    public void WithNoMcmInstance_EachValueFallsBackToItsDefault()
    {
        var sut = new MarshalSettingsProvider();

        // TaomSettings.Instance is null in a unit-test process (no MCM), so the accessor returns the compiled defaults.
        Assert.AreEqual(MarshalSettingsProvider.DefaultEnabled, sut.Enabled);
        Assert.AreEqual(MarshalSettingsProvider.DefaultKeepArray, sut.KeepArray);
        Assert.AreEqual(MarshalSettingsProvider.DefaultArriveTogether, sut.ArriveTogether);
    }
}
