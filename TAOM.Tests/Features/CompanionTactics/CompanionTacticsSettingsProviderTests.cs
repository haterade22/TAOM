using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CompanionTactics;

namespace TAOM.Tests.Features.CompanionTactics;

/// <summary>
/// Read every frame by the battle action bar and the order-of-battle overlay, and per formation order
/// by Patch35, so the provider caches the MCM reference and reads through it. Every getter name equals
/// its <c>TaomSettings</c> property name. <c>TaomSettings.Instance</c> is null in tests, so the public
/// constructor pins the no-MCM fallbacks.
/// </summary>
[TestClass]
public class CompanionTacticsSettingsProviderTests
{
    [TestMethod]
    public void Getters_NoMcm_ReturnTheDocumentedDefaults()
    {
        var sut = new CompanionTacticsSettingsProvider();

        Assert.IsTrue(sut.EnableCompanionRoleTooltips, nameof(sut.EnableCompanionRoleTooltips));
        Assert.IsTrue(sut.EnableOOBRoleDisplay, nameof(sut.EnableOOBRoleDisplay));
        Assert.IsFalse(sut.CompanionRolesDebug, nameof(sut.CompanionRolesDebug));
        Assert.IsFalse(sut.EnableFormationPresets, nameof(sut.EnableFormationPresets));
        Assert.AreEqual(10, sut.MaxFormationPresets, nameof(sut.MaxFormationPresets));
        Assert.IsFalse(sut.FormationPresetsDebug, nameof(sut.FormationPresetsDebug));
        Assert.IsTrue(sut.EnableBattleActionBar, nameof(sut.EnableBattleActionBar));
        Assert.IsTrue(sut.CancelStanceOnMove, nameof(sut.CancelStanceOnMove));
        Assert.IsTrue(sut.EnableVolleyFire, nameof(sut.EnableVolleyFire));
        Assert.IsFalse(sut.BattleActionBarDebug, nameof(sut.BattleActionBarDebug));
    }

    // The fallbacks agree with the MCM compiled defaults, or the feature behaves one way before
    // TAOM.json is first written and another way after.
    [TestMethod]
    public void EveryFallback_EqualsTheMcmCompiledDefault()
    {
        var provider = new CompanionTacticsSettingsProvider();
        var mcm = new TaomSettings();
        foreach (var p in typeof(ICompanionTacticsSettingsProvider).GetProperties())
            Assert.AreEqual(typeof(TaomSettings).GetProperty(p.Name).GetValue(mcm), p.GetValue(provider), p.Name);
    }

    // Read THROUGH the cached reference. One setting is edited per pass on a fresh TaomSettings, so a
    // getter wired to another setting fails the pass that edits either one, and a getter that caches
    // its first read fails the pass that edits its own setting.
    [TestMethod]
    public void Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var props = typeof(ICompanionTacticsSettingsProvider).GetProperties();
        foreach (var edited in props)
        {
            var mcm = new TaomSettings();
            var provider = new CompanionTacticsSettingsProvider(mcm);
            foreach (var p in props)
                Assert.AreEqual(typeof(TaomSettings).GetProperty(p.Name).GetValue(mcm), p.GetValue(provider), p.Name + " before any edit");

            var setting = typeof(TaomSettings).GetProperty(edited.Name);
            setting.SetValue(mcm, setting.PropertyType == typeof(bool)
                ? !(bool)setting.GetValue(mcm)
                : (object)((int)setting.GetValue(mcm) + 1));

            foreach (var p in props)
                Assert.AreEqual(typeof(TaomSettings).GetProperty(p.Name).GetValue(mcm), p.GetValue(provider), p.Name + " after editing " + edited.Name);
        }
    }
}
