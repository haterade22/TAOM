using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.BannerColorPersistence;

namespace TAOM.Tests.Features.BannerColorPersistence;

[TestClass]
public class AgentColorStoreTests
{
    private AgentColorStore _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new AgentColorStore();
    }

    [TestMethod]
    public void Register_ThenTryGet_ReturnsColor()
    {
        // Arrange
        var info = new ClanColorInfo("clan_gondor_1", 0xFF112233, 0xFF445566);

        // Act
        _sut.Register(42, info);
        bool found = _sut.TryGetColors(42, out var result);

        // Assert
        Assert.IsTrue(found);
        Assert.AreEqual("clan_gondor_1", result.ClanStringId);
        Assert.AreEqual(0xFF112233u, result.Color1);
        Assert.AreEqual(0xFF445566u, result.Color2);
    }

    [TestMethod]
    public void Register_Twice_OverwritesFirst()
    {
        // Arrange
        var info1 = new ClanColorInfo("clan_gondor_1", 0xFF111111, 0xFF222222);
        var info2 = new ClanColorInfo("clan_mordor_1", 0xFF333333, 0xFF444444);

        // Act
        _sut.Register(42, info1);
        _sut.Register(42, info2);
        _sut.TryGetColors(42, out var result);

        // Assert
        Assert.AreEqual("clan_mordor_1", result.ClanStringId);
    }

    [TestMethod]
    public void TryGet_Unregistered_ReturnsFalse()
    {
        // Act
        bool found = _sut.TryGetColors(99, out _);

        // Assert
        Assert.IsFalse(found);
    }

    [TestMethod]
    public void Clear_EmptiesAllEntries()
    {
        // Arrange
        _sut.Register(1, new ClanColorInfo("clan_a", 0xFF111111, 0xFF222222));
        _sut.Register(2, new ClanColorInfo("clan_b", 0xFF333333, 0xFF444444));

        // Act
        _sut.Clear();

        // Assert
        Assert.IsFalse(_sut.TryGetColors(1, out _));
        Assert.IsFalse(_sut.TryGetColors(2, out _));
    }
}
