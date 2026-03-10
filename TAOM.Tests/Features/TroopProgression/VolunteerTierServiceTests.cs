using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.TroopProgression;

[TestClass]
public class VolunteerTierServiceTests
{
    private VolunteerTierService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new VolunteerTierService();
    }

    [TestMethod]
    public void MaxVolunteerTier_ReturnsHigherThanVanilla()
    {
        // Vanilla is 4; we need at least 6 to support higher-tier volunteers
        Assert.IsTrue(_sut.MaxVolunteerTier > 4);
    }

    [TestMethod]
    public void MaxVolunteerTier_Returns6()
    {
        Assert.AreEqual(6, _sut.MaxVolunteerTier);
    }
}
