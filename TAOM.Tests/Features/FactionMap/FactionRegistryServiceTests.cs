using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class FactionRegistryServiceTests
{
    private FactionRegistryService _sut = null!;
    private Dictionary<string, RegionData> _regions = null!;
    private Dictionary<string, FactionData> _factions = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new FactionRegistryService();

        _regions = new Dictionary<string, RegionData>
        {
            ["kingdom_of_gondor"] = new RegionData { FactionId = "gondor" },
            ["deco_mountains"] = new RegionData { FactionId = "" },
            ["kingdom_of_rohan"] = new RegionData { FactionId = "rohan" }
        };

        _factions = new Dictionary<string, FactionData>
        {
            ["gondor"] = new FactionData { Name = "Kingdom of Gondor", Color = "#4169E1FF" },
            ["rohan"] = new FactionData { Name = "Kingdom of Rohan", Color = "#228B22FF" }
        };
    }

    [TestMethod]
    public void Initialize_SetsData_GetRegionReturnsCorrectData()
    {
        _sut.Initialize(_regions, _factions);

        var region = _sut.GetRegion("kingdom_of_gondor");

        Assert.IsNotNull(region);
        Assert.AreEqual("gondor", region.FactionId);
    }

    [TestMethod]
    public void GetRegion_UnknownKey_ReturnsNull()
    {
        _sut.Initialize(_regions, _factions);

        var result = _sut.GetRegion("kingdom_of_mordor");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetRegion_EmptyKey_ReturnsNull()
    {
        _sut.Initialize(_regions, _factions);

        Assert.IsNull(_sut.GetRegion(""));
        Assert.IsNull(_sut.GetRegion(null!));
    }

    [TestMethod]
    public void GetFactionForRegion_ValidRegionWithFaction_ReturnsFaction()
    {
        _sut.Initialize(_regions, _factions);

        var faction = _sut.GetFactionForRegion("kingdom_of_gondor");

        Assert.IsNotNull(faction);
        Assert.AreEqual("Kingdom of Gondor", faction.Name);
        Assert.AreEqual("#4169E1FF", faction.Color);
    }

    [TestMethod]
    public void GetFactionForRegion_DecorationRegion_ReturnsNull()
    {
        _sut.Initialize(_regions, _factions);

        var result = _sut.GetFactionForRegion("deco_mountains");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetFactionForRegion_UnknownRegion_ReturnsNull()
    {
        _sut.Initialize(_regions, _factions);

        var result = _sut.GetFactionForRegion("nonexistent_region");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetAllRegionKeys_ReturnsAllKeys()
    {
        _sut.Initialize(_regions, _factions);

        var keys = _sut.GetAllRegionKeys().ToList();

        Assert.AreEqual(3, keys.Count);
        CollectionAssert.Contains(keys, "kingdom_of_gondor");
        CollectionAssert.Contains(keys, "deco_mountains");
        CollectionAssert.Contains(keys, "kingdom_of_rohan");
    }

    [TestMethod]
    public void GetFaction_ValidId_ReturnsFaction()
    {
        _sut.Initialize(_regions, _factions);

        var faction = _sut.GetFaction("rohan");

        Assert.IsNotNull(faction);
        Assert.AreEqual("Kingdom of Rohan", faction.Name);
    }

    [TestMethod]
    public void GetFaction_UnknownId_ReturnsNull()
    {
        _sut.Initialize(_regions, _factions);

        var result = _sut.GetFaction("mordor");

        Assert.IsNull(result);
    }
}
