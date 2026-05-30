using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BanditManagement;

namespace TAOM.Tests.Features.BanditManagement;

[TestClass]
public class HideoutDescriptionServiceTests
{
    private HideoutDescriptionService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new HideoutDescriptionService();

    [TestMethod]
    public void GetDescription_DunlandRaiders_ReturnsDunlandTemplate()
    {
        var result = _sut.GetDescription("dunland_raiders");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "{=taom_hideout_desc_dunland}");
    }

    [TestMethod]
    public void GetDescription_GundabadRaiders_ReturnsGundabadTemplate()
    {
        var result = _sut.GetDescription("gundabad_raiders");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "{=taom_hideout_desc_gundabad}");
    }

    [TestMethod]
    public void GetDescription_HaradRaiders_ReturnsHaradTemplate()
    {
        var result = _sut.GetDescription("harad_raiders");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "{=taom_hideout_desc_harad}");
    }

    [TestMethod]
    public void GetDescription_RhunRaiders_ReturnsRhunTemplate()
    {
        var result = _sut.GetDescription("rhun_raiders");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "{=taom_hideout_desc_rhun}");
    }

    [TestMethod]
    public void GetDescription_UmbarCorsairs_ReturnsUmbarTemplate()
    {
        var result = _sut.GetDescription("umbar_corsairs");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "{=taom_hideout_desc_umbar}");
    }

    [TestMethod]
    public void GetDescription_UnknownCulture_ReturnsNull()
    {
        Assert.IsNull(_sut.GetDescription("vlandia"));
    }

    [TestMethod]
    public void GetDescription_VanillaForestBandits_ReturnsNull()
    {
        // Vanilla bandit cultures keep their own engine description -> we must NOT override.
        Assert.IsNull(_sut.GetDescription("forest_bandits"));
    }

    [TestMethod]
    public void GetDescription_EmptyString_ReturnsNull()
    {
        Assert.IsNull(_sut.GetDescription(""));
    }

    [TestMethod]
    public void GetDescription_Null_ReturnsNull()
    {
        Assert.IsNull(_sut.GetDescription(null!));
    }
}
