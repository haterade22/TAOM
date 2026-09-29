using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// CultureMarketplace's per-town question. With gating off every item qualifies and no town level is read;
/// with it on, each town is judged at its own armoury level, by settlement id.
/// </summary>
[TestClass]
public class ArmourMarketplaceGateTests
{
    private IArmourGateService _gate = null!;
    private IArmouryTownAdapter _towns = null!;
    private ArmourMarketplaceGate _stockGate = null!;

    [TestInitialize]
    public void Setup()
    {
        _gate = Substitute.For<IArmourGateService>();
        _gate.IsActive.Returns(true);
        _towns = Substitute.For<IArmouryTownAdapter>();
        _towns.GetBarracksLevel("town_A").Returns(1);
        _towns.GetBarracksLevel("town_B").Returns(3);
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);
        var settings = Substitute.For<IArmourAcquisitionSettingsProvider>();
        var levels = new ArmouryLevelService(new VisitingArmourerService(new ArmourAcquisitionState(), config, _towns), settings, _towns);
        _stockGate = new ArmourMarketplaceGate(_gate, levels);
    }

    [TestMethod]
    public void ForTown_GatingInactive_EveryItemQualifies_AndNoLevelIsRead()
    {
        _gate.IsActive.Returns(false);

        var isEligible = _stockGate.ForTown("town_A");

        Assert.IsTrue(isEligible("lord_a"));
        _towns.DidNotReceive().GetBarracksLevel(Arg.Any<string>());
        _gate.DidNotReceive().IsEligibleForMarket(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ForTown_GatingActive_AsksTheGateAtThatTownsLevel()
    {
        _gate.IsEligibleForMarket("elite_a", 1).Returns(false);
        _gate.IsEligibleForMarket("elite_a", 3).Returns(true);

        Assert.IsFalse(_stockGate.ForTown("town_A")("elite_a"));
        Assert.IsTrue(_stockGate.ForTown("town_B")("elite_a"));
    }
}
