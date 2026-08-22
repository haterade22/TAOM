using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// The reach falloff and the soft theater weighting, which together replace the old
/// "long-range priority boost" mechanism.
///
/// SIZING, and why it is gentle. Vanilla is NOT flat with distance: its besieger term is
/// MBMath.Map((5G-d)/G, 0, 5, 0.9f, 10f), which already spans 10.0 at zero distance down to 0.9
/// at five town gaps, an 11.1x ramp, and CalculateDistanceScoreForBesieging hard-zeroes any
/// target whose two-hop topology score is under 0.1. TAOM multiplies on top of that rather than
/// replacing it. So this term only has to stop TAOM's OWN multipliers (priority up to 3.0 x
/// primary theater 1.25 = 3.75) from outrunning vanilla's 2.2x penalty at three gaps. An earlier
/// draft sized it as if vanilla were flat and produced a 44x combined spread that crushed real
/// fronts; the measured widest genuine hostile front on this map is about 2.95 gaps.
///
/// A hard theater gate was rejected on measurement: the corrected membership table severs only
/// six pairs, all of which the falloff already handles, while a hard gate strands any kingdom
/// whose enemies are all foreign and loses its army to Army.CheckInactivity two days later.
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
        _settings.ReachRadiusInTownGaps.Returns(6.0f);
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
        string committed = null,
        float distance = 0f) =>
        new TargetScoreContext
        {
            BaseScore = baseScore,
            Mission = mission,
            FactionId = faction,
            TargetFactionId = targetFaction,
            TargetSettlementId = target,
            CommittedTargetId = committed,
            NormalizedDistance = distance,
        };

    // ---------------------------------------------------------------- reach falloff

    [TestMethod]
    public void GetReachMultiplier_AtOrigin_ReturnsUnity()
    {
        Assert.AreEqual(1.0f, CreateSut().GetReachMultiplier(0f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_InsideInnerRadius_ReturnsUnity()
    {
        // The widest measured genuine hostile front is about 2.95 gaps. Every real war must sit
        // in the flat region, because vanilla already penalises them on its own.
        Assert.AreEqual(1.0f, CreateSut().GetReachMultiplier(2.9f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_AtOuterRadius_ReturnsFloor()
    {
        Assert.AreEqual(_config.ReachFloor, CreateSut().GetReachMultiplier(6.0f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_FarBeyondRadius_ReturnsFloor()
    {
        Assert.AreEqual(_config.ReachFloor, CreateSut().GetReachMultiplier(9.0f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_FloatMaxValue_ReturnsFloorWithoutOverflow()
    {
        float result = CreateSut().GetReachMultiplier(float.MaxValue);
        Assert.AreEqual(_config.ReachFloor, result, 0.0001f);
        Assert.IsFalse(float.IsNaN(result));
        Assert.IsFalse(float.IsInfinity(result));
    }

    [TestMethod]
    public void GetReachMultiplier_UnreachableSentinel_ReturnsFloor()
    {
        // The engine reports an unreachable pair as a huge finite sentinel (1e30), not infinity.
        Assert.AreEqual(_config.ReachFloor, CreateSut().GetReachMultiplier(1e30f / 93.95f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_NaN_DoesNotSuppress()
    {
        // NaN means "the adapter could not measure a distance" (landless faction, degenerate town
        // gap). Damping every target on garbage would break AI targeting outright, so the gate
        // that would suppress must FAIL on NaN. csharp-architecture.md NaN-gate rule.
        Assert.AreEqual(1.0f, CreateSut().GetReachMultiplier(float.NaN), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_Infinity_DoesNotSuppress()
    {
        Assert.AreEqual(1.0f, CreateSut().GetReachMultiplier(float.PositiveInfinity), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_NegativeDistance_ReturnsUnity()
    {
        Assert.AreEqual(1.0f, CreateSut().GetReachMultiplier(-5f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_IsMonotonicallyNonIncreasing()
    {
        // The guard against a sign error that would invert the whole feature into "prefer the far
        // war". Steps of 0.1 across 0 to 40 gaps.
        var sut = CreateSut();
        float previous = sut.GetReachMultiplier(0f);
        for (int step = 1; step <= 400; step++)
        {
            float d = step * 0.1f;
            float current = sut.GetReachMultiplier(d);
            Assert.IsTrue(current <= previous + 0.0001f,
                $"reach rose from {previous} to {current} at {d} town gaps");
            Assert.IsTrue(current >= _config.ReachFloor - 0.0001f, $"reach fell below the floor at {d}");
            Assert.IsTrue(current <= 1.0f + 0.0001f, $"reach exceeded unity at {d}");
            previous = current;
        }
    }

    [TestMethod]
    public void GetReachMultiplier_DoesNotDampMeasuredRealFronts()
    {
        // The widest genuine hostile fronts measured on the live map, in town gaps. Vanilla
        // already applies its own ramp across this range, so TAOM must not stack a second one.
        var sut = CreateSut();
        foreach (float gaps in new[] { 0.18f, 1.04f, 1.76f, 2.24f, 2.52f, 2.95f })
            Assert.AreEqual(1.0f, sut.GetReachMultiplier(gaps), 0.0001f, $"real front at {gaps} gaps was damped");
    }

    [TestMethod]
    public void GetReachMultiplier_ContributesLessSpreadThanVanillaAlreadyDoes()
    {
        // The guard against re-creating the 44x version. Vanilla already supplies an 11.1x
        // distance ramp of its own; TAOM only needs to cancel its own 3.75x priority-plus-
        // theater stack, so anything past about 4x here means the sizing drifted back to
        // treating vanilla as flat.
        var sut = CreateSut();
        float taomSpread = sut.GetReachMultiplier(0f) / sut.GetReachMultiplier(99f);
        Assert.IsTrue(taomSpread <= 4.0f,
            $"TAOM contributes a {taomSpread:F1}x spread on top of vanilla's own 11.1x ramp");
    }

    [TestMethod]
    public void GetReachMultiplier_FeatureDisabled_ReturnsUnity()
    {
        _settings.EnableArmyStrategicIntelligence.Returns(false);
        Assert.AreEqual(1.0f, CreateSut().GetReachMultiplier(50f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_NaNRadiusSetting_FallsBackToConfigRadius()
    {
        _settings.ReachRadiusInTownGaps.Returns(float.NaN);
        var sut = CreateSut();
        Assert.AreEqual(1.0f, sut.GetReachMultiplier(0f), 0.0001f);
        Assert.AreEqual(_config.ReachFloor, sut.GetReachMultiplier(6.0f), 0.0001f);
    }

    [TestMethod]
    public void GetReachMultiplier_InnerRadiusAboveOuter_CannotInvert()
    {
        // MCM can drive the outer radius below the config's inner radius. The service derives the
        // inner one from the resolved outer one so the span can never go non-positive.
        _config.ReachInnerRadiusInTownGaps = 50f;
        _settings.ReachRadiusInTownGaps.Returns(2.0f);
        var sut = CreateSut();

        Assert.AreEqual(1.0f, sut.GetReachMultiplier(0.5f), 0.0001f);
        Assert.AreEqual(_config.ReachFloor, sut.GetReachMultiplier(2.0f), 0.0001f);
        Assert.IsFalse(float.IsNaN(sut.GetReachMultiplier(1.5f)));
    }

    // ---------------------------------------------------------------- IsWithinReach

    [TestMethod]
    public void IsWithinReach_NearTarget_IsTrue()
    {
        Assert.IsTrue(CreateSut().IsWithinReach(1.0f));
    }

    [TestMethod]
    public void IsWithinReach_BeyondRadius_IsFalse()
    {
        Assert.IsFalse(CreateSut().IsWithinReach(9.0f));
    }

    [TestMethod]
    public void IsWithinReach_NaN_IsFalse()
    {
        // Opposite polarity to GetReachMultiplier and deliberately so: this gate decides whether
        // TAOM may OVERTURN vanilla's "unreachable" verdict. Both directions defer to vanilla.
        Assert.IsFalse(CreateSut().IsWithinReach(float.NaN));
    }

    [TestMethod]
    public void IsWithinReach_FeatureDisabled_IsTrue()
    {
        _settings.EnableArmyStrategicIntelligence.Returns(false);
        Assert.IsTrue(CreateSut().IsWithinReach(float.NaN));
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
        // The behaviour this feature exists to stop: Gondor [south, central] against Gundabad [north].
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
        float result = CreateSut().ApplyTargetScoreModifiers(Ctx(baseScore: float.NaN));
        Assert.IsTrue(float.IsNaN(result));
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_InfiniteBaseScore_ReturnsItUnmodified()
    {
        float result = CreateSut().ApplyTargetScoreModifiers(Ctx(baseScore: float.PositiveInfinity));
        Assert.IsTrue(float.IsPositiveInfinity(result));
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
    public void ApplyTargetScoreModifiers_Raider_PassesThroughUnmodified()
    {
        // Vanilla already hard-zeroes raiders past 5 town gaps in GetDistanceScoreForRaiding.
        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Raider, targetFaction: "gundabad", distance: 9f));
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_Patrolling_PassesThroughUnmodified()
    {
        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Other, targetFaction: "gundabad", distance: 9f));
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_MasterSwitchDisabled_PassesThroughUnmodified()
    {
        _settings.EnableArmyStrategicIntelligence.Returns(false);
        float result = CreateSut().ApplyTargetScoreModifiers(
            Ctx(baseScore: 100f, mission: ArmyTargetingMission.Defender, targetFaction: "gundabad", distance: 9f));
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_CommitmentCannotOutrunSuppression()
    {
        // The interaction that would otherwise pin an in-flight cross-map siege forever on an
        // existing save: Army.AiBehaviorObject persists, and TAOM stacks a 4.0x commitment
        // multiplier on the target an army already holds.
        var sut = CreateSut();

        float pinnedFarTarget = sut.ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, faction: "empire_w", targetFaction: "gundabad",
            target: "town_G1", committed: "town_G1", distance: 9.0f));

        float freshNearTarget = sut.ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, faction: "empire_w", targetFaction: "empire_s",
            target: "town_ES1", committed: "town_G1", distance: 1.0f));

        Assert.IsTrue(freshNearTarget > pinnedFarTarget,
            $"a legal near target ({freshNearTarget}) must beat a committed cross-map siege ({pinnedFarTarget}), or armies never come home");
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_NearPrimaryTheaterTarget_IsBoosted()
    {
        float result = CreateSut().ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, faction: "empire_w", targetFaction: "empire_s", distance: 1.0f));
        Assert.AreEqual(125f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_FarForeignTarget_IsDamped()
    {
        float result = CreateSut().ApplyTargetScoreModifiers(Ctx(
            baseScore: 100f, faction: "empire_w", targetFaction: "gundabad", distance: 9.0f));
        Assert.AreEqual(100f * _config.ForeignTheaterWeight * _config.ReachFloor, result, 0.01f);
    }
}
