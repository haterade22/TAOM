using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// A town's armoury level is its Barracks level (0 to 3, Mike's call: KEYforce's "armoury or some
/// building in that settlement is tier X-Y-Z") plus a visiting master armourer's bonus, capped at 3.
/// </summary>
[TestClass]
public class ArmouryLevelServiceTests
{
    private ArmourAcquisitionState _state = null!;
    private IArmourAcquisitionSettingsProvider _settings = null!;
    private IArmouryTownAdapter _towns = null!;
    private ArmouryLevelService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _state = new ArmourAcquisitionState();
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);
        _settings = Substitute.For<IArmourAcquisitionSettingsProvider>();
        _settings.VisitingArmourerEnabled.Returns(true);
        _towns = Substitute.For<IArmouryTownAdapter>();
        _towns.Today.Returns(5);
        _service = new ArmouryLevelService(new VisitingArmourerService(_state, config, _towns), _settings, _towns);
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(2, 2)]
    [DataRow(3, 3)]
    [DataRow(-1, 0)]
    [DataRow(9, 3)]
    public void GetTownLevel_IsTheBarracksLevelClampedToTheArmouryRange(int barracks, int expected)
    {
        _towns.GetBarracksLevel("town_A").Returns(barracks);

        Assert.AreEqual(expected, _service.GetTownLevel("town_A"));
    }

    [TestMethod]
    public void GetTownLevel_AVisitingArmourerAddsHisBonus()
    {
        _towns.GetBarracksLevel("town_A").Returns(1);
        _state.VisitUntilDay["town_A"] = 9;

        Assert.AreEqual(2, _service.GetTownLevel("town_A"));
    }

    [TestMethod]
    public void GetTownLevel_VisitBonusStillCapsAtThree()
    {
        _towns.GetBarracksLevel("town_A").Returns(3);
        _state.VisitUntilDay["town_A"] = 9;

        Assert.AreEqual(3, _service.GetTownLevel("town_A"));
    }

    [TestMethod]
    public void GetTownLevel_VisitOver_NoBonus()
    {
        _towns.GetBarracksLevel("town_A").Returns(1);
        _state.VisitUntilDay["town_A"] = 5;

        Assert.AreEqual(1, _service.GetTownLevel("town_A"), "the visit ends on its last day");
    }

    [TestMethod]
    public void GetTownLevel_VisitingArmourersOff_IgnoresAStandingVisit()
    {
        _settings.VisitingArmourerEnabled.Returns(false);
        _towns.GetBarracksLevel("town_A").Returns(1);
        _state.VisitUntilDay["town_A"] = 9;

        Assert.AreEqual(1, _service.GetTownLevel("town_A"));
    }

    [TestMethod]
    public void GetTownLevel_EachTownReadsItsOwnBarracks()
    {
        _towns.GetBarracksLevel("town_A").Returns(1);
        _towns.GetBarracksLevel("town_B").Returns(3);

        Assert.AreEqual(1, _service.GetTownLevel("town_A"));
        Assert.AreEqual(3, _service.GetTownLevel("town_B"));
    }
}
