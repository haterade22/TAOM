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
        Assert.AreEqual(0f, _storage.Get("unknown_hero", "scraps"));
    }

    [TestMethod]
    public void Set_StoresValue()
    {
        _storage.Set("hero1", "scraps", 42.5f);
        Assert.AreEqual(42.5f, _storage.Get("hero1", "scraps"));
    }

    [TestMethod]
    public void Set_ClampsToZero_WhenNegative()
    {
        _storage.Set("hero1", "scraps", -10f);
        Assert.AreEqual(0f, _storage.Get("hero1", "scraps"));
    }

    [TestMethod]
    public void Add_IncreasesValue()
    {
        _storage.Set("hero1", "scraps", 100f);
        _storage.Add("hero1", "scraps", 25f);
        Assert.AreEqual(125f, _storage.Get("hero1", "scraps"));
    }

    [TestMethod]
    public void Add_DecreasesValue()
    {
        _storage.Set("hero1", "scraps", 100f);
        _storage.Add("hero1", "scraps", -30f);
        Assert.AreEqual(70f, _storage.Get("hero1", "scraps"));
    }

    [TestMethod]
    public void Add_ClampsToZero_WhenResultNegative()
    {
        _storage.Set("hero1", "scraps", 10f);
        _storage.Add("hero1", "scraps", -50f);
        Assert.AreEqual(0f, _storage.Get("hero1", "scraps"));
    }

    [TestMethod]
    public void MultipleHeroes_IndependentStorage()
    {
        _storage.Set("hero1", "scraps", 100f);
        _storage.Set("hero2", "scraps", 200f);
        Assert.AreEqual(100f, _storage.Get("hero1", "scraps"));
        Assert.AreEqual(200f, _storage.Get("hero2", "scraps"));
    }

    [TestMethod]
    public void MultipleResources_SameHero_IndependentStorage()
    {
        _storage.Set("hero1", "scraps", 100f);
        _storage.Set("hero1", "mithril", 50f);
        Assert.AreEqual(100f, _storage.Get("hero1", "scraps"));
        Assert.AreEqual(50f, _storage.Get("hero1", "mithril"));
    }

    [TestMethod]
    public void ClampAll_CapsHighValues()
    {
        _storage.Set("hero1", "scraps", 999f);
        _storage.ClampAll(500f);
        Assert.AreEqual(500f, _storage.Get("hero1", "scraps"));
    }

    [TestMethod]
    public void ClampAll_ClampsNegativeToZero()
    {
        _storage.Set("hero1", "scraps", 50f);
        // Simulate save-edited negative via RestoreData
        var data = _storage.GetAllData();
        data["hero1:scraps"] = -10f;
        _storage.RestoreData(data);
        _storage.ClampAll(500f);
        Assert.AreEqual(0f, _storage.Get("hero1", "scraps"));
    }

    [TestMethod]
    public void RestoreData_HandlesNull()
    {
        _storage.Set("hero1", "scraps", 50f);
        _storage.RestoreData(null);
        Assert.AreEqual(0f, _storage.Get("hero1", "scraps"));
    }

    // ── Contains (Phase 9b deferred #133 P2 legacy-seed gate) ──
    //
    // Distinguishes "never seeded this resource for this hero" from "seeded and then
    // spent to zero". The legacy-save seed in OnSessionLaunched uses this to avoid
    // re-seeding on every kingdom-change (Mordor→Gondor exposes a new resource slot
    // with no balance; seeding it would bypass the "earn it" progression).

    [TestMethod]
    public void Contains_ReturnsFalse_WhenKeyMissing()
    {
        Assert.IsFalse(_storage.Contains("hero1", "war_spoils"));
    }

    [TestMethod]
    public void Contains_ReturnsTrue_AfterSet_EvenWithZeroValue()
    {
        _storage.Set("hero1", "war_spoils", 0f);
        Assert.IsTrue(_storage.Contains("hero1", "war_spoils"));
    }

    [TestMethod]
    public void Contains_ReturnsTrue_AfterSpendToZero()
    {
        _storage.Set("hero1", "war_spoils", 50f);
        _storage.Add("hero1", "war_spoils", -50f);
        Assert.IsTrue(_storage.Contains("hero1", "war_spoils"));
    }

    [TestMethod]
    public void Contains_DistinguishesResourcesPerHero()
    {
        _storage.Set("hero1", "war_spoils", 10f);
        Assert.IsTrue(_storage.Contains("hero1", "war_spoils"));
        Assert.IsFalse(_storage.Contains("hero1", "gems"));
        Assert.IsFalse(_storage.Contains("hero2", "war_spoils"));
    }
}
