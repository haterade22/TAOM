using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleGearLoot;

namespace TAOM.Tests.Features.BattleGearLoot;

[TestClass]
public class BattleGearLootSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsOffAndTen()
    {
        Assert.AreEqual(new BattleGearLootSettings(false, 10), BattleGearLootSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        Assert.AreEqual(BattleGearLootSettingsProvider.From(null), BattleGearLootSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_ValuesInRange_PassThrough()
    {
        var s = BattleGearLootSettingsProvider.From(new TaomSettings { EnableBattleGearLoot = true, BattleGearLootChancePercent = 40 });
        Assert.AreEqual(new BattleGearLootSettings(true, 40), s);
    }

    [DataTestMethod]
    [DataRow(-5, 0)]
    [DataRow(0, 0)]
    [DataRow(100, 100)]
    [DataRow(150, 100)]
    public void From_Chance_IsClampedToZeroToHundred(int value, int expected)
    {
        Assert.AreEqual(expected, BattleGearLootSettingsProvider.From(new TaomSettings { BattleGearLootChancePercent = value }).ChancePercent);
    }

    [TestMethod]
    public void McmInitializers_EqualTheProviderConstants()
    {
        var s = new TaomSettings();
        Assert.AreEqual(BattleGearLootSettingsProvider.DefaultEnabled, s.EnableBattleGearLoot);
        Assert.AreEqual(BattleGearLootSettingsProvider.DefaultChancePercent, s.BattleGearLootChancePercent);
    }

    [TestMethod]
    public void Slider_BoundsEqualTheProviderMinAndMax()
    {
        var prop = typeof(TaomSettings).GetProperty(nameof(TaomSettings.BattleGearLootChancePercent));
        Assert.IsNotNull(prop);
        var attr = prop.GetCustomAttributes(false).Single(a => a.GetType().Name == "SettingPropertyIntegerAttribute");
        var type = attr.GetType();
        Assert.AreEqual((decimal)BattleGearLootSettingsProvider.MinChancePercent, System.Convert.ToDecimal(type.GetProperty("MinValue")!.GetValue(attr)));
        Assert.AreEqual((decimal)BattleGearLootSettingsProvider.MaxChancePercent, System.Convert.ToDecimal(type.GetProperty("MaxValue")!.GetValue(attr)));
    }
}
