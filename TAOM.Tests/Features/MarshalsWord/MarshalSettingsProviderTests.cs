using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.MarshalsWord;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The six MCM values (build-spec section 4): their compiled defaults and that the provider reads through to the settings object.</summary>
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
        Assert.IsFalse(MarshalSettingsProvider.DefaultOutlines);
        Assert.IsTrue(MarshalSettingsProvider.DefaultHollowSquare);
        Assert.IsTrue(MarshalSettingsProvider.DefaultSpacingRepair);
        Assert.IsTrue(MarshalSettingsProvider.DefaultThrowersFirst);
    }

    [TestMethod]
    public void TheMcmProperties_StartAtTheCompiledDefaults()
    {
        var settings = new TaomSettings();

        Assert.AreEqual(MarshalSettingsProvider.DefaultEnabled, settings.MarshalEnabled);
        Assert.AreEqual(MarshalSettingsProvider.DefaultKeepArray, settings.MarshalKeepArray);
        Assert.AreEqual(MarshalSettingsProvider.DefaultArriveTogether, settings.MarshalArriveTogether);
        Assert.AreEqual(MarshalSettingsProvider.DefaultOutlines, settings.MarshalShowOutlines);
        Assert.AreEqual(MarshalSettingsProvider.DefaultHollowSquare, settings.MarshalHollowSquare);
        Assert.AreEqual(MarshalSettingsProvider.DefaultSpacingRepair, settings.MarshalSpacingRepair);
        Assert.AreEqual(MarshalSettingsProvider.DefaultThrowersFirst, settings.MarshalThrowersFirst);
    }

    [TestMethod]
    public void EachValue_ReadsThroughToItsOwnProperty()
    {
        var settings = new TaomSettings { MarshalEnabled = false, MarshalKeepArray = true, MarshalArriveTogether = false, MarshalShowOutlines = false, MarshalHollowSquare = false, MarshalSpacingRepair = false, MarshalThrowersFirst = false };
        var sut = new MarshalSettingsProvider(settings);

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.KeepArray);
        Assert.IsFalse(sut.ArriveTogether);
        Assert.IsFalse(sut.Outlines);
        Assert.IsFalse(sut.HollowSquare);
        Assert.IsFalse(sut.SpacingRepair);
        Assert.IsFalse(sut.ThrowersFirst);

        settings.MarshalKeepArray = false;
        settings.MarshalThrowersFirst = true;
        Assert.IsTrue(sut.ThrowersFirst, "the provider keeps the settings object, not a copy of a value");
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
        Assert.AreEqual(MarshalSettingsProvider.DefaultOutlines, sut.Outlines);
        Assert.AreEqual(MarshalSettingsProvider.DefaultHollowSquare, sut.HollowSquare);
        Assert.AreEqual(MarshalSettingsProvider.DefaultSpacingRepair, sut.SpacingRepair);
        Assert.AreEqual(MarshalSettingsProvider.DefaultThrowersFirst, sut.ThrowersFirst);
    }
}
