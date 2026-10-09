using System;
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
/// #767: the SpecialResourceGain career passive scaled only the daily town income; every other
/// earning path ignored it. And desertion judged a troop by its configured upkeep, so a pick that cut
/// the upkeep to zero still let the troop walk. These pin the one scaling helper and the effective
/// per-unit upkeep that desertion now reads.
/// </summary>
[TestClass]
public class SpecialResourceGainTests
{
    private ISpecialResourceConfigProvider _config;
    private ISpecialResourceStorageService _storage;
    private ICareerPassiveService _passiveService;
    private SpecialResourceService _service;

    private static readonly SpecialResource Resource = new(
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
        perPrisoner: 1f,
        perTournamentWin: 6f,
        perHideoutClear: 4f);

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _service = new SpecialResourceService(_config, _storage, Substitute.For<IModLogger>(), _passiveService);

        _config.GetByKingdomId("empire_s").Returns(Resource);
        _storage.Get("hero1", "war_spoils").Returns(0f);
    }

    private void Gain(float magnitude)
        => _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceGain).Returns(magnitude);

    private void UpkeepModifier(float magnitude)
        => _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(magnitude);

    private static readonly (string Name, Action<SpecialResourceService> Earn, float Base)[] Paths =
    {
        ("battle", s => s.EarnFromBattle("hero1", "empire_s", null, 1f), 10f),
        ("raid", s => s.EarnFromRaid("hero1", "empire_s", null), 8f),
        ("siege", s => s.EarnFromSiege("hero1", "empire_s", null), 15f),
        ("prisoners", s => s.EarnFromPrisoners("hero1", "empire_s", null, 4), 4f),
        ("tournament", s => s.EarnFromTournament("hero1", "empire_s", null), 6f),
        ("hideout", s => s.EarnFromHideout("hero1", "empire_s", null), 4f),
    };

    [TestMethod]
    public void EarnPaths_Gain_ScalesEverySource()
    {
        Gain(0.5f);
        foreach (var (name, earn, baseAmount) in Paths)
        {
            _storage.ClearReceivedCalls();
            earn(_service);
            try
            {
                _storage.Received(1).Set("hero1", "war_spoils", baseAmount * 1.5f);
            }
            catch (Exception ex)
            {
                Assert.Fail($"earning path '{name}' did not store base x1.5: {ex.Message}");
            }
        }
    }

    [TestMethod]
    public void EarnPaths_ZeroGain_LeavesAmountExact()
    {
        Gain(0f);
        foreach (var (_, earn, baseAmount) in Paths)
        {
            _storage.ClearReceivedCalls();
            earn(_service);
            _storage.Received(1).Set("hero1", "war_spoils", baseAmount);
        }
    }

    [TestMethod]
    public void EarnPaths_GainBelowMinusOne_EarnsNothing()
    {
        Gain(-2f);
        foreach (var (_, earn, _) in Paths)
        {
            _storage.ClearReceivedCalls();
            earn(_service);
            _storage.Received(1).Set("hero1", "war_spoils", 0f);
        }
    }

    [TestMethod]
    public void EarnPaths_NaNGain_EarnsNothing()
    {
        Gain(float.NaN);
        foreach (var (_, earn, _) in Paths)
        {
            _storage.ClearReceivedCalls();
            earn(_service);
            _storage.Received(1).Set("hero1", "war_spoils", 0f);
        }
    }

    [TestMethod]
    public void DailyBreakdown_NaNGain_EarnsNothing()
    {
        Gain(float.NaN);

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 2, new List<TroopUpkeepInfo>());

        Assert.AreEqual(0f, breakdown.Earning);
    }

    [TestMethod]
    public void DailyBreakdown_GainBelowMinusOne_EarnsNothingAndDoesNotDebit()
    {
        Gain(-2f);

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 2, new List<TroopUpkeepInfo>());

        Assert.AreEqual(0f, breakdown.Earning);
        Assert.AreEqual(0f, breakdown.Net);
    }

    // ── Public projection (#767) ──

    [TestMethod]
    public void ScaleEarned_PositiveGain_MultipliesTheBase()
    {
        Gain(0.5f);
        Assert.AreEqual(15f, _service.ScaleEarned("hero1", 10f), 0.0001f);
    }

    [TestMethod]
    public void ScaleEarned_NoGain_ReturnsTheBase()
    {
        Gain(0f);
        Assert.AreEqual(10f, _service.ScaleEarned("hero1", 10f));
    }

    [TestMethod]
    public void ScaleEarned_GainBelowMinusOne_IsZero()
    {
        Gain(-2f);
        Assert.AreEqual(0f, _service.ScaleEarned("hero1", 10f));
    }

    [TestMethod]
    public void ScaleEarned_NaNGain_IsZero()
    {
        Gain(float.NaN);
        Assert.AreEqual(0f, _service.ScaleEarned("hero1", 10f));
    }

    [TestMethod]
    public void ScaleEarned_ProjectionOfEachPathBase_EqualsWhatThePathStores()
    {
        Gain(0.35f);
        foreach (var (name, earn, baseAmount) in Paths)
        {
            _storage.ClearReceivedCalls();
            earn(_service);
            var projected = _service.ScaleEarned("hero1", baseAmount);
            try
            {
                _storage.Received(1).Set("hero1", "war_spoils", projected);
            }
            catch (Exception ex)
            {
                Assert.Fail($"projection for '{name}' ({projected}) differs from the stored value: {ex.Message}");
            }
        }
    }

    // ── Desertion reads the effective upkeep ──

    private IReadOnlyList<TroopUpkeepInfo> UpkeepTroop()
    {
        _config.GetTroopCost("mordor_uruk_darkblade").Returns(
            new TroopResourceCostEntry("mordor_uruk_darkblade", "war_spoils", 2, 0.3f, 0, 0));
        return new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 10) };
    }

    [TestMethod]
    public void CalculateDesertion_UpkeepCutToZero_DoesNotDesert()
    {
        UpkeepModifier(-1f);

        Assert.AreEqual(0, _service.CalculateDesertion("hero1", "empire_s", null, UpkeepTroop()).Count);
    }

    [TestMethod]
    public void CalculateDesertion_UpkeepPartlyCut_StillDeserts()
    {
        UpkeepModifier(-0.5f);

        Assert.AreEqual(1, _service.CalculateDesertion("hero1", "empire_s", null, UpkeepTroop()).Count);
    }

    [TestMethod]
    public void CalculateDesertion_NoModifier_StillDeserts()
    {
        Assert.AreEqual(1, _service.CalculateDesertion("hero1", "empire_s", null, UpkeepTroop()).Count);
    }

    [TestMethod]
    public void CalculateDesertion_NaNModifier_IsTreatedAsNoModifier()
    {
        UpkeepModifier(float.NaN);

        Assert.AreEqual(1, _service.CalculateDesertion("hero1", "empire_s", null, UpkeepTroop()).Count);
    }

    // ── HasUpkeepDue ──

    [TestMethod]
    public void HasUpkeepDue_AllZeroLines_IsFalse()
    {
        var breakdown = new DailyResourceBreakdown(1f, new[] { new TroopUpkeepLine("a", 5, 0f, 0f) });

        Assert.AreEqual(1, breakdown.UpkeepLines.Count);
        Assert.IsFalse(breakdown.HasUpkeepDue);
    }

    [TestMethod]
    public void HasUpkeepDue_NoLines_IsFalse()
        => Assert.IsFalse(DailyResourceBreakdown.Empty.HasUpkeepDue);

    [TestMethod]
    public void HasUpkeepDue_PositiveLine_IsTrue()
    {
        var breakdown = new DailyResourceBreakdown(0f, new[] { new TroopUpkeepLine("a", 5, 0.2f, 1f) });

        Assert.IsTrue(breakdown.HasUpkeepDue);
    }

    [TestMethod]
    public void HasUpkeepDue_NaNUpkeep_IsFalse()
    {
        var breakdown = new DailyResourceBreakdown(0f, new[] { new TroopUpkeepLine("a", 5, 0.2f, float.NaN) });

        Assert.IsFalse(breakdown.HasUpkeepDue);
    }

    // ── One upkeep computation: a non-finite modifier charges the configured upkeep ──

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void GetDailyBreakdown_NonFiniteUpkeepModifier_ChargesConfiguredUpkeep(float modifier)
    {
        UpkeepModifier(modifier);

        var breakdown = _service.GetDailyBreakdown("hero1", "empire_s", null, 0, UpkeepTroop());

        Assert.AreEqual(0.3f * 10, breakdown.Upkeep, 0.0001f);
        Assert.IsTrue(breakdown.HasUpkeepDue);
        Assert.IsFalse(float.IsNaN(breakdown.Net) || float.IsInfinity(breakdown.Net));
    }

    [TestMethod]
    public void ApplyDailyTick_NaNUpkeepModifier_StoresAFiniteBalance()
    {
        UpkeepModifier(float.NaN);
        _storage.Get("hero1", "war_spoils").Returns(100f);

        _service.ApplyDailyTick("hero1", "empire_s", null, 0, UpkeepTroop());

        _storage.Received(1).Add("hero1", "war_spoils", Arg.Is<float>(v => !float.IsNaN(v) && !float.IsInfinity(v) && v < 0f));
    }

    [TestMethod]
    public void CalculateDesertion_NegativeConfiguredUpkeepWithModifierBelowMinusOne_DoesNotDesert()
    {
        _config.GetTroopCost("odd_troop").Returns(
            new TroopResourceCostEntry("odd_troop", "war_spoils", 2, -0.3f, 0, 0));
        UpkeepModifier(-2f);

        var result = _service.CalculateDesertion("hero1", "empire_s", null, new List<TroopUpkeepInfo> { new("odd_troop", 10) });

        Assert.AreEqual(0, result.Count);
    }
}
