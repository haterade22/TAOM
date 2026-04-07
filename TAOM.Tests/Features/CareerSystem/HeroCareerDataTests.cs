using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class HeroCareerDataTests
{
    private HeroCareerData _data;

    [TestInitialize]
    public void Setup()
    {
        _data = new HeroCareerData("hero1");
    }

    [TestMethod]
    public void Constructor_SetsHeroStringId()
    {
        Assert.AreEqual("hero1", _data.HeroStringId);
    }

    [TestMethod]
    public void Constructor_InitializesEmptyChoices()
    {
        Assert.AreEqual(0, _data.GetChoiceCount());
    }

    [TestMethod]
    public void AddChoice_NewChoice_ReturnsTrue()
    {
        Assert.IsTrue(_data.AddChoice("choice1"));
    }

    [TestMethod]
    public void AddChoice_NewChoice_IncreasesCount()
    {
        _data.AddChoice("choice1");
        Assert.AreEqual(1, _data.GetChoiceCount());
    }

    [TestMethod]
    public void AddChoice_Duplicate_ReturnsFalse()
    {
        _data.AddChoice("choice1");
        Assert.IsFalse(_data.AddChoice("choice1"));
    }

    [TestMethod]
    public void AddChoice_Duplicate_DoesNotIncreaseCount()
    {
        _data.AddChoice("choice1");
        _data.AddChoice("choice1");
        Assert.AreEqual(1, _data.GetChoiceCount());
    }

    [TestMethod]
    public void HasChoice_ExistingChoice_ReturnsTrue()
    {
        _data.AddChoice("choice1");
        Assert.IsTrue(_data.HasChoice("choice1"));
    }

    [TestMethod]
    public void HasChoice_UnknownChoice_ReturnsFalse()
    {
        Assert.IsFalse(_data.HasChoice("nonexistent"));
    }

    [TestMethod]
    public void RemoveChoice_ExistingChoice_ReturnsTrue()
    {
        _data.AddChoice("choice1");
        Assert.IsTrue(_data.RemoveChoice("choice1"));
    }

    [TestMethod]
    public void RemoveChoice_ExistingChoice_DecreasesCount()
    {
        _data.AddChoice("choice1");
        _data.RemoveChoice("choice1");
        Assert.AreEqual(0, _data.GetChoiceCount());
    }

    [TestMethod]
    public void RemoveChoice_UnknownChoice_ReturnsFalse()
    {
        Assert.IsFalse(_data.RemoveChoice("nonexistent"));
    }

    [TestMethod]
    public void GetChoiceCount_AfterMultipleAdds_ReturnsCorrectCount()
    {
        _data.AddChoice("choice1");
        _data.AddChoice("choice2");
        _data.AddChoice("choice3");
        Assert.AreEqual(3, _data.GetChoiceCount());
    }
}
