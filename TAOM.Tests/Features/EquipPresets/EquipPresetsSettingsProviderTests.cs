using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EquipPresets;

namespace TAOM.Tests.Features.EquipPresets;

[TestClass]
public class EquipPresetsSettingsProviderTests
{
    // Note: tests that instantiate EquipPresetsSettingsProvider trigger MCMv5 assembly load,
    // which fails in the test runtime (same env issue affecting FiefManagementSettingsProviderTests).
    // We assert the public surface via the static constants here; runtime-fallback behavior is
    // covered indirectly via service tests that mock IEquipPresetsSettingsProvider.

    [TestMethod]
    public void Constants_RangeIsCoherent()
    {
        Assert.AreEqual(1, EquipPresetsSettingsProvider.MinMaxPresets);
        Assert.AreEqual(20, EquipPresetsSettingsProvider.MaxMaxPresets);
        Assert.AreEqual(10, EquipPresetsSettingsProvider.DefaultMaxPresets);
    }

    [TestMethod]
    public void DefaultMaxPresets_IsWithinRange()
    {
        Assert.IsTrue(EquipPresetsSettingsProvider.DefaultMaxPresets >= EquipPresetsSettingsProvider.MinMaxPresets);
        Assert.IsTrue(EquipPresetsSettingsProvider.DefaultMaxPresets <= EquipPresetsSettingsProvider.MaxMaxPresets);
    }
}
