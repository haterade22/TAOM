using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The daily breakdown is the one calculation the tick applies, the tooltip renders, the daily
/// message quotes and the console dumps. Before it existed the tooltip computed its own upkeep from
/// an EMPTY troop list and its own income without the career passive, so it could never agree with
/// the tick (#558). These pin the object all four consumers now read, and the desertion rule that
/// rides on it: a cost row is not an upkeep row, so a troop that costs nothing to keep never walks.
/// </summary>
[TestClass]
public class SpecialResourceBreakdownTests
{
    private ISpecialResourceConfigProvider _config;
    private ISpecialResourceStorageService _storage;
    private ICareerPassiveService _passiveService;
    private SpecialResourceService _service;

    private static readonly SpecialResource MordorResource = new(
        id: "war_spoils",
        kingdomIds: new[] { "empire_s" },
        cultureIds: new[] { "mordor" },
        displayName: "War Spoils",
        iconSpriteName: "taom_war_spoils_icon",
        cap: 500f,
        startingAmount: 0f,
        dailyPerTown: 0.5f,
        perBattleVictoryBase: 10f,
        perRaid: 8f,
        perSiegeVictory: 15f,
        perPrisoner: 1f);

    private static readonly List<TroopUpkeepInfo> NoTroops = new();

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _service = new SpecialResourceService(_config, _storage, Substitute.For<IModLogger>(), _passiveService);

