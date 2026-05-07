using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.QuickActions.Models;

namespace TAOM.Tests.Features.QuickActions;

[TestClass]
public class DamagedQualityPresetTests
{
    [TestMethod]
    public void ToThreshold_Pristine_ReturnsZero()
    {
        Assert.AreEqual(0f, DamagedQualityPreset.Pristine.ToThreshold());
    }

    [TestMethod]
    public void ToThreshold_Slight_ReturnsNegativePoint10()
    {
        Assert.AreEqual(-0.10f, DamagedQualityPreset.Slight.ToThreshold());
    }

    [TestMethod]
    public void ToThreshold_Moderate_ReturnsNegativePoint20()
    {
        Assert.AreEqual(-0.20f, DamagedQualityPreset.Moderate.ToThreshold());
    }

    [TestMethod]
    public void ToThreshold_Heavy_ReturnsNegativePoint40()
    {
        Assert.AreEqual(-0.40f, DamagedQualityPreset.Heavy.ToThreshold());
    }

    [TestMethod]
    public void FromDropdownIndex_0_Pristine()
    {
        Assert.AreEqual(DamagedQualityPreset.Pristine, DamagedQualityPresetExtensions.FromDropdownIndex(0));
    }

    [TestMethod]
    public void FromDropdownIndex_1_Slight()
    {
        Assert.AreEqual(DamagedQualityPreset.Slight, DamagedQualityPresetExtensions.FromDropdownIndex(1));
    }

    [TestMethod]
    public void FromDropdownIndex_2_Moderate()
    {
        Assert.AreEqual(DamagedQualityPreset.Moderate, DamagedQualityPresetExtensions.FromDropdownIndex(2));
    }

    [TestMethod]
    public void FromDropdownIndex_3_Heavy()
    {
        Assert.AreEqual(DamagedQualityPreset.Heavy, DamagedQualityPresetExtensions.FromDropdownIndex(3));
    }

    [TestMethod]
    public void FromDropdownIndex_OutOfRange_FallsBackToModerate()
    {
        Assert.AreEqual(DamagedQualityPreset.Moderate, DamagedQualityPresetExtensions.FromDropdownIndex(99));
        Assert.AreEqual(DamagedQualityPreset.Moderate, DamagedQualityPresetExtensions.FromDropdownIndex(-5));
    }
}
