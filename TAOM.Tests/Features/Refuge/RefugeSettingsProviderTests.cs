using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.Refuge;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// The refuge settings read through the one MCM object the provider keeps (#745), still clamped: a bad or non-finite
/// value falls back to the compiled default rather than reaching founding, defence or militia maths, and a bound itself
/// passes (the in-game check sets the defence bonus to 0). Every getter is a row: its MCM property, a value inside the
/// slider range, one outside it, and its fallback, which must equal the compiled default. That the object is taken once
/// and kept is HotPathSettingsProvidersTests'.
/// </summary>
[TestClass]
public class RefugeSettingsProviderTests
{
    // getter, TaomSettings property, a value in range, a value out of range, the fallback (= the compiled default)
    [DataTestMethod]
    [DataRow("FoundCost", "RefugeFoundCost", 3000.0, 50001.0, 2000.0)]
    [DataRow("StrongholdUpgradeCost", "RefugeStrongholdUpgradeCost", 0.0, 100001.0, 5000.0)]
    [DataRow("BuildHours", "RefugeBuildHours", 48.0, 0.5, 6.0)]
    [DataRow("MaxRefugesCap", "RefugeMaxCap", 10.0, 0.0, 3.0)]
    [DataRow("ManageRange", "RefugeManageRange", 15.0, 15.5, 4.0)]
    [DataRow("MinTownDistance", "RefugeMinTownDistance", 0.0, 61.0, 16.0)]
    [DataRow("StrongholdMinTownDistance", "RefugeStrongholdMinTownDistance", 80.0, -1.0, 26.0)]
    [DataRow("RefugeDefenseBonus", "RefugeDefenseBonus", 0.0, 0.95, 0.2)]
    [DataRow("StrongholdDefenseBonus", "RefugeStrongholdDefenseBonus", 0.9, -0.1, 0.35)]
    [DataRow("MilitiaBase", "RefugeMilitiaBase", 50.0, 51.0, 6.0)]
    [DataRow("MilitiaMax", "RefugeMilitiaMax", 0.0, 101.0, 40.0)]
    [DataRow("RaidRange", "RefugeRaidRange", 1.0, 21.0, 6.0)]
    public void Getter_ReadsThroughAndClampsToTheCompiledDefault(string getter, string setting, double inRange, double outOfRange, double fallback)
    {
        var settings = new TaomSettings();
        var sut = new RefugeSettingsProvider(settings);

        Assert.AreEqual(fallback, Read(sut, getter), 1e-6, $"{getter}: the fallback must equal TaomSettings' compiled default");

        Write(settings, setting, inRange);
        Assert.AreEqual(inRange, Read(sut, getter), 1e-6, $"{getter}: a live MCM edit inside the range must read through");

        Write(settings, setting, outOfRange);
        Assert.AreEqual(fallback, Read(sut, getter), 1e-6, $"{getter}: an out-of-range value must fall back");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void DefenseBonuses_NonFinite_FallBackToTheDefault(float raw)
    {
        var sut = new RefugeSettingsProvider(new TaomSettings { RefugeDefenseBonus = raw, RefugeStrongholdDefenseBonus = raw });

        Assert.AreEqual(0.2f, sut.RefugeDefenseBonus);
        Assert.AreEqual(0.35f, sut.StrongholdDefenseBonus);
    }

    [TestMethod]
    public void Toggles_ReadThroughTheSettings()
    {
        var settings = new TaomSettings();
        var sut = new RefugeSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);
        Assert.IsFalse(sut.EnableRaids);

        settings.EnableRefuges = false;
        settings.RefugeEnableRaids = true;

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.EnableRaids);
    }

    private static double Read(RefugeSettingsProvider sut, string getter)
        => Convert.ToDouble(typeof(RefugeSettingsProvider).GetProperty(getter)!.GetValue(sut));

    private static void Write(TaomSettings settings, string property, double value)
    {
        var info = typeof(TaomSettings).GetProperty(property)!;
        info.SetValue(settings, Convert.ChangeType(value, info.PropertyType));
    }
}
