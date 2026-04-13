using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

[TestClass]
public class ResourceTierTests
{
    [TestMethod]
    public void Constructor_SetsAllProperties()
    {
        // Arrange / Act
        var tier = new ResourceTier(level: 2, name: "Journeyman Smith", threshold: 250f, description: "Forges burn bright.");

        // Assert
        Assert.AreEqual(2, tier.Level);
        Assert.AreEqual("Journeyman Smith", tier.Name);
        Assert.AreEqual(250f, tier.Threshold);
        Assert.AreEqual("Forges burn bright.", tier.Description);
    }

    [TestMethod]
    public void Constructor_LevelOne_IsValid()
    {
        var tier = new ResourceTier(level: 1, name: "Apprentice Miner", threshold: 100f, description: "Desc");

        Assert.AreEqual(1, tier.Level);
        Assert.AreEqual(100f, tier.Threshold);
    }

    [TestMethod]
    public void Constructor_ZeroThreshold_IsValid()
    {
        var tier = new ResourceTier(level: 1, name: "Base", threshold: 0f, description: "");

        Assert.AreEqual(0f, tier.Threshold);
    }
}
