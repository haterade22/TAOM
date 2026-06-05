using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Spider;

namespace TAOM.Tests.Features.Spider;

[TestClass]
public class SpiderTroopSpawnServiceTests
{
    private readonly SpiderTroopSpawnService _sut = new();

    [TestMethod]
    public void IsSpiderTroop_SpiderAnchorId_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsSpiderTroop(SpiderConfig.SpiderCharacterId));
    }

    [TestMethod]
    public void IsSpiderTroop_OtherCharacter_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsSpiderTroop("dg_goblin_slave"));
    }

    [TestMethod]
    public void IsSpiderTroop_Null_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsSpiderTroop(null));
    }

    [TestMethod]
    public void IsSpiderTroop_AnchorIdIsTaomSpiderCreature()
    {
        // Pin the contract: the anchor id is shared with spider_creature.xml + the recruitment pools.
        Assert.AreEqual("taom_spider_creature", SpiderConfig.SpiderCharacterId);
    }
}