        _config.GetByKingdomId("empire_s").Returns(MordorResource);
        _config.GetByCultureId("mordor").Returns(MordorResource);
    }

    private void CostRow(string troopId, float dailyUpkeep, int upgradeCost = 2, int recruitCost = 0, int merchantCost = 0)
    {
        _config.GetTroopCost(troopId).Returns(
            new TroopResourceCostEntry(troopId, "war_spoils", upgradeCost, dailyUpkeep, recruitCost, merchantCost));
    }

    // ── GetDailyBreakdown ──

    [TestMethod]
    public void GetDailyBreakdown_NoResource_ReturnsEmpty()
    {
        var breakdown = _service.GetDailyBreakdown("hero1", "nonexistent_kingdom", null, 4, NoTroops);

        Assert.AreEqual(0f, breakdown.Earning);
        Assert.AreEqual(0f, breakdown.Upkeep);
        Assert.AreEqual(0f, breakdown.Net);
        Assert.AreEqual(0, breakdown.UpkeepLines.Count);
    }

    [TestMethod]
    public void GetDailyBreakdown_ExcludesTroopsWithoutUpkeep_FromLines()
    {
        // Three kinds of troop share a party: one that costs upkeep, one the Elite Emissary sells
        // (merchant_cost only, no daily_upkeep, the 50 rows that shipped with that feature) and one
        // with no cost row at all. Only the first is an upkeep line.
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        CostRow("erebor_noble_royal_warden", dailyUpkeep: 0f, upgradeCost: 0, merchantCost: 28);
        _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);
        var troops = new List<TroopUpkeepInfo>
        {
            new("mordor_uruk_darkblade", 10),
            new("erebor_noble_royal_warden", 12),
            new("plain_troop", 40),
        };

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 0, troops);

        Assert.AreEqual(1, breakdown.UpkeepLines.Count);
        Assert.AreEqual("mordor_uruk_darkblade", breakdown.UpkeepLines[0].TroopId);
        Assert.AreEqual(3.0f, breakdown.Upkeep, 0.001f);
    }

    [TestMethod]
    public void GetDailyBreakdown_UpkeepLine_CarriesCountPerUnitAndTotal()
    {
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 10) };

        var line = _service.GetDailyBreakdown("hero1", "empire_s", null, 0, troops).UpkeepLines[0];

        Assert.AreEqual(10, line.Count);
        Assert.AreEqual(0.3f, line.PerUnit, 0.001f);
        Assert.AreEqual(3.0f, line.Total, 0.001f);
    }

    [TestMethod]
    public void GetDailyBreakdown_UpkeepModifier_AppliesToEachLine_AndLinesSumToUpkeep()
    {
        // The tooltip shows the lines and the total side by side; a modifier applied to the total
        // alone would make the lines add up to a different number than the one under them.
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(-0.25f);
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        CostRow("mordor_uruk_captain", dailyUpkeep: 0.2f);
        var troops = new List<TroopUpkeepInfo>
        {
            new("mordor_uruk_darkblade", 10),   // 3.0 → 2.25
            new("mordor_uruk_captain", 5),      // 1.0 → 0.75
        };

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 0, troops);

        Assert.AreEqual(2.25f, breakdown.UpkeepLines[0].Total, 0.001f);
        Assert.AreEqual(0.75f, breakdown.UpkeepLines[1].Total, 0.001f);
        var sum = 0f;
        foreach (var line in breakdown.UpkeepLines) sum += line.Total;
        Assert.AreEqual(sum, breakdown.Upkeep, 0.001f);
        Assert.AreEqual(3.0f, breakdown.Upkeep, 0.001f);
    }

    [TestMethod]
    public void GetDailyBreakdown_UpkeepModifierBelowMinusOneHundredPercent_ClampsLinesAndTotalToZero()
    {
        // A relief past -100% must not turn upkeep into income: every line and the total floor at 0,
        // exactly as the old single-total Math.Max did.
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(-1.5f);
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        CostRow("mordor_uruk_captain", dailyUpkeep: 0.2f);
        var troops = new List<TroopUpkeepInfo>
        {
            new("mordor_uruk_darkblade", 10),
            new("mordor_uruk_captain", 5),
        };

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 2, troops);

        Assert.AreEqual(2, breakdown.UpkeepLines.Count);
        foreach (var line in breakdown.UpkeepLines)
        {
            Assert.AreEqual(0f, line.PerUnit);
            Assert.AreEqual(0f, line.Total);
        }
        Assert.AreEqual(0f, breakdown.Upkeep);
        Assert.AreEqual(1.0f, breakdown.Net, 0.001f);   // 2 towns * 0.5 income, nothing subtracted
    }

    [TestMethod]
    public void GetDailyBreakdown_CareerGain_ScalesEarning()
    {
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceGain).Returns(0.2f);

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 4, NoTroops);

        // 4 towns * 0.5 = 2.0, +20% = 2.4. The old tooltip path skipped the passive.
        Assert.AreEqual(2.4f, breakdown.Earning, 0.001f);
    }

    [TestMethod]
    public void GetDailyBreakdown_Net_MatchesGetProjectedDailyNet()
    {
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceGain).Returns(0.2f);
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(-0.5f);
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 10) };

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 4, troops);
        var projected = _service.GetProjectedDailyNet("hero1", "empire_s", null, 4, troops);

        // An independent expected value, not only the two reads against each other: 4 towns * 0.5
        // = 2.0, +20% = 2.4 earning; 10 * 0.3 = 3.0, -50% = 1.5 upkeep; net 0.9 (Codex, review 95).
        Assert.AreEqual(0.9f, breakdown.Net, 0.001f);
        Assert.AreEqual(projected, breakdown.Net, 0.0001f);
        Assert.AreEqual(breakdown.Earning - breakdown.Upkeep, breakdown.Net, 0.0001f);
    }

    // ── DaysUntilDepleted ──

    [TestMethod]
    public void DaysUntilDepleted_NegativeNet_CeilsBalanceOverDailyLoss()
    {
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 15) };   // -4.5/day

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 0, troops);

        Assert.AreEqual(3, breakdown.DaysUntilDepleted(10f));   // 10 / 4.5 = 2.2 → 3
        Assert.AreEqual(2, breakdown.DaysUntilDepleted(9f));    // exactly 2
    }

    [TestMethod]
    public void DaysUntilDepleted_ZeroBalance_ReturnsNull()
    {
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 15) };

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 0, troops);

        Assert.IsNull(breakdown.DaysUntilDepleted(0f));
    }

    [TestMethod]
    public void DaysUntilDepleted_PositiveNet_ReturnsNull()
    {
        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 4, NoTroops);

        Assert.IsTrue(breakdown.Net > 0f);
        Assert.IsNull(breakdown.DaysUntilDepleted(10f));
    }

    [TestMethod]
    public void DaysUntilDepleted_ZeroNet_ReturnsNull()
    {
        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 0, NoTroops);

        Assert.AreEqual(0f, breakdown.Net);
        Assert.IsNull(breakdown.DaysUntilDepleted(10f));
    }

    [TestMethod]
    public void DaysUntilDepleted_NetBelowTheBalanceFloatResolution_ReturnsNull()
    {
        // Codex (review 95) reproduced this on the CLR with shipped data: a Dale player with one town
        // (+0.7) and two captains plus one Bara-dur guard (0.2 * 2 + 0.3) has a float net of about
        // -6e-8. The stored balance never moves, yet ceil(1000 / 6e-8) is 1.7e10 and the unchecked
        // cast made the tooltip say "Depleted in -2147483648 days". No countdown when the next tick
        // would not lower the stored float, and no cast without a range check.
        var dale = new SpecialResource(
            id: "lake_fish", kingdomIds: new[] { "sturgia" }, cultureIds: new[] { "sturgia" },
            displayName: "Lake Fish", iconSpriteName: "taom_lake_fish_icon", cap: 10000f, startingAmount: 0f,
            dailyPerTown: 0.7f, perBattleVictoryBase: 7f, perRaid: 5f, perSiegeVictory: 10f, perPrisoner: 1f);
        _config.GetByKingdomId("sturgia").Returns(dale);
        CostRow("mordor_uruk_captain", dailyUpkeep: 0.2f);
        CostRow("mordor_uruk_baraddurguard", dailyUpkeep: 0.3f);
        var troops = new List<TroopUpkeepInfo>
        {
            new("mordor_uruk_captain", 2),
            new("mordor_uruk_baraddurguard", 1),
        };

        var breakdown = _service.GetDailyBreakdown("hero1", "sturgia", null, 1, troops);

        Assert.IsTrue(breakdown.Net < 0f, $"the fixture must produce a tiny negative net, got {breakdown.Net:R}");
        Assert.AreEqual(1000f, 1000f + breakdown.Net, "the fixture must not move the stored float");
        Assert.IsNull(breakdown.DaysUntilDepleted(1000f));
    }

    // ── Desertion only for troops that actually cost upkeep (#558 finding 5) ──

    [TestMethod]
    public void CalculateDesertion_MerchantOnlyTroop_AtZeroBalance_DoesNotDesert()
    {
        // erebor_noble_royal_warden is a regular upgrade target that also carries an Elite Emissary
        // merchant_cost. At 0 Gems it used to lose 10% a day to "your Gems are depleted" while
        // costing nothing to keep.
        _storage.Get("hero1", "war_spoils").Returns(0f);
        CostRow("erebor_noble_royal_warden", dailyUpkeep: 0f, upgradeCost: 0, merchantCost: 28);
        var troops = new List<TroopUpkeepInfo> { new("erebor_noble_royal_warden", 12) };

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_TroopWithoutCostRow_AtZeroBalance_DoesNotDesert()
    {
        _storage.Get("hero1", "war_spoils").Returns(0f);
        _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);
        var troops = new List<TroopUpkeepInfo> { new("plain_troop", 12) };

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_MixedParty_DesertsOnlyUpkeepTroops()
    {
        _storage.Get("hero1", "war_spoils").Returns(0f);
        CostRow("mordor_uruk_darkblade", dailyUpkeep: 0.3f);
        CostRow("erebor_noble_royal_warden", dailyUpkeep: 0f, upgradeCost: 0, merchantCost: 28);
        var troops = new List<TroopUpkeepInfo>
        {
            new("erebor_noble_royal_warden", 12),
            new("mordor_uruk_darkblade", 10),
        };

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mordor_uruk_darkblade", result[0].TroopId);
        Assert.AreEqual(1, result[0].DesertCount);
    }

    // ── Spend paths report what they debited, so the behavior can say it to the player ──

    [TestMethod]
    public void CommitSession_PendingSpend_ReturnsAmountDebited()
    {
        // Real storage: the return value is the measured debit, so a substitute whose Get returns 0
        // would read as "nothing left the wallet" (Codex, review 95).
        var storage = new SpecialResourceStorageService();
        var service = new SpecialResourceService(_config, storage, Substitute.For<IModLogger>(), _passiveService);
        storage.Set("hero1", "war_spoils", 100f);
        CostRow("mordor_uruk_captain", dailyUpkeep: 0.2f, upgradeCost: 4);
        service.BeginPartyScreenSession();
        service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);

        var spent = service.CommitSession("hero1", "empire_s", null);

        Assert.AreEqual(12f, spent, 0.001f);
        Assert.AreEqual(88f, storage.Get("hero1", "war_spoils"), 0.001f);
    }

    [TestMethod]
    public void CommitSession_NothingPending_ReturnsZero()
    {
        _service.BeginPartyScreenSession();

        Assert.AreEqual(0f, _service.CommitSession("hero1", "empire_s", null));
    }

    [TestMethod]
    public void CommitSession_NotInSession_ReturnsZero()
    {
        Assert.AreEqual(0f, _service.CommitSession("hero1", "empire_s", null));
    }

    [TestMethod]
    public void ChargeRecruitCost_TroopWithRecruitCost_ReturnsAmountCharged()
    {
        var storage = new SpecialResourceStorageService();
        var service = new SpecialResourceService(_config, storage, Substitute.For<IModLogger>(), _passiveService);
        storage.Set("hero1", "war_spoils", 250f);
        CostRow("harad_elephant_rider", dailyUpkeep: 10f, upgradeCost: 0, recruitCost: 50);

        var charged = service.ChargeRecruitCost("hero1", "empire_s", null, "harad_elephant_rider", 2);

        Assert.AreEqual(100f, charged, 0.001f);
        Assert.AreEqual(150f, storage.Get("hero1", "war_spoils"), 0.001f);
    }

    // Real storage below: the substitute never floors, so a return-value test against it can only
    // prove what was REQUESTED, never what left the wallet (Codex, review 95, F1).

    [TestMethod]
    public void ChargeRecruitCost_BalanceBelowCost_ReturnsOnlyTheAmountDebited()
    {
        // A captured spider recruited in the party screen: Patch51 gates the volunteer screen only, so
        // the charge lands on a balance of 2 for a cost of 5 and the storage floors at 0. The toast
        // must say -2, not -5.
        var storage = new SpecialResourceStorageService();
        var service = new SpecialResourceService(_config, storage, Substitute.For<IModLogger>(), _passiveService);
        storage.Set("hero1", "war_spoils", 2f);
        CostRow("taom_spider_creature", dailyUpkeep: 1f, upgradeCost: 0, recruitCost: 5);

        var debited = service.ChargeRecruitCost("hero1", "empire_s", null, "taom_spider_creature", 1);

        Assert.AreEqual(2f, debited, 0.001f);
        Assert.AreEqual(0f, storage.Get("hero1", "war_spoils"));
    }

    [TestMethod]
    public void CommitSession_BalanceFellBelowPending_ReturnsOnlyTheAmountDebited()
    {
        // Ten affordable upgrades queued at balance 10, then a prisoner recruit inside the same party
        // screen takes 5 before Done commits the queue: 5 leaves the wallet, not 10.
        var storage = new SpecialResourceStorageService();
        var service = new SpecialResourceService(_config, storage, Substitute.For<IModLogger>(), _passiveService);
        storage.Set("hero1", "war_spoils", 10f);
        CostRow("mordor_uruk_captain", dailyUpkeep: 0.2f, upgradeCost: 1);
        service.BeginPartyScreenSession();
        service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 10);
        storage.Add("hero1", "war_spoils", -5f);

        var debited = service.CommitSession("hero1", "empire_s", null);

        Assert.AreEqual(5f, debited, 0.001f);
        Assert.AreEqual(0f, storage.Get("hero1", "war_spoils"));
    }

    [TestMethod]
    public void ChargeRecruitCost_TroopWithoutRecruitCost_ReturnsZero()
    {
        CostRow("mordor_uruk_captain", dailyUpkeep: 0.2f, upgradeCost: 4);

        Assert.AreEqual(0f, _service.ChargeRecruitCost("hero1", "empire_s", null, "mordor_uruk_captain", 2));
        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }
}
