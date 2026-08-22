using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// Theater weighting, the border-rescue range, and the priority-index safety properties.
///
/// <para><b>There is no distance term in the score, and that is the point.</b> Vanilla's besieger
/// factor is <c>MBMath.Map((5G-d)/G, 0f, 5f, 0.9f, 10f)</c>, which already ramps 10.0 at zero
/// distance down to 0.9 at five town gaps, and <c>CalculateDistanceScoreForBesieging</c> hard-zeroes
/// anything under 0.1 topology score. A TAOM falloff was built and then removed on 2026-08-22:
/// measured end to end it moved the crossover between a max-boost far target and a near neutral one
/// from 4.029 to 3.746 town gaps, 0.283 gaps, in exchange for a hot-path adapter, a three-way cache,
/// and a path where suppressing a committed target pushed <c>Army.ThinkAboutCohesionBoost</c> under
/// its 0.01f gate and disbanded the army.</para>
///
/// <para>The only distance decision TAOM still owns is whether Patch22 may overturn vanilla's
/// unreachable verdict for an authored priority target, bounded by its own radius.</para>
/// </summary>
[TestClass]
public class WarTheaterAndReachTests
{
    private IArmyTargetingSettingsProvider _settings;
    private IArmyTargetingConfigProvider _configProvider;
    private IModLogger _logger;
    private ArmyTargetingConfig _config;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IArmyTargetingSettingsProvider>();
        _configProvider = Substitute.For<IArmyTargetingConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _settings.EnableArmyStrategicIntelligence.Returns(true);
        _settings.EnableWarTheaters.Returns(true);
        _settings.CommitmentMultiplier.Returns(4.0f);
        _settings.MaxPriorityBoost.Returns(3.0f);
        _settings.EvilAggressionScale.Returns(1.0f);
        _settings.BorderProximityFloor.Returns(0.15f);
        _settings.BorderRescueRadiusInTownGaps.Returns(3.2f);
        _settings.DefenderPriorityMultiplier.Returns(1.6f);

