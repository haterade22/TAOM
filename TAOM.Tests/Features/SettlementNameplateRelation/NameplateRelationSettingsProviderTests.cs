using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

[TestClass]
public class NameplateRelationSettingsProviderTests
{
    // TaomSettings.Instance is null in test environments (MCM v5 is not loaded), so the provider
    // falls back to its compiled defaults. These pin the fallbacks to the MCM defaults declared in
    // TaomSettings.cs; drift would silently change the plates before MCM finishes initialising.

    [TestMethod]
    public void ColorsEnabled_NoMcmInstance_DefaultsToTrue()
        => Assert.IsTrue(new NameplateRelationSettingsProvider().ColorsEnabled);

    [TestMethod]
    public void TintStrength_NoMcmInstance_DefaultsToOne()
        => Assert.AreEqual(1f, new NameplateRelationSettingsProvider().TintStrength);

    [TestMethod]
    public void NeutralPlateAlpha_NoMcmInstance_DefaultsToVanillaNeutral()
        => Assert.AreEqual(0.35f, new NameplateRelationSettingsProvider().NeutralPlateAlpha, 0.0001f);

    [TestMethod]
    public void RelationPlateAlpha_NoMcmInstance_DefaultsToVanillaOwnFaction()
        => Assert.AreEqual(0.5f, new NameplateRelationSettingsProvider().RelationPlateAlpha, 0.0001f);

    [TestMethod]
    public void NormalizePercent_InRange_ReturnsFraction()
    {
        Assert.AreEqual(0.35f, NameplateRelationSettingsProvider.NormalizePercent(35f, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(1f, NameplateRelationSettingsProvider.NormalizePercent(100f, 50f, 0f, 100f), 0.0001f);
        Assert.AreEqual(0f, NameplateRelationSettingsProvider.NormalizePercent(0f, 50f, 0f, 100f), 0.0001f);
    }

    [TestMethod]
    public void NormalizePercent_NonFinite_ReturnsDefaultFraction()
    {
        // NaN fails every range compare, so it must be rejected before the range check.
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(float.NaN, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(float.PositiveInfinity, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(float.NegativeInfinity, 50f, 10f, 100f), 0.0001f);
    }

    [TestMethod]
    public void NormalizePercent_OutOfRange_ReturnsDefaultFraction()
    {
        // A hand-edited TAOM.json can hold anything; below the slider floor or above 100 revert.
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(5f, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(150f, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(-1f, 50f, 0f, 100f), 0.0001f);
    }
}
