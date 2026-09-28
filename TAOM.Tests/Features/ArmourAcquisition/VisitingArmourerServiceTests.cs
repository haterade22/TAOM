using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// A visiting master armourer raises one town's armoury level for a few days. The roll, the expiry, the
/// choice of town and the bonus are pinned here; the town list, the Barracks levels and the day come from
/// the town adapter.
/// </summary>
[TestClass]
public class VisitingArmourerServiceTests
{
    private ArmourAcquisitionState _state = null!;
    private IArmourAcquisitionConfigProvider _config = null!;
    private IArmouryTownAdapter _towns = null!;
    private VisitingArmourerService _service = null!;

    private static ArmourAcquisitionConfig Config(float chance, int days = 7, int bonus = 1)
    {
        var d = ArmourAcquisitionConfig.Default;
        return new ArmourAcquisitionConfig(d.Enabled, d.HeavyLevel, d.EliteLevel, d.LordLevel, d.Recipes, d.NamedWeapons,
            d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation, chance, days, bonus, d.Ladder);
    }

    private sealed class FixedRandom : Random
    {
        private readonly double _value;
        public FixedRandom(double value) { _value = value; }
        public override double NextDouble() => _value;
        public override int Next(int maxValue) => 0;
    }

    private void Towns(params string[] ids) => _towns.AllTownIds().Returns(ids);

    [TestInitialize]
    public void Setup()
    {
        _state = new ArmourAcquisitionState();
        _config = Substitute.For<IArmourAcquisitionConfigProvider>();
        _config.GetConfig().Returns(Config(chance: 0.5f));
        _towns = Substitute.For<IArmouryTownAdapter>();
        _towns.GetBarracksLevel(Arg.Any<string>()).Returns(1);
        Towns("town_A", "town_B");
        _service = new VisitingArmourerService(_state, _config, _towns);
    }

    [TestMethod]
    public void OnDailyTick_RollUnderTheChance_StartsAVisit()
    {
        var town = _service.OnDailyTick(10, new FixedRandom(0.1), enabled: true);

        Assert.AreEqual("town_A", town);
        Assert.AreEqual(17, _state.VisitUntilDay["town_A"]);
    }

    [TestMethod]
    public void OnDailyTick_RollAtOrAboveTheChance_StartsNothing()
    {
        Assert.IsNull(_service.OnDailyTick(10, new FixedRandom(0.5), enabled: true));
        Assert.AreEqual(0, _state.VisitUntilDay.Count);
    }

    [TestMethod]
    public void OnDailyTick_RollEqualToAFractionalChance_StartsNothing()
    {
        // 0.1f widened to double is 0.1000000015, so a double comparison would let a roll of 0.1 through.
        _config.GetConfig().Returns(Config(chance: 0.1f));

        Assert.IsNull(_service.OnDailyTick(10, new FixedRandom(0.1), enabled: true));
    }

    [TestMethod]
    public void OnDailyTick_Disabled_StartsNothing()
    {
        Assert.IsNull(_service.OnDailyTick(10, new FixedRandom(0.0), enabled: false));
    }

    [TestMethod]
    public void OnDailyTick_NoTowns_StartsNothing()
    {
        Towns();

        Assert.IsNull(_service.OnDailyTick(10, new FixedRandom(0.0), enabled: true));
    }

    [TestMethod]
    public void OnDailyTick_SkipsATownAlreadyVisited()
    {
        _state.VisitUntilDay["town_A"] = 30;

        var town = _service.OnDailyTick(10, new FixedRandom(0.0), enabled: true);

        Assert.AreEqual("town_B", town);
    }

    [TestMethod]
    public void OnDailyTick_SkipsATownWhoseArmouryIsAlreadyAtTheTop()
    {
        // A level-3 armoury cannot rise, so a visit there would change nothing and still be announced.
        _towns.GetBarracksLevel("town_A").Returns(ArmourAcquisitionConfig.MaxArmouryLevel);

        var town = _service.OnDailyTick(10, new FixedRandom(0.0), enabled: true);

        Assert.AreEqual("town_B", town);
    }

    [TestMethod]
    public void OnDailyTick_EveryTownVisitedOrAtTheTop_StartsNothing()
    {
        _state.VisitUntilDay["town_A"] = 30;
        _towns.GetBarracksLevel("town_B").Returns(ArmourAcquisitionConfig.MaxArmouryLevel);

        Assert.IsNull(_service.OnDailyTick(10, new FixedRandom(0.0), enabled: true));
    }

    [TestMethod]
    public void OnDailyTick_ExpiresFinishedVisits_EvenWhenDisabled()
    {
        _state.VisitUntilDay["town_A"] = 10;
        _state.VisitUntilDay["town_B"] = 11;

        _service.OnDailyTick(10, new FixedRandom(0.9), enabled: false);

        Assert.IsFalse(_state.VisitUntilDay.ContainsKey("town_A"));
        Assert.IsTrue(_state.VisitUntilDay.ContainsKey("town_B"));
    }

    [TestMethod]
    public void GetBonus_DuringAVisit_IsTheConfiguredBonus()
    {
        _config.GetConfig().Returns(Config(chance: 0.5f, bonus: 2));
        _state.VisitUntilDay["town_A"] = 15;

        Assert.AreEqual(2, _service.GetBonus("town_A", 14));
        Assert.AreEqual(0, _service.GetBonus("town_A", 15), "the visit ends on its last day");
        Assert.AreEqual(0, _service.GetBonus("town_B", 14));
    }
}
