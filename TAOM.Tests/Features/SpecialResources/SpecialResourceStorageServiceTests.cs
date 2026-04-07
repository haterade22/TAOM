using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

[TestClass]
public class SpecialResourceStorageServiceTests
{
    private SpecialResourceStorageService _storage;

    [TestInitialize]
    public void Setup()
    {
        _storage = new SpecialResourceStorageService();
    }

    [TestMethod]
    public void Get_ReturnsZero_WhenHeroNotFound()
    {
        Assert.AreEqual(0f, _storage.Get("unknown_hero"));
    }

    [TestMethod]
    public void Set_StoresValue()
    {
        _storage.Set("hero1", 42.5f);
        Assert.AreEqual(42.5f, _storage.Get("hero1"));
    }

    [TestMethod]
    public void Set_ClampsToZero_WhenNegative()
    {
        _storage.Set("hero1", -10f);
        Assert.AreEqual(0f, _storage.Get("hero1"));
    }

    [TestMethod]
    public void Add_IncreasesValue()
    {
        _storage.Set("hero1", 100f);
        _storage.Add("hero1", 25f);
        Assert.AreEqual(125f, _storage.Get("hero1"));
    }

    [TestMethod]
    public void Add_DecreasesValue()
    {
        _storage.Set("hero1", 100f);
        _storage.Add("hero1", -30f);
        Assert.AreEqual(70f, _storage.Get("hero1"));
    }

    [TestMethod]
    public void Add_ClampsToZero_WhenResultNegative()
    {
        _storage.Set("hero1", 10f);
        _storage.Add("hero1", -50f);
        Assert.AreEqual(0f, _storage.Get("hero1"));
    }

    [TestMethod]
    public void MultipleHeroes_IndependentStorage()
    {
        _storage.Set("hero1", 100f);
        _storage.Set("hero2", 200f);
        Assert.AreEqual(100f, _storage.Get("hero1"));
        Assert.AreEqual(200f, _storage.Get("hero2"));
    }
}
