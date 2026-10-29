using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.StagedSiege;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// The MCM switch has no value to validate (a bool), so the rules are about wiring: the provider reads the live setting, the
/// fallback used before MCM has built its settings is the compiled default (off), the property sits in its own Battle Tactics
/// group at a free order with a plain-English label and hint that says when it takes effect, and the default is never
/// flipped, only renamed (csharp-architecture.md; the ShaderPrecompilation trap).
/// </summary>
[TestClass]
public class StagedSiegeSettingsProviderTests
{
    private const int GroupOrder = 81;

    [TestMethod]
    public void From_TheSettingOn_IsEnabled()
    {
        Assert.IsTrue(StagedSiegeSettingsProvider.From(new TaomSettings { EnableStagedSiege = true }));
    }

    [TestMethod]
    public void From_TheSettingOff_IsDisabled()
    {
        Assert.IsFalse(StagedSiegeSettingsProvider.From(new TaomSettings { EnableStagedSiege = false }));
    }

    [TestMethod]
    public void From_NoSettingsYet_FallsBackToOff()
    {
        Assert.IsFalse(StagedSiegeSettingsProvider.From(null));
    }

    [TestMethod]
    public void CompiledDefault_IsOff()
    {
        Assert.IsFalse(new TaomSettings().EnableStagedSiege);
        Assert.AreEqual(StagedSiegeSettingsProvider.From(null), new TaomSettings().EnableStagedSiege);
    }

    [TestMethod]
    public void IsEnabled_WithoutMcm_IsOff()
    {
        // The instance property reads TaomSettings.Instance, which is null in a test host.
        Assert.IsFalse(new StagedSiegeSettingsProvider().IsEnabled);
    }

    [TestMethod]
    public void TheProvider_ReadsOnlyItsOwnSetting()
    {
        // Flip every OTHER bool of TaomSettings one at a time: the provider's answer must not move.
        foreach (var property in typeof(TaomSettings).GetProperties()
                     .Where(p => p.PropertyType == typeof(bool) && p.CanWrite && p.Name != nameof(TaomSettings.EnableStagedSiege)))
        {
            var settings = new TaomSettings();
            property.SetValue(settings, !(bool)property.GetValue(settings)!);

            Assert.IsFalse(StagedSiegeSettingsProvider.From(settings), $"{property.Name} moved the staged siege switch");
        }
    }

    // --- the MCM property -------------------------------------------------------------------------------------------------

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsOwnBattleTacticsGroup_AppliesWithoutARestart()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableStagedSiege))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Battle Tactics/Staged Siege", Read(group, "GroupName"));
        Assert.AreEqual(GroupOrder, (int)Read(group, "GroupOrder"),
            "MCM creates a group from its first property and ignores a GroupOrder on any later one");

        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the switch is read once per battle: a restart is never needed");
    }

    [TestMethod]
    public void TheLabelAndHint_ArePlainEnglish_AndSayWhenItTakesEffect()
    {
        var value = Attribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableStagedSiege))!, "SettingPropertyBoolAttribute");
        var hint = (string)Read(value, "HintText");

        StringAssert.Contains(hint, "points A to D");
        StringAssert.Contains(hint, "Reinforcement Wave Count");
        StringAssert.Contains(hint, "next battle");
        StringAssert.Contains(hint, "Default: off");
        StringAssert.StartsWith((string)Read(value, "DisplayName"), "{=taom_staged_siege_mcm_enable}");
        StringAssert.StartsWith(hint, "{=taom_staged_siege_mcm_enable_hint}");
    }

    [TestMethod]
    public void TheGroupOrder_IsFreeAmongAllGroups()
    {
        var clashes = typeof(TaomSettings).GetProperties()
            .Where(p => p.Name != nameof(TaomSettings.EnableStagedSiege))
            .Select(p => p.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == "SettingPropertyGroupAttribute"))
            .Where(a => a != null)
            .Select(a => (Name: (string)Read(a!, "GroupName"), Order: (int)Read(a!, "GroupOrder")))
            .Where(g => g.Order == GroupOrder)
            .ToList();

        Assert.AreEqual(0, clashes.Count,
            "another group already uses GroupOrder " + GroupOrder + ": " + string.Join(", ", clashes.Select(g => g.Name)));
    }
}
