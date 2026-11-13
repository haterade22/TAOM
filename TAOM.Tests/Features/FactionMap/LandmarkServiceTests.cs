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
    public void GetCapitals_ReturnsOnlyCapitals()
    {
        var capitals = _sut.GetCapitals().ToList();

        Assert.IsTrue(capitals.Count > 0);
        Assert.IsTrue(capitals.All(l => l.Type == LandmarkType.Capital));
    }
}
