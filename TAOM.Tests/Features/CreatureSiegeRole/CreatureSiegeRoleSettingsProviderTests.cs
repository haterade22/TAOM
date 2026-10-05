using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CreatureSiegeRole;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The MCM switch has no value to validate (a bool), so the rules are about wiring: the provider reads the live setting, the
/// fallback used before MCM has built its settings is the compiled default (on), the property sits in its own Battle Tactics
/// group at a free order with a plain-English label and hint that says when it takes effect, and the default is never
/// flipped, only renamed (csharp-architecture.md; the ShaderPrecompilation trap).
/// </summary>
[TestClass]
public class CreatureSiegeRoleSettingsProviderTests
{
    [TestMethod]
    public void From_TheSettingOn_IsEnabled()
    {
        Assert.IsTrue(CreatureSiegeRoleSettingsProvider.From(new TaomSettings { EnableCreatureSiegeRole = true }));
    }

    [TestMethod]
    public void From_TheSettingOff_IsDisabled()
    {
        Assert.IsFalse(CreatureSiegeRoleSettingsProvider.From(new TaomSettings { EnableCreatureSiegeRole = false }));
    }

    [TestMethod]
    public void From_NoSettingsYet_FallsBackToOn()
    {
        // MCM's Instance is null until its own provider is up; the role must not be silently off for that window.
        Assert.IsTrue(CreatureSiegeRoleSettingsProvider.From(null));
    }

    [TestMethod]
    public void TheCompiledDefault_IsOn_AndMatchesTheFallback()
    {
        Assert.AreEqual(CreatureSiegeRoleSettingsProvider.From(null), new TaomSettings().EnableCreatureSiegeRole);
        Assert.IsTrue(new TaomSettings().EnableCreatureSiegeRole);
    }

    [TestMethod]
    public void IsEnabled_WithoutMcm_IsOn()
    {
        // The instance property reads TaomSettings.Instance, which is null in a test host.
        Assert.IsTrue(new CreatureSiegeRoleSettingsProvider().IsEnabled);
    }

    [TestMethod]
    public void TheProvider_ReadsOnlyItsOwnSetting()
    {
        // Flip every OTHER bool of TaomSettings off one at a time: the provider's answer must not move.
        foreach (var property in typeof(TaomSettings).GetProperties()
                     .Where(p => p.PropertyType == typeof(bool) && p.CanWrite && p.Name != nameof(TaomSettings.EnableCreatureSiegeRole)))
        {
            var settings = new TaomSettings();
            property.SetValue(settings, !(bool)property.GetValue(settings)!);

            Assert.IsTrue(CreatureSiegeRoleSettingsProvider.From(settings), $"{property.Name} moved the creature siege role switch");
        }
    }

    // --- the MCM property -------------------------------------------------------------------------------------------------

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsOwnBattleTacticsGroup_AtOrder26_AppliesWithoutARestart()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableCreatureSiegeRole))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Battle Tactics/Creature Siege Role", Read(group, "GroupName"));
        Assert.AreEqual(26, (int)Read(group, "GroupOrder"),
            "MCM creates a group from its first property and ignores a GroupOrder on any later one");

        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the role is read once per battle: a restart is never needed");
    }

    [TestMethod]
    public void TheLabelAndHint_ArePlainEnglish_AndSayWhenItTakesEffect()
    {
        // MCM labels and hints in this file are not localisation keys (0 matches for "{=" in TaomSettings.cs).
        var value = Attribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableCreatureSiegeRole))!, "SettingPropertyBoolAttribute");
        var label = (string)Read(value, "DisplayName");
        var hint = (string)Read(value, "HintText");

        Assert.IsFalse(label.Contains("{=") || hint.Contains("{="));
        StringAssert.Contains(hint, "ladders");
        StringAssert.Contains(hint, "towers");
        StringAssert.Contains(hint, "siege engines");
        StringAssert.Contains(hint, "gate");
        StringAssert.Contains(hint, "walls");
        StringAssert.Contains(hint, "next battle");
        StringAssert.Contains(hint, "Default: on");
    }

    [TestMethod]
    public void TheGroupOrder_IsFreeAmongTheOtherBattleTacticsGroups()
    {
        var orders = typeof(TaomSettings).GetProperties()
            .Where(p => p.Name != nameof(TaomSettings.EnableCreatureSiegeRole))
            .Select(p => p.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == "SettingPropertyGroupAttribute"))
            .Where(a => a != null)
            .Select(a => (Name: (string)Read(a!, "GroupName"), Order: (int)Read(a!, "GroupOrder")))
            .Where(g => g.Name.StartsWith("Battle Tactics/", StringComparison.Ordinal) && g.Order != 0)
            .ToList();

        Assert.IsTrue(orders.Count >= 10, "expected the other Battle Tactics groups, found " + orders.Count);
        CollectionAssert.DoesNotContain(orders.Select(g => g.Order).ToList(), 26,
            "another Battle Tactics group already uses GroupOrder 26: " + string.Join(", ", orders.Where(g => g.Order == 26).Select(g => g.Name)));
    }
}
