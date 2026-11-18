using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.EyeOfSauron;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8: the hunt's MCM values are validated at the provider. A non-finite float reverts to its default before
/// any range check (NaN passes every comparison), an out-of-range one is clamped to its slider, and ints are clamped.
/// </summary>
[TestClass]
public class EyeOfSauronSettingsProviderTests
{
    private static float ReadFloat(EyeOfSauronSettingsProvider sut, string name) =>
        (float)typeof(EyeOfSauronSettingsProvider).GetProperty(name)!.GetValue(sut)!;

    private static void Set(TaomSettings mcm, string name, object value) =>
        typeof(TaomSettings).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!.SetValue(mcm, value);

    // provider property, MCM property, default, min, max
    public static IEnumerable<object[]> Floats() => new[]
    {
        new object[] { nameof(IEyeOfSauronSettings.Threshold), nameof(TaomSettings.EyeThreshold), 10f, 1f, 100f },
        new object[] { nameof(IEyeOfSauronSettings.HalfLifeDays), nameof(TaomSettings.EyeHalfLifeDays), 30f, 1f, 365f },
        new object[] { nameof(IEyeOfSauronSettings.HunterSpeedMultiplier), nameof(TaomSettings.HunterSpeedMultiplier), 1.3f, 1.1f, 2.0f },
        new object[] { nameof(IEyeOfSauronSettings.HunterStrengthRatio), nameof(TaomSettings.HunterStrengthRatio), 1.3f, 0.5f, 3.0f },
        new object[] { nameof(IEyeOfSauronSettings.HunterStepCap), nameof(TaomSettings.HunterStepCap), 15f, 1f, 50f },
    };

    [DataTestMethod]
    [DynamicData(nameof(Floats), DynamicDataSourceType.Method)]
    public void FloatSetting_CompiledDefault_IsTheDesignDefault(string prop, string mcmProp, float def, float min, float max)
    {
        Assert.AreEqual(def, ReadFloat(new EyeOfSauronSettingsProvider(new TaomSettings(), NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>()), prop), 1e-5f);
    }

    [DataTestMethod]
    [DynamicData(nameof(Floats), DynamicDataSourceType.Method)]
    public void FloatSetting_NaN_RevertsToTheDefault(string prop, string mcmProp, float def, float min, float max)
    {
        var mcm = new TaomSettings();
        Set(mcm, mcmProp, float.NaN);

        Assert.AreEqual(def, ReadFloat(new EyeOfSauronSettingsProvider(mcm, NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>()), prop), 1e-5f);
    }

    [DataTestMethod]
    [DynamicData(nameof(Floats), DynamicDataSourceType.Method)]
    public void FloatSetting_Infinity_RevertsToTheDefault(string prop, string mcmProp, float def, float min, float max)
    {
        var mcm = new TaomSettings();
        Set(mcm, mcmProp, float.PositiveInfinity);
        Assert.AreEqual(def, ReadFloat(new EyeOfSauronSettingsProvider(mcm, NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>()), prop), 1e-5f);

        Set(mcm, mcmProp, float.NegativeInfinity);
        Assert.AreEqual(def, ReadFloat(new EyeOfSauronSettingsProvider(mcm, NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>()), prop), 1e-5f);
    }

    [DataTestMethod]
    [DynamicData(nameof(Floats), DynamicDataSourceType.Method)]
    public void FloatSetting_OutOfRange_ClampsToTheSlider(string prop, string mcmProp, float def, float min, float max)
    {
        var mcm = new TaomSettings();
        Set(mcm, mcmProp, min - 100f);
        Assert.AreEqual(min, ReadFloat(new EyeOfSauronSettingsProvider(mcm, NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>()), prop), 1e-5f);

        Set(mcm, mcmProp, max + 100f);
        Assert.AreEqual(max, ReadFloat(new EyeOfSauronSettingsProvider(mcm, NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>()), prop), 1e-5f);
    }

    [TestMethod]
    public void IntSettings_DefaultsAndClamps()
    {
        var mcm = new TaomSettings();
        var sut = new EyeOfSauronSettingsProvider(mcm, NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>());
        Assert.AreEqual(60, sut.CooldownDays);
        Assert.AreEqual(20, sut.HuntTimeoutDays);
        Assert.AreEqual(250, sut.HunterMaxTroops);
        Assert.IsTrue(sut.Enabled);

        mcm.EyeCooldownDays = -5;
        mcm.HuntTimeoutDays = 0;
        mcm.HunterMaxTroops = 5;
        Assert.AreEqual(0, sut.CooldownDays);
        Assert.AreEqual(1, sut.HuntTimeoutDays);
        Assert.AreEqual(20, sut.HunterMaxTroops);

        mcm.EyeCooldownDays = 9999;
        mcm.HuntTimeoutDays = 9999;
        mcm.HunterMaxTroops = 99999;
        Assert.AreEqual(365, sut.CooldownDays);
        Assert.AreEqual(120, sut.HuntTimeoutDays);
        Assert.AreEqual(1000, sut.HunterMaxTroops);
    }

    [TestMethod]
    public void ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new EyeOfSauronSettingsProvider(mcm, NSubstitute.Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>());
        Assert.AreEqual(1.3f, sut.HunterSpeedMultiplier, 1e-5f);

        mcm.EyeOfSauronEnabled = false;
        mcm.HunterSpeedMultiplier = 1.8f;

        Assert.IsFalse(sut.Enabled);
        Assert.AreEqual(1.8f, sut.HunterSpeedMultiplier, 1e-5f);
    }
}
