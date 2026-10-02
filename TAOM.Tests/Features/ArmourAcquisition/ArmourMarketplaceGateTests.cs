using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// CultureMarketplace's per-town question. With gating off every item its XML lets be merchandise qualifies
/// and no town level is read; with it on, each town is judged at its own armoury level, by settlement id.
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
    public void ForTown_GatingInactive_XmlMerchandiseQualifies_AndNoLevelIsRead()
    {
        _gate.IsActive.Returns(false);
        _gate.GetRecord("lord_a").Returns(Record("lord_a", isMerchandise: true));

        var isEligible = _stockGate.ForTown("town_A");

        Assert.IsTrue(isEligible("lord_a"));
        _towns.DidNotReceive().GetBarracksLevel(Arg.Any<string>());
        _gate.DidNotReceive().IsEligibleForMarket(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ForTown_GatingInactive_XmlNonMerchandise_Refused()
    {
        // The troll gear (is_merchandise="false"): CultureMarketplace's pool ignores NotMerchandise, so with
        // the gate off this predicate is the only thing keeping it off a Mordor stall.
        _gate.IsActive.Returns(false);
        _gate.GetRecord("lotr_troll_armor").Returns(Record("lotr_troll_armor", isMerchandise: false));

        Assert.IsFalse(_stockGate.ForTown("town_A")("lotr_troll_armor"));
    }

    [TestMethod]
    public void ForTown_GatingInactive_UnknownItem_Qualifies()
    {
        // No record means the gate never read this game's items (a failed init): behave as before.
        _gate.IsActive.Returns(false);
        _gate.GetRecord("unknown").Returns((ArmourItemRecord?)null);

        Assert.IsTrue(_stockGate.ForTown("town_A")("unknown"));
    }

    [TestMethod]
    public void ForTown_GatingActive_AsksTheGateAtThatTownsLevel()
    {
        _gate.IsEligibleForMarket("elite_a", 1).Returns(false);
        _gate.IsEligibleForMarket("elite_a", 3).Returns(true);

        Assert.IsFalse(_stockGate.ForTown("town_A")("elite_a"));
        Assert.IsTrue(_stockGate.ForTown("town_B")("elite_a"));
    }

    private static ArmourItemRecord Record(string id, bool isMerchandise) =>
        new(id, ArmourSlot.Body, engineTier: 2, isMerchandise, cultureId: "mordor", value: 100);
}
