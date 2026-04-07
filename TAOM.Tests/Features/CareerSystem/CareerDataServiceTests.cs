using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerDataServiceTests
{
    private CareerDataService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = new CareerDataService();
    }

    // ── GetOrCreateData ──

    [TestMethod]
    public void GetOrCreateData_NewHero_ReturnsEmptyData()
    {
        var data = _service.GetOrCreateData("hero1");
        Assert.IsNotNull(data);
        Assert.AreEqual("hero1", data.HeroStringId);
        Assert.IsNull(data.CareerStringId);
        Assert.AreEqual(0, data.GetChoiceCount());
    }

    [TestMethod]
    public void GetOrCreateData_ExistingHero_ReturnsSameInstance()
    {
        var first = _service.GetOrCreateData("hero1");
        var second = _service.GetOrCreateData("hero1");
        Assert.AreSame(first, second);
    }

    // ── SetCareer ──

    [TestMethod]
    public void SetCareer_ValidCareer_PersistsCareerStringId()
    {
        _service.SetCareer("hero1", "warboss");
        Assert.AreEqual("warboss", _service.GetCareerStringId("hero1"));
    }

    [TestMethod]
    public void SetCareer_CreatesDataIfNotExists()
    {
        _service.SetCareer("hero1", "warboss");
        Assert.IsTrue(_service.HasCareer("hero1"));
    }

    // ── HasCareer ──

    [TestMethod]
    public void HasCareer_NoCareerSet_ReturnsFalse()
    {
        _service.GetOrCreateData("hero1");
        Assert.IsFalse(_service.HasCareer("hero1"));
    }

    [TestMethod]
    public void HasCareer_CareerSet_ReturnsTrue()
    {
        _service.SetCareer("hero1", "warboss");
        Assert.IsTrue(_service.HasCareer("hero1"));
    }

    [TestMethod]
    public void HasCareer_UnknownHero_ReturnsFalse()
    {
        Assert.IsFalse(_service.HasCareer("nonexistent"));
    }

    // ── TryAddChoice ──

    [TestMethod]
    public void TryAddChoice_NewChoice_ReturnsTrue()
    {
        _service.SetCareer("hero1", "warboss");
        Assert.IsTrue(_service.TryAddChoice("hero1", "choice1", 5));
    }

    [TestMethod]
    public void TryAddChoice_AtMaxChoices_ReturnsFalse()
    {
        _service.SetCareer("hero1", "warboss");
        _service.TryAddChoice("hero1", "choice1", 2);
        _service.TryAddChoice("hero1", "choice2", 2);
        Assert.IsFalse(_service.TryAddChoice("hero1", "choice3", 2));
    }

    [TestMethod]
    public void TryAddChoice_DuplicateChoice_ReturnsFalse()
    {
        _service.SetCareer("hero1", "warboss");
        _service.TryAddChoice("hero1", "choice1", 5);
        Assert.IsFalse(_service.TryAddChoice("hero1", "choice1", 5));
    }

    // ── RemoveChoice ──

    [TestMethod]
    public void RemoveChoice_ExistingChoice_RemovesIt()
    {
        _service.SetCareer("hero1", "warboss");
        _service.TryAddChoice("hero1", "choice1", 5);
        _service.RemoveChoice("hero1", "choice1");
        Assert.AreEqual(0, _service.GetChoiceCount("hero1"));
    }

    // ── GetChoiceIds ──

    [TestMethod]
    public void GetChoiceIds_ReturnsAllAddedChoices()
    {
        _service.SetCareer("hero1", "warboss");
        _service.TryAddChoice("hero1", "choice1", 5);
        _service.TryAddChoice("hero1", "choice2", 5);
        var choices = _service.GetChoiceIds("hero1");
        Assert.AreEqual(2, choices.Count);
        Assert.AreEqual("choice1", choices[0]);
        Assert.AreEqual("choice2", choices[1]);
    }

    [TestMethod]
    public void GetChoiceIds_UnknownHero_ReturnsEmpty()
    {
        var choices = _service.GetChoiceIds("nonexistent");
        Assert.AreEqual(0, choices.Count);
    }

    // ── Tier Unlocks ──

    [TestMethod]
    public void IsTierUnlocked_NeverUnlocked_ReturnsFalse()
    {
        _service.GetOrCreateData("hero1");
        Assert.IsFalse(_service.IsTierUnlocked("hero1", 2));
    }

    [TestMethod]
    public void UnlockTier_Tier2_SetsTierUnlocked()
    {
        _service.GetOrCreateData("hero1");
        _service.UnlockTier("hero1", 2);
        Assert.IsTrue(_service.IsTierUnlocked("hero1", 2));
    }

    [TestMethod]
    public void UnlockTier_DoesNotAffectOtherTiers()
    {
        _service.GetOrCreateData("hero1");
        _service.UnlockTier("hero1", 2);
        Assert.IsFalse(_service.IsTierUnlocked("hero1", 3));
    }

    // ── ClearCareer ──

    [TestMethod]
    public void ClearCareer_ExistingData_RemovesCareerAndChoices()
    {
        _service.SetCareer("hero1", "warboss");
        _service.TryAddChoice("hero1", "choice1", 5);
        _service.UnlockTier("hero1", 2);
        _service.ClearCareer("hero1");

        Assert.IsFalse(_service.HasCareer("hero1"));
        Assert.AreEqual(0, _service.GetChoiceCount("hero1"));
        Assert.IsFalse(_service.IsTierUnlocked("hero1", 2));
    }

    [TestMethod]
    public void ClearCareer_UnknownHero_DoesNotThrow()
    {
        _service.ClearCareer("nonexistent");
    }

    // ── Persistence Round-Trip ──

    [TestMethod]
    public void GetAllData_RestoreData_RoundTrip()
    {
        _service.SetCareer("hero1", "warboss");
        _service.TryAddChoice("hero1", "choice1", 5);
        _service.UnlockTier("hero1", 2);

        var snapshot = _service.GetAllData();

        var newService = new CareerDataService();
        newService.RestoreData(snapshot);

        Assert.AreEqual("warboss", newService.GetCareerStringId("hero1"));
        Assert.AreEqual(1, newService.GetChoiceCount("hero1"));
        Assert.IsTrue(newService.IsTierUnlocked("hero1", 2));
    }

    [TestMethod]
    public void RestoreData_NullData_DoesNotThrow()
    {
        _service.RestoreData(null);
        Assert.IsFalse(_service.HasCareer("hero1"));
    }

    [TestMethod]
    public void RestoreData_ReplacesExistingState()
    {
        _service.SetCareer("hero1", "warboss");

        var freshData = new Dictionary<string, HeroCareerData>
        {
            ["hero2"] = new HeroCareerData("hero2") { CareerStringId = "ranger" }
        };
        _service.RestoreData(freshData);

        Assert.IsFalse(_service.HasCareer("hero1"));
        Assert.AreEqual("ranger", _service.GetCareerStringId("hero2"));
    }
}
