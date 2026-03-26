using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AtmospherePersistence;

namespace TAOM.Tests.Features.AtmospherePersistence;

[TestClass]
public class AtmosphereOverrideServiceTests
{
    [TestMethod]
    public void RequiresAtmosphereOverride_SceneContainsForceatmo_ReturnsTrue()
    {
        var result = AtmosphereOverrideService.RequiresAtmosphereOverride("some_forceatmo_scene");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void RequiresAtmosphereOverride_SceneWithoutMarker_ReturnsFalse()
    {
        var result = AtmosphereOverrideService.RequiresAtmosphereOverride("battania_village_a");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RequiresAtmosphereOverride_NullSceneName_ReturnsFalse()
    {
        var result = AtmosphereOverrideService.RequiresAtmosphereOverride(null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RequiresAtmosphereOverride_EmptySceneName_ReturnsFalse()
    {
        var result = AtmosphereOverrideService.RequiresAtmosphereOverride("");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RequiresAtmosphereOverride_MarkerIsCaseInsensitive_ReturnsTrue()
    {
        var result = AtmosphereOverrideService.RequiresAtmosphereOverride("scene_FORCEATMO_day");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void RequiresAtmosphereOverride_MarkerAtStart_ReturnsTrue()
    {
        var result = AtmosphereOverrideService.RequiresAtmosphereOverride("forceatmo_moria");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void RequiresAtmosphereOverride_MarkerAtEnd_ReturnsTrue()
    {
        var result = AtmosphereOverrideService.RequiresAtmosphereOverride("moria_forceatmo");

        Assert.IsTrue(result);
    }
}
