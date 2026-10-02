using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.FactionUI;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. The thirteen front-end toggles are thirteen booleans of one type, so a swapped pair in
/// the MCM-to-settings mapping compiles and silently crosses two toggles. Each MCM property
/// <c>FrontEndX</c> must reach the setting <c>X</c> and nothing else, and the fallback used before MCM
/// has built its settings must match the compiled MCM defaults.
/// </summary>
[TestClass]
public class FactionUISettingsProviderTests
{
    private const string McmPrefix = "FrontEnd";

    private static PropertyInfo[] McmToggles() =>
        typeof(TaomSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.StartsWith(McmPrefix) && p.PropertyType == typeof(bool))
            .ToArray();

    private static PropertyInfo[] SettingToggles() =>
        typeof(FactionUISettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool))
            .ToArray();

    [TestMethod]
    public void McmToggles_EachNamesOneSetting_AndEverySettingHasOne()
    {
        var mcm = McmToggles().Select(p => p.Name.Substring(McmPrefix.Length)).OrderBy(n => n).ToArray();
        var settings = SettingToggles().Select(p => p.Name).OrderBy(n => n).ToArray();

        CollectionAssert.AreEqual(settings, mcm);
    }

    [TestMethod]
    public void From_FlippingOneMcmToggle_FlipsOnlyItsOwnSetting()
    {
        var settingToggles = SettingToggles();
        var baseline = FactionUISettingsProvider.From(new TaomSettings());

        foreach (var toggle in McmToggles())
        {
            var mcm = new TaomSettings();
            toggle.SetValue(mcm, !(bool)toggle.GetValue(mcm));

            var mapped = FactionUISettingsProvider.From(mcm);

            foreach (var setting in settingToggles)
            {
                var flipped = (bool)setting.GetValue(mapped) != (bool)setting.GetValue(baseline);
                var own = setting.Name == toggle.Name.Substring(McmPrefix.Length);
                Assert.AreEqual(own, flipped, $"flipping {toggle.Name} {(own ? "did not reach" : "also changed")} {setting.Name}");
            }
        }
    }

    [TestMethod]
    public void WithoutMcm_TheFallback_MatchesTheCompiledMcmDefaults()
    {
        var compiled = FactionUISettingsProvider.From(new TaomSettings());

        foreach (var setting in SettingToggles())
            Assert.AreEqual(setting.GetValue(compiled), setting.GetValue(FactionUISettings.Default), setting.Name);
    }
}
