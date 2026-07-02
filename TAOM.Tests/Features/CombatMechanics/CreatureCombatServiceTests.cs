using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class CreatureCombatServiceTests
{
    private const float Delta = 0.0001f;

    private ICombatMechanicsConfigProvider _configProvider;
    private ICombatMechanicsSettingsProvider _settings;
    private IRaceCombatModifiersResolver _raceModifiers;
    private CombatMechanicsConfig _config;
    private CreatureCombatService _sut;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _raceModifiers = Substitute.For<IRaceCombatModifiersResolver>();

        _config = new CombatMechanicsConfig();
        _config.Creatures.CleaveMonsterIds = new List<string> { "cave_troll", "hill_troll" };
        _config.Creatures.CleaveRemainingMomentumFactor = 0.3f;
        _config.Creatures.UnstoppableDamageThresholds = new Dictionary<string, int>
        {
            ["cave_troll"] = 15,
            ["spider"] = 10,
        };
        _configProvider.GetConfig().Returns(_config);

        _settings.CreatureCleaveEnabled.Returns(true);
        _settings.CreatureUnstoppableEnabled.Returns(true);
        _raceModifiers.Resolve(Arg.Any<int?>()).Returns(RaceCombatModifiers.Neutral);

        // Config lookups are precomputed in the constructor, so the SUT is built AFTER the
        // config is composed. Settings are read per call — toggle tests re-configure the mock.
        _sut = new CreatureCombatService(_configProvider, _settings, _raceModifiers);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_ListedMonster_ScalesByFactor()
    {
        var result = _sut.CalculateCleaveMomentum("cave_troll", 10f, isColliderAgent: true);

        Assert.IsTrue(result.HasValue);
        Assert.AreEqual(3f, result.Value, Delta);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_UnlistedMonster_ReturnsNull()
    {
        var result = _sut.CalculateCleaveMomentum("taom_war_elephant", 10f, isColliderAgent: true);

        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_NotColliderAgent_ReturnsNull()
    {
        var result = _sut.CalculateCleaveMomentum("cave_troll", 10f, isColliderAgent: false);

        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_CleaveDisabled_ReturnsNull()
    {
        _settings.CreatureCleaveEnabled.Returns(false);

        var result = _sut.CalculateCleaveMomentum("cave_troll", 10f, isColliderAgent: true);

        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_SettlementFastVariant_ScalesByFactor()
    {
        // LOTRLOME settlement variants normalize to the base id (the longer suffix must win).
        var result = _sut.CalculateCleaveMomentum("cave_troll_settlement_fast", 10f, isColliderAgent: true);

        Assert.IsTrue(result.HasValue);
        Assert.AreEqual(3f, result.Value, Delta);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_NullMonsterId_ReturnsNull()
    {
        var result = _sut.CalculateCleaveMomentum(null, 10f, isColliderAgent: true);

        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_EmptyMonsterId_ReturnsNull()
    {
        var result = _sut.CalculateCleaveMomentum(string.Empty, 10f, isColliderAgent: true);

        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_ListedMonsterWithMomentumAndDamage_ReturnsTrue()
    {
        var result = _sut.ShouldForceSliceThrough("cave_troll", 1.5f, isColliderAgent: true, inflictedDamage: 20);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_ZeroMomentumRemaining_ReturnsFalse()
    {
        // Cooperation guard: when the momentum half didn't fire (momentum zeroed), the reaction
        // half must not force SlicedThrough on its own.
        var result = _sut.ShouldForceSliceThrough("cave_troll", 0f, isColliderAgent: true, inflictedDamage: 20);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_ZeroDamage_ReturnsFalse()
    {
        var result = _sut.ShouldForceSliceThrough("cave_troll", 1.5f, isColliderAgent: true, inflictedDamage: 0);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_NotColliderAgent_ReturnsFalse()
    {
        var result = _sut.ShouldForceSliceThrough("cave_troll", 1.5f, isColliderAgent: false, inflictedDamage: 20);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_CleaveDisabled_ReturnsFalse()
    {
        _settings.CreatureCleaveEnabled.Returns(false);

        var result = _sut.ShouldForceSliceThrough("cave_troll", 1.5f, isColliderAgent: true, inflictedDamage: 20);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_UnlistedMonster_ReturnsFalse()
    {
        var result = _sut.ShouldForceSliceThrough("spider", 1.5f, isColliderAgent: true, inflictedDamage: 20);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_SettlementSlowVariant_ReturnsTrue()
    {
        var result = _sut.ShouldForceSliceThrough("hill_troll_settlement_slow", 1.5f, isColliderAgent: true, inflictedDamage: 20);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_NullMonsterId_ReturnsFalse()
    {
        var result = _sut.ShouldForceSliceThrough(null, 1.5f, isColliderAgent: true, inflictedDamage: 20);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsUnstoppable_DamageEqualsThreshold_ReturnsTrue()
    {
        // Boundary: damage AT the threshold is shrugged off ("at or below").
        var result = _sut.IsUnstoppable("cave_troll", 15);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsUnstoppable_DamageAboveThreshold_ReturnsFalse()
    {
        var result = _sut.IsUnstoppable("cave_troll", 16);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsUnstoppable_UnlistedMonster_ReturnsFalse()
    {
        var result = _sut.IsUnstoppable("hill_troll", 1);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsUnstoppable_UnstoppableDisabled_ReturnsFalse()
    {
        _settings.CreatureUnstoppableEnabled.Returns(false);

        var result = _sut.IsUnstoppable("cave_troll", 5);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsUnstoppable_SettlementVariant_MapsToBaseId()
    {
        var result = _sut.IsUnstoppable("spider_settlement", 10);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsUnstoppable_NullMonsterId_ReturnsFalse()
    {
        var result = _sut.IsUnstoppable(null, 5);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ApplyStaggerThresholdMultiplier_ResolverReturnsMultiplier_MultipliesThreshold()
    {
        _raceModifiers.Resolve(3).Returns(new RaceCombatModifiers { StaggerThresholdMultiplier = 1.5f });

        var result = _sut.ApplyStaggerThresholdMultiplier(3, 100f);

        Assert.AreEqual(150f, result, Delta);
    }

    [TestMethod]
    public void ApplyStaggerThresholdMultiplier_ResolverReturnsNeutral_ReturnsBaseThreshold()
    {
        // Resolver already returns Neutral (multiplier 1) for disabled/invalid/unknown races —
        // the service adds no extra gating.
        var result = _sut.ApplyStaggerThresholdMultiplier(null, 42.5f);

        Assert.AreEqual(42.5f, result, Delta);
    }

    [TestMethod]
    public void ShouldForceSliceThrough_NaNMomentum_ReturnsFalse()
    {
        // NaN-gate regression (deep-review 2026-07-02): `momentumRemaining <= 0f` let NaN pass
        // (NaN comparisons are all false) — the guard must be the positive `> 0f` requirement.
        var result = _sut.ShouldForceSliceThrough("cave_troll", float.NaN, isColliderAgent: true, inflictedDamage: 10);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CalculateCleaveMomentum_NaNMomentum_ReturnsNull()
    {
        // A NaN momentum must fall through to the base model, never NaN×factor into the chain.
        var result = _sut.CalculateCleaveMomentum("cave_troll", float.NaN, isColliderAgent: true);

        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    public void Constructor_SuffixedConfigEntry_MatchesBaseAndVariantIds()
    {
        // Config entries are normalized then expanded, so a settlement-variant entry in the JSON
        // behaves identically to its base id (deep-review 2026-07-02 cross-service consistency).
        _config.Creatures.CleaveMonsterIds = new List<string> { "taom_mumakil_settlement" };
        var sut = new CreatureCombatService(_configProvider, _settings, _raceModifiers);

        Assert.IsTrue(sut.CalculateCleaveMomentum("taom_mumakil", 10f, isColliderAgent: true).HasValue);
        Assert.IsTrue(sut.CalculateCleaveMomentum("taom_mumakil_settlement_fast", 10f, isColliderAgent: true).HasValue);
    }
}
