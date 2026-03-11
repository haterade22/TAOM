using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class LandmarkServiceTests
{
    private LandmarkService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new LandmarkService();
    }

    [TestMethod]
    public void GetAllLandmarks_ReturnsAllLandmarks()
    {
        var landmarks = _sut.GetAllLandmarks();

        Assert.AreEqual(32, landmarks.Count);
    }

    [TestMethod]
    public void GetCapitals_ReturnsOnlyCapitals()
    {
        var capitals = _sut.GetCapitals().ToList();

        Assert.IsTrue(capitals.Count > 0);
        Assert.IsTrue(capitals.All(l => l.Type == LandmarkType.Capital));
    }

    [TestMethod]
    public void GetByFaction_ValidId_ReturnsCorrectLandmarks()
    {
        var gondorLandmarks = _sut.GetByFaction(11).ToList();

        Assert.AreEqual(4, gondorLandmarks.Count);
        CollectionAssert.AreEquivalent(
            new[] { "minas_tirith", "dol_amroth", "osgiliath", "pelargir" },
            gondorLandmarks.Select(l => l.Id).ToList());
    }

    [TestMethod]
    public void GetByFaction_NeutralFaction_ReturnsNeutralLandmarks()
    {
        var neutralLandmarks = _sut.GetByFaction(0).ToList();

        Assert.IsTrue(neutralLandmarks.Count > 0);
        Assert.IsTrue(neutralLandmarks.All(l => l.FactionId == 0));
    }

    [TestMethod]
    public void GetByFaction_UnknownFaction_ReturnsEmpty()
    {
        var result = _sut.GetByFaction(999).ToList();

        Assert.AreEqual(0, result.Count);
    }
}
