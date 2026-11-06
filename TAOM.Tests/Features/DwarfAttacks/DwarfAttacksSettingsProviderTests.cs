using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.DwarfAttacks;

// The dwarf attacks' two MCM settings (#1087): the switch and the damage slider, read live, with the slider clamped
// at the boundary because a hand-edited json2 can hold any integer (config-providers.md).

namespace TAOM.Tests.Features.DwarfAttacks;

[TestClass]
public class DwarfAttacksSettingsProviderTests
{
    [TestMethod]
    public void Defaults_NoSettingsObject_AreOnAndFullDamage()
    {
        var provider = new DwarfAttacksSettingsProvider(null);
        Assert.IsTrue(provider.Enabled);
        Assert.AreEqual(DwarfAttacksConfig.DefaultDamagePercent, provider.DamagePercent);
    }

    [TestMethod]
    public void Defaults_FreshSettingsObject_MatchTheProviderDefaults()
    {
        var provider = new DwarfAttacksSettingsProvider(new TaomSettings());
        Assert.IsTrue(provider.Enabled);
        Assert.AreEqual(DwarfAttacksConfig.DefaultDamagePercent, provider.DamagePercent);
    }

    [TestMethod]
    public void Enabled_SwitchedOff_IsFalse()
    {
        Assert.IsFalse(new DwarfAttacksSettingsProvider(new TaomSettings { DwarfAttacksEnabled = false }).Enabled);
    }

    [DataTestMethod]
    [DataRow(150, 150)]
    [DataRow(0, 0)]
    [DataRow(-20, 0)]
    [DataRow(DwarfAttacksConfig.MaxDamagePercent + 1, DwarfAttacksConfig.MaxDamagePercent)]
    public void DamagePercent_IsClampedToTheSliderRange(int stored, int expected)
    {
        var provider = new DwarfAttacksSettingsProvider(new TaomSettings { DwarfAttacksDamagePercent = stored });
        Assert.AreEqual(expected, provider.DamagePercent);
    }

    [TestMethod]
    public void SettingsGroup_HasItsOwnGroupOrder()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "Main", "Features", "TaomSettings.cs"));
        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"Battle Tactics/Dwarf Attacks\", GroupOrder = 84)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 84" }, StringSplitOptions.None).Length - 1,
            "GroupOrder 84 is used by another group");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        Assert.IsNotNull(dir, "repo root not found");
        return dir!.FullName;
    }
}
