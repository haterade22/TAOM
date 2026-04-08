using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SettlementGuards;
using TAOM.Features.SettlementGuards.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.SettlementGuards;

[TestClass]
public class SettlementGuardServiceTests
{
    private ISettlementGuardConfigProvider _config;
    private IRandomProvider _random;
    private IModLogger _logger;
    private SettlementGuardService _sut;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISettlementGuardConfigProvider>();
        _random = Substitute.For<IRandomProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new SettlementGuardService(_config, _random, _logger);
    }

    // --- Fallback chain: settlement → clan → culture ---

    [TestMethod]
    public void ResolveGuardTroopId_SettlementHit_ReturnsSettlementGuard()
    {
        var pool = MakePool(("gondor_citadel_guard", 1));
        _config.GetBySettlementId("town_EW1").Returns(pool);
        _random.Next(Arg.Any<int>()).Returns(0);

        var context = new SettlementGuardContext("town_EW1", "clan_empire_west_1", "gondor");
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.AreEqual("gondor_citadel_guard", result);
    }

    [TestMethod]
    public void ResolveGuardTroopId_SettlementMiss_ClanHit_ReturnsClanGuard()
    {
        _config.GetBySettlementId("town_EW99").Returns((GuardPool)null);
        var pool = MakePool(("gondor_tower_guard", 1));
        _config.GetByClanId("clan_empire_west_1").Returns(pool);
        _random.Next(Arg.Any<int>()).Returns(0);

        var context = new SettlementGuardContext("town_EW99", "clan_empire_west_1", "gondor");
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.AreEqual("gondor_tower_guard", result);
    }

    [TestMethod]
    public void ResolveGuardTroopId_SettlementMiss_ClanMiss_CultureHit_ReturnsCultureGuard()
    {
        _config.GetBySettlementId("town_EW99").Returns((GuardPool)null);
        _config.GetByClanId("clan_unknown").Returns((GuardPool)null);
        var pool = MakePool(("guard_gondor", 1));
        _config.GetByCultureId("gondor").Returns(pool);
        _random.Next(Arg.Any<int>()).Returns(0);

        var context = new SettlementGuardContext("town_EW99", "clan_unknown", "gondor");
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.AreEqual("guard_gondor", result);
    }

    [TestMethod]
    public void ResolveGuardTroopId_AllMiss_ReturnsNull()
    {
        _config.GetBySettlementId(Arg.Any<string>()).Returns((GuardPool)null);
        _config.GetByClanId(Arg.Any<string>()).Returns((GuardPool)null);
        _config.GetByCultureId(Arg.Any<string>()).Returns((GuardPool)null);

        var context = new SettlementGuardContext("town_unknown", "clan_unknown", "culture_unknown");
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.IsNull(result);
    }

    // --- Weighted selection ---

    [TestMethod]
    public void ResolveGuardTroopId_WeightedSelection_PicksHigherWeight()
    {
        var pool = MakePool(("citadel_guard", 3), ("fountain_guard", 1));
        _config.GetBySettlementId("town_EW1").Returns(pool);
        _random.Next(4).Returns(2); // roll=2, cumulative: 3 at index 0 → picks citadel_guard

        var context = new SettlementGuardContext("town_EW1", null, null);
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.AreEqual("citadel_guard", result);
    }

    [TestMethod]
    public void ResolveGuardTroopId_WeightedSelection_PicksSecondEntry()
    {
        var pool = MakePool(("citadel_guard", 3), ("fountain_guard", 1));
        _config.GetBySettlementId("town_EW1").Returns(pool);
        _random.Next(4).Returns(3); // roll=3, cumulative: 3 at index 0 (not <), 4 at index 1 → picks fountain_guard

        var context = new SettlementGuardContext("town_EW1", null, null);
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.AreEqual("fountain_guard", result);
    }

    // --- Spawn point filtering ---

    [TestMethod]
    public void ResolveGuardTroopId_SpawnPointFilter_OnlyMatchingGuards()
    {
        var guards = new List<GuardEntry>
        {
            new GuardEntry("citadel_guard", 3, new List<string> { "sp_guard_castle" }),
            new GuardEntry("fountain_guard", 2, new List<string> { "sp_guard", "sp_guard_patrol" })
        };
        var pool = new GuardPool(guards, null);
        _config.GetBySettlementId("town_EW1").Returns(pool);
        _random.Next(Arg.Any<int>()).Returns(0);

        var context = new SettlementGuardContext("town_EW1", null, null);
        var result = _sut.ResolveGuardTroopId(context, "sp_guard_castle");

        Assert.AreEqual("citadel_guard", result);
    }

    [TestMethod]
    public void ResolveGuardTroopId_SpawnPointFilter_FallsBackToAllGuards_WhenNoMatch()
    {
        var guards = new List<GuardEntry>
        {
            new GuardEntry("citadel_guard", 1, new List<string> { "sp_guard_castle" })
        };
        var pool = new GuardPool(guards, null);
        _config.GetBySettlementId("town_EW1").Returns(pool);
        _random.Next(Arg.Any<int>()).Returns(0);

        var context = new SettlementGuardContext("town_EW1", null, null);
        var result = _sut.ResolveGuardTroopId(context, "sp_guard_unarmed");

        Assert.AreEqual("citadel_guard", result);
    }

    [TestMethod]
    public void ResolveGuardTroopId_NoSpawnPointsOnGuard_MatchesAnySpawnPoint()
    {
        var guards = new List<GuardEntry>
        {
            new GuardEntry("generic_guard", 1, new List<string>())
        };
        var pool = new GuardPool(guards, null);
        _config.GetBySettlementId("town_EW1").Returns(pool);
        _random.Next(Arg.Any<int>()).Returns(0);

        var context = new SettlementGuardContext("town_EW1", null, null);
        var result = _sut.ResolveGuardTroopId(context, "sp_guard_castle");

        Assert.AreEqual("generic_guard", result);
    }

    // --- Settlement takes priority over clan ---

    [TestMethod]
    public void ResolveGuardTroopId_SettlementAndClanBothExist_SettlementWins()
    {
        var settlementPool = MakePool(("citadel_guard", 1));
        var clanPool = MakePool(("tower_guard", 1));
        _config.GetBySettlementId("town_EW1").Returns(settlementPool);
        _config.GetByClanId("clan_empire_west_1").Returns(clanPool);
        _random.Next(Arg.Any<int>()).Returns(0);

        var context = new SettlementGuardContext("town_EW1", "clan_empire_west_1", "gondor");
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.AreEqual("citadel_guard", result);
    }

    // --- Empty guards list ---

    [TestMethod]
    public void ResolveGuardTroopId_EmptyGuardsList_ReturnsNull()
    {
        var pool = new GuardPool(new List<GuardEntry>(), null);
        _config.GetBySettlementId("town_EW1").Returns(pool);

        var context = new SettlementGuardContext("town_EW1", null, null);
        var result = _sut.ResolveGuardTroopId(context, null);

        Assert.IsNull(result);
    }

    // --- Spear resolution ---

    [TestMethod]
    public void ResolveSpearItemId_KnownCulture_ReturnsItemId()
    {
        _config.GetSpearItemId("gondor").Returns("gondor_guard_spear");

        var result = _sut.ResolveSpearItemId("gondor");

        Assert.AreEqual("gondor_guard_spear", result);
    }

    [TestMethod]
    public void ResolveSpearItemId_UnknownCulture_ReturnsNull()
    {
        _config.GetSpearItemId("unknown").Returns((string)null);

        var result = _sut.ResolveSpearItemId("unknown");

        Assert.IsNull(result);
    }

    // --- Helpers ---

    private static GuardPool MakePool(params (string troopId, int weight)[] entries)
    {
        var guards = new List<GuardEntry>();
        foreach (var (troopId, weight) in entries)
            guards.Add(new GuardEntry(troopId, weight, new List<string>()));
        return new GuardPool(guards, null);
    }
}
