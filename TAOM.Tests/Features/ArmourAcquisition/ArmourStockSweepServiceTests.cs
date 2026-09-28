using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The daily market sweep removes exactly the gated pieces a town's armoury does not allow today, and
/// nothing else: light and medium stock, ungoverned goods and pieces the armoury allows all stay. Each
/// town is swept at its own level (its Barracks plus any visiting armourer), by settlement id.
/// </summary>
[TestClass]
public class ArmourStockSweepServiceTests
{
    private const int Today = 20;

    private IArmourGateService _gate = null!;
    private ITownRosterAdapter _rosters = null!;
    private IArmouryTownAdapter _towns = null!;
    private ArmourAcquisitionState _state = null!;
    private ArmourStockSweepService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _gate = Substitute.For<IArmourGateService>();
        _gate.IsActive.Returns(true);
        _rosters = Substitute.For<ITownRosterAdapter>();
        _rosters.RemoveItemById(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _rosters.EnumerateRosterById(Arg.Any<string>()).Returns(new List<RosterItemSnapshot>());
        _towns = Substitute.For<IArmouryTownAdapter>();
        _towns.Today.Returns(Today);
        _towns.GetBarracksLevel("town_A").Returns(1);
        _towns.GetBarracksLevel("town_B").Returns(3);
        _state = new ArmourAcquisitionState();
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);
        var settings = Substitute.For<IArmourAcquisitionSettingsProvider>();
        settings.VisitingArmourerEnabled.Returns(true);
        var levels = new ArmouryLevelService(new VisitingArmourerService(_state, config, _towns), settings, _towns);
        _service = new ArmourStockSweepService(_gate, levels, _rosters);
    }

    private void Roster(string townId, params (string id, int count, ArmourClass? cls)[] rows)
    {
        var list = new List<RosterItemSnapshot>();
        foreach (var (id, count, cls) in rows)
        {
            list.Add(new RosterItemSnapshot(id, "gondor", count));
            _gate.GetClass(id).Returns(cls);
        }
        _rosters.EnumerateRosterById(townId).Returns(list);
    }

    [TestMethod]
    public void SweepTown_RemovesGatedPiecesTheTownMayNotStock()
    {
        Roster("town_A", ("elite_a", 2, ArmourClass.Elite), ("named_a", 1, ArmourClass.Named));
        _gate.IsEligibleForMarket(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        var removed = _service.SweepTown("town_A");

        Assert.AreEqual(3, removed);
        _rosters.Received(1).RemoveItemById("town_A", "elite_a", 2);
        _rosters.Received(1).RemoveItemById("town_A", "named_a", 1);
    }

    [TestMethod]
    public void SweepTown_KeepsGatedPiecesTheArmouryAllows()
    {
        Roster("town_A", ("heavy_a", 1, ArmourClass.Heavy));
        _gate.IsEligibleForMarket("heavy_a", 1).Returns(true);

        Assert.AreEqual(0, _service.SweepTown("town_A"));
        _rosters.DidNotReceive().RemoveItemById(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void SweepTown_KeepsFreeClassesAndUngovernedGoods()
    {
        // A routed mount the XML marks non-merchandise (the Animalia elk) is ungoverned: the guaranteed-stock
        // pass adds it every day, so the sweep must never fight it.
        Roster("town_A", ("light_a", 3, ArmourClass.Light), ("grain", 40, null), ("taom_animalia_elk_a", 1, null));
        _gate.IsEligibleForMarket(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        Assert.AreEqual(0, _service.SweepTown("town_A"));
        _rosters.DidNotReceive().RemoveItemById(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void SweepTown_ARemovalTheRosterRefuses_IsNotCounted()
    {
        Roster("town_A", ("named_a", 1, ArmourClass.Named));
        _gate.IsEligibleForMarket("named_a", Arg.Any<int>()).Returns(false);
        _rosters.RemoveItemById("town_A", "named_a", 1).Returns(false);

        Assert.AreEqual(0, _service.SweepTown("town_A"));
    }

    [TestMethod]
    public void SweepTown_GatingInactive_TouchesNothing()
    {
        _gate.IsActive.Returns(false);
        Roster("town_A", ("elite_a", 2, ArmourClass.Elite));
        _gate.IsEligibleForMarket(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        Assert.AreEqual(0, _service.SweepTown("town_A"));
        _rosters.DidNotReceive().EnumerateRosterById(Arg.Any<string>());
    }

    [TestMethod]
    public void SweepTown_EachTownIsJudgedAtItsOwnLevel()
    {
        Roster("town_A", ("elite_a", 1, ArmourClass.Elite));
        Roster("town_B", ("elite_a", 1, ArmourClass.Elite));
        _gate.IsEligibleForMarket("elite_a", 1).Returns(false);
        _gate.IsEligibleForMarket("elite_a", 3).Returns(true);

        Assert.AreEqual(1, _service.SweepTown("town_A"));
        Assert.AreEqual(0, _service.SweepTown("town_B"));
        _rosters.Received(1).RemoveItemById("town_A", "elite_a", 1);
        _rosters.DidNotReceive().RemoveItemById("town_B", Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void SweepTown_VisitEnded_RemovesTheEliteStockItAllowed()
    {
        // Barracks 1 plus a visiting armourer's bonus is level 2, which lets elite pieces in; the day the
        // visit ends the town is back at level 1 and the elite stock must leave.
        Roster("town_A", ("elite_a", 1, ArmourClass.Elite));
        _gate.IsEligibleForMarket("elite_a", 1).Returns(false);
        _gate.IsEligibleForMarket("elite_a", 2).Returns(true);

        _state.VisitUntilDay["town_A"] = Today + 1;
        Assert.AreEqual(0, _service.SweepTown("town_A"), "during the visit the armoury allows elite");

        _state.VisitUntilDay["town_A"] = Today;
        Assert.AreEqual(1, _service.SweepTown("town_A"), "the visit is over on its last day");
    }
}