        _config = new ArmyTargetingConfig
        {
            Theaters = new List<string> { "north", "central", "south", "east" },
            KingdomTheaters = new Dictionary<string, List<string>>
            {
                // Trimmed slice of the shipped table. empire_w is GONDOR, vlandia is Rohan,
                // empire_s is Mordor, empire is Dunland.
                ["empire_w"] = new List<string> { "south", "central" },
                ["empire_s"] = new List<string> { "south", "east", "central" },
                ["vlandia"] = new List<string> { "central" },
                ["gundabad"] = new List<string> { "north" },
                ["erebor"] = new List<string> { "north", "east" },
                ["bluecraig"] = new List<string>(),
            },
            FactionPriorityTargets = new Dictionary<string, List<string>>(),
            FactionAggressionMultipliers = new Dictionary<string, float>(),
        };
        _configProvider.GetConfig().Returns(_config);
    }

    private ArmyTargetingService CreateSut() => new ArmyTargetingService(_settings, _configProvider, _logger);

    private static TargetScoreContext Ctx(
        float baseScore = 100f,
        ArmyTargetingMission mission = ArmyTargetingMission.Besieger,
        string faction = "empire_w",
        string targetFaction = "empire_s",
        string target = "town_ES1",
        string committed = null) =>
        new TargetScoreContext
        {
            BaseScore = baseScore,
            Mission = mission,
            FactionId = faction,
            TargetFactionId = targetFaction,
            TargetSettlementId = target,
            CommittedTargetId = committed,
        };

    // ---------------------------------------------------------------- border rescue range

    [TestMethod]
    public void IsWithinBorderRescueRange_NearTarget_IsTrue()
    {
        Assert.IsTrue(CreateSut().IsWithinBorderRescueRange(1.0f));
    }

    [TestMethod]
    public void IsWithinBorderRescueRange_AtTheWidestRealFront_IsTrue()
    {
        // Lothlorien to Gundabad measures 3.08 town gaps against the engine's own path cache, and
        // is the widest genuine hostile front on the map. The 3.2 radius exists to clear it.
        Assert.IsTrue(CreateSut().IsWithinBorderRescueRange(3.08f));
    }

    [TestMethod]
    public void IsWithinBorderRescueRange_BeyondRadius_IsFalse()
    {
        Assert.IsFalse(CreateSut().IsWithinBorderRescueRange(5.0f));
    }

    [TestMethod]
    public void IsWithinBorderRescueRange_NaN_IsFalse()
    {
        // This gate decides whether TAOM may OVERTURN vanilla's unreachable verdict. Vanilla has
        // already said no, and an unmeasurable distance is not grounds to overrule it.
        Assert.IsFalse(CreateSut().IsWithinBorderRescueRange(float.NaN));
    }

    [TestMethod]
    public void IsWithinBorderRescueRange_Infinity_IsFalse()
    {
        Assert.IsFalse(CreateSut().IsWithinBorderRescueRange(float.PositiveInfinity));
    }

    [TestMethod]
    public void IsWithinBorderRescueRange_FeatureDisabled_IsTrue()
    {
        _settings.EnableArmyStrategicIntelligence.Returns(false);
        Assert.IsTrue(CreateSut().IsWithinBorderRescueRange(float.NaN));
    }

    [TestMethod]
    public void IsWithinBorderRescueRange_NaNRadiusSetting_FallsBackToConfigRadius()
    {
        _settings.BorderRescueRadiusInTownGaps.Returns(float.NaN);
        var sut = CreateSut();
        Assert.IsTrue(sut.IsWithinBorderRescueRange(3.0f));
        Assert.IsFalse(sut.IsWithinBorderRescueRange(5.0f));
    }

    // ---------------------------------------------------------------- priority index safety

    [TestMethod]
    public void GetTargetMultiplier_DuplicatePriorityIds_NeverProducesANegativeBoost()
    {
        // The service builds its index defensively, independent of the config provider, because it
        // can be constructed from a config that never went through Validate. Writing raw list
        // positions into a dictionary lets a duplicate collapse two entries while the surviving
        // index keeps climbing, so Count-1 no longer equals the maximum index and the boost formula
        // computes t > 1. ["A","A","B"] gave B a boost of -1.0, flipping a positive siege score
        // negative and making the AI actively prefer what it should avoid.
        _config.FactionPriorityTargets["empire_s"] = new List<string> { "town_A", "town_A", "town_B" };
        var sut = CreateSut();

        foreach (var id in new[] { "town_A", "town_B" })
        {
            float m = sut.GetTargetMultiplier(id, null, "empire_s");
            Assert.IsTrue(m >= 1.0f, $"{id} scored {m}; a priority entry must never be penalised");
        }
    }

    [TestMethod]
    public void GetTargetMultiplier_NullAndBlankPriorityIds_DoNotThrow()
    {
        // A null id reaching Dictionary[key] throws while the model is being registered.
        _config.FactionPriorityTargets["empire_s"] = new List<string> { null, "  ", "town_EW3" };
        var sut = CreateSut();

        Assert.AreEqual(3.0f, sut.GetTargetMultiplier("town_EW3", null, "empire_s"), 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_LongPriorityList_StaysWithinTheDeclaredBoostRange()
    {
        _config.FactionPriorityTargets["empire_s"] =
            new List<string> { "t1", "t2", "t3", "t4", "t5", "t6", "t7", "t8" };
        var sut = CreateSut();

        for (int i = 1; i <= 8; i++)
        {
            float m = sut.GetTargetMultiplier("t" + i, null, "empire_s");
            Assert.IsTrue(m >= 1.0f && m <= 3.0f, $"t{i} boost {m} left the [1,3] range");
        }
    }

    // ---------------------------------------------------------------- aggression multipliers

    [TestMethod]
    public void GetStrengthMultiplier_InfiniteAggression_IsDroppedToNeutral()
    {
        // An infinity here makes the inflated ourStrength infinite, which defeats vanilla's
        // `ourStrength < defenderStrength * 2` siege veto for every fortress on the map. Json.NET
        // parses 1e39, "Infinity" and a bare Infinity token into float.PositiveInfinity.
        _config.FactionAggressionMultipliers["empire_s"] = float.PositiveInfinity;
        Assert.AreEqual(1.0f, CreateSut().GetStrengthMultiplier("empire_s"), 0.0001f);
    }

    [TestMethod]
    public void GetStrengthMultiplier_NaNAggression_IsDroppedToNeutral()
    {
        _config.FactionAggressionMultipliers["empire_s"] = float.NaN;
        Assert.AreEqual(1.0f, CreateSut().GetStrengthMultiplier("empire_s"), 0.0001f);
    }

    [TestMethod]
    public void GetStrengthMultiplier_AbsurdlyLargeAggression_IsDroppedToNeutral()
    {
        _config.FactionAggressionMultipliers["empire_s"] = 1e30f;
        Assert.AreEqual(1.0f, CreateSut().GetStrengthMultiplier("empire_s"), 0.0001f);
    }

    [TestMethod]
    public void GetEffectiveStrength_InfiniteAggression_StaysFinite()
    {
        _config.FactionAggressionMultipliers["empire_s"] = float.PositiveInfinity;
        float result = CreateSut().GetEffectiveStrength("empire_s", isBesieger: true, ourStrength: 500f);
        Assert.AreEqual(500f, result, 0.01f);
        Assert.IsFalse(float.IsInfinity(result));
    }

    // ---------------------------------------------------------------- theater weighting

    [TestMethod]
    public void GetTheaterWeight_TargetInAttackersPrimaryTheater_ReturnsPrimaryWeight()
    {
        // Gondor's primary front is south; Mordor is in south.
        Assert.AreEqual(_config.PrimaryTheaterWeight,
            CreateSut().GetTheaterWeight("empire_w", "empire_s"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_SharedButNotPrimary_ReturnsSecondaryWeight()
    {
        // Gondor is [south, central]; Rohan is [central] only. Shared, but not Gondor's primary.
        Assert.AreEqual(_config.SecondaryTheaterWeight,
            CreateSut().GetTheaterWeight("empire_w", "vlandia"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_NoSharedTheater_ReturnsForeignWeight()
    {
        Assert.AreEqual(_config.ForeignTheaterWeight,
            CreateSut().GetTheaterWeight("empire_w", "gundabad"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_ForeignIsDampedNotVetoed()
    {
        Assert.IsTrue(CreateSut().GetTheaterWeight("empire_w", "gundabad") > 0f,
            "a zero here would strand a kingdom whose enemies are all foreign and get its army disbanded for inactivity");
    }

    [TestMethod]
    public void GetTheaterWeight_PlayerFoundedKingdom_IsNeutral()
    {
        // Kingdom.CreateKingdom hands a player-founded realm the runtime StringId "new_kingdom",
        // which cannot appear in any shipped config. Failing closed would silently make the
        // player's own kingdom un-besiegeable.
        var sut = CreateSut();
        Assert.AreEqual(1.0f, sut.GetTheaterWeight("new_kingdom", "empire_s"), 0.0001f);
        Assert.AreEqual(1.0f, sut.GetTheaterWeight("empire_w", "new_kingdom"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_RebelAndPlayerFactionIds_AreNeutral()
    {
        var sut = CreateSut();
        Assert.AreEqual(1.0f, sut.GetTheaterWeight("town_EW3_rebel_clan", "empire_s"), 0.0001f);
        Assert.AreEqual(1.0f, sut.GetTheaterWeight("player_faction", "gundabad"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_PassiveKingdomWithEmptyList_IsNeutral()
    {
        // bluecraig sits on a closed land-navigation island and can reach nothing. An empty list
        // records that honestly rather than implying a capability it does not have.
        var sut = CreateSut();
        Assert.AreEqual(1.0f, sut.GetTheaterWeight("bluecraig", "erebor"), 0.0001f);
        Assert.AreEqual(1.0f, sut.GetTheaterWeight("erebor", "bluecraig"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_NullIds_AreNeutral()
    {
        var sut = CreateSut();
        Assert.AreEqual(1.0f, sut.GetTheaterWeight(null, "empire_s"), 0.0001f);
        Assert.AreEqual(1.0f, sut.GetTheaterWeight("empire_w", null), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_DuplicateEntriesInAListDoNotChangeTheVerdict()
    {
        _config.KingdomTheaters["empire_w"] = new List<string> { "south", "south", "central" };
        Assert.AreEqual(_config.PrimaryTheaterWeight,
            CreateSut().GetTheaterWeight("empire_w", "empire_s"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_TheatersDisabled_ReturnsUnity()
    {
        _settings.EnableWarTheaters.Returns(false);
        Assert.AreEqual(1.0f, CreateSut().GetTheaterWeight("empire_w", "gundabad"), 0.0001f);
    }

    [TestMethod]
    public void GetTheaterWeight_MasterSwitchDisabled_ReturnsUnity()
    {
        _settings.EnableArmyStrategicIntelligence.Returns(false);
        Assert.AreEqual(1.0f, CreateSut().GetTheaterWeight("empire_w", "gundabad"), 0.0001f);
    }

    // ---------------------------------------------------------------- ApplyTargetScoreModifiers

    [TestMethod]
    public void ApplyTargetScoreModifiers_NaNBaseScore_ReturnsItUnmodified()
    {
        // The previous gate read `baseScore <= 0f`. NaN <= 0f is false, so a NaN fell straight
        // into the multiply chain instead of deferring to vanilla.
        Assert.IsTrue(float.IsNaN(CreateSut().ApplyTargetScoreModifiers(Ctx(baseScore: float.NaN))));
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_InfiniteBaseScore_ReturnsItUnmodified()
    {
        Assert.IsTrue(float.IsPositiveInfinity(
            CreateSut().ApplyTargetScoreModifiers(Ctx(baseScore: float.PositiveInfinity))));
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_VanillaRejection_PassesThrough()
    {
        Assert.AreEqual(0f, CreateSut().ApplyTargetScoreModifiers(Ctx(baseScore: 0f)), 0.0001f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_NullContext_ReturnsZero()
    {
        Assert.AreEqual(0f, CreateSut().ApplyTargetScoreModifiers(null), 0.0001f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_Defender_ReceivesHomeDefenceMultiplier()
    {
        // The home-defence lever. DefendingFactor on the GameModel cannot do this: it has exactly
        // one engine consumer (CurrentObjectiveValue, feeding Army.ThinkAboutCohesionBoost) and the
        // defender weighting inside GetTargetScoreForFaction is a hardcoded 1.75 / 1.28 literal.
        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Defender, targetFaction: "empire_w"));
        Assert.AreEqual(160f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_DefenderSettingOutOfRange_FallsBackToCompiledDefault()
    {
        // Reverting to 1.0 would silently DISABLE the home-defence lever on a garbage MCM value
        // rather than restoring its intended strength.
        _settings.DefenderPriorityMultiplier.Returns(float.NaN);

        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Defender, targetFaction: "empire_w"));

        Assert.AreEqual(160f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_Raider_PassesThroughUnmodified()
    {
        // Vanilla already hard-zeroes raiders past 5 town gaps in GetDistanceScoreForRaiding.
        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Raider, targetFaction: "gundabad"));
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_Patrolling_PassesThroughUnmodified()
    {
        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Other, targetFaction: "gundabad"));
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_MasterSwitchDisabled_PassesThroughUnmodified()
    {
        _settings.EnableArmyStrategicIntelligence.Returns(false);
        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Defender, targetFaction: "gundabad"));
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_NearPrimaryTheaterTarget_IsBoosted()
    {
        float result = CreateSut().ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, faction: "empire_w", targetFaction: "empire_s"));
        Assert.AreEqual(125f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_ForeignTarget_IsDampedButStillViable()
    {
        float result = CreateSut().ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, faction: "empire_w", targetFaction: "gundabad"));
        Assert.AreEqual(100f * _config.ForeignTheaterWeight, result, 0.01f);
        Assert.IsTrue(result > 0f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_TaomRankingSpreadStaysBelowVanillasOwnDistanceRamp()
    {
        // Sizing guard. Vanilla's own besieger distance term spans 11.1x by itself. Everything TAOM
        // contributes to candidate ranking must stay under that, or the tuning has drifted back
        // toward treating vanilla as flat, which is the mistake that produced a 44x spread.
        _config.FactionPriorityTargets["empire_w"] = new List<string> { "town_ES1" };
        var sut = CreateSut();

        float best = sut.ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, targetFaction: "empire_s", target: "town_ES1", committed: "town_ES1"));
        float worst = sut.ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, targetFaction: "gundabad", target: "town_G1"));

        float spread = best / worst;
        Assert.IsTrue(spread <= 45.0f,
            $"TAOM contributes a {spread:F1}x ranking spread including commitment stickiness; keep it bounded");
    }
}
