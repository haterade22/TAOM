using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class CombatMechanicsConfigProviderTests
{
    private string _tempDir = null!;
    private string _configDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private CombatMechanicsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CombatMechanics_" + Path.GetRandomFileName());
        _configDir = Path.Combine(_tempDir, "combat_mechanics");
        Directory.CreateDirectory(_configDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CombatMechanicsConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_configDir, "combat_mechanics_config.json"), json);

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var c = _sut.GetConfig();

        Assert.AreEqual(25f, c.CrushThrough.ExtraCrushThroughEnergyThreshold, 0.0001f);
        Assert.AreEqual(30, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        Assert.AreEqual(4, c.Creatures.CleaveMonsterIds.Count);
        Assert.AreEqual(5, c.RaceModifiers.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_SauronDefaultsMirrorElf()
    {
        // Sauron (lord_1_17) was race "elf" until issue #321 split him into his own race;
        // the compiled default must keep his former elf CTB/overhead baseline (his Dark Lord
        // additions on top are pinned separately below).
        var c = _sut.GetConfig();

        Assert.AreEqual(c.RaceModifiers["elf"].CtbAttackBonus, c.RaceModifiers["sauron"].CtbAttackBonus, 0.0001f);
        Assert.AreEqual(c.RaceModifiers["elf"].RemoveNonOverheadPenalty, c.RaceModifiers["sauron"].RemoveNonOverheadPenalty);
    }

    [TestMethod]
    public void GetConfig_MissingFile_SauronOffenseAndKnockdownDefaults()
    {
        // #321 follow-up (user decision): the Dark Lord gets the full offensive set + charge
        // knockdown resistance above the dwarf ceiling (2.5).
        var c = _sut.GetConfig();

        Assert.AreEqual(3.0f, c.RaceModifiers["sauron"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(0.20f, c.RaceModifiers["sauron"].SwingEnergyBonusFactor, 0.0001f);
        CollectionAssert.Contains(c.CrushThrough.MonsterCrushMonsterIds, "sauron");
        CollectionAssert.Contains(c.CrushThrough.OrcShieldCrushRaces, "sauron");
        CollectionAssert.Contains(c.Creatures.CleaveMonsterIds, "sauron");
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var c = _sut.GetConfig();

        Assert.AreEqual(25f, c.CrushThrough.ExtraCrushThroughEnergyThreshold, 0.0001f);
        Assert.AreEqual(8f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_SingleCleaveMonsterId_ReplacesDefaultListNotAppends()
    {
        // ObjectCreationHandling.Replace pin: Json.NET's default append-merge would yield the
        // 3 compiled defaults + 1 JSON entry = 4. The JSON list must REPLACE the default.
        WriteConfig(@"{ ""creatures"": { ""cleaveMonsterIds"": [ ""cave_troll"" ] } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.Creatures.CleaveMonsterIds.Count);
        Assert.AreEqual("cave_troll", c.Creatures.CleaveMonsterIds[0]);
    }

    [TestMethod]
    public void GetConfig_SingleUnstoppableEntry_ReplacesDefaultDictNotMerges()
    {
        WriteConfig(@"{ ""creatures"": { ""unstoppableDamageThresholds"": { ""spider"": 20 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.Creatures.UnstoppableDamageThresholds.Count);
        Assert.AreEqual(20, c.Creatures.UnstoppableDamageThresholds["spider"]);
    }

    [TestMethod]
    public void GetConfig_PartialJson_AbsentFieldsKeepDefaults()
    {
        WriteConfig(@"{ ""crushThrough"": { ""maxSkillChance"": 0.3 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.3f, c.CrushThrough.MaxSkillChance, 0.0001f);
        Assert.AreEqual(30, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(6, c.CrushThrough.MonsterCrushMonsterIds.Count);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        Assert.AreEqual(5, c.RaceModifiers.Count);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""enabled"": true }");

        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
    }

    [TestMethod]
    public void GetConfig_NaNMaxSkillChance_RevertsToDefaultAndWarns()
    {
        // IEEE-754: naive `value < min || value > max` checks pass NaN through — must be rejected.
        WriteConfig(@"{ ""crushThrough"": { ""maxSkillChance"": NaN } }");

        var c = _sut.GetConfig();

        Assert.IsTrue(FiniteFloatValidator.IsFinite(c.CrushThrough.MaxSkillChance),
            "NaN maxSkillChance must be rejected, never surfaced");
        Assert.AreEqual(0.5f, c.CrushThrough.MaxSkillChance, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxSkillChance")));
    }

    [TestMethod]
    public void GetConfig_InfinityNeutralWeightRatio_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""chargeKnockdown"": { ""neutralWeightRatio"": Infinity } }");

        var c = _sut.GetConfig();

        Assert.IsTrue(FiniteFloatValidator.IsFinite(c.ChargeKnockdown.NeutralWeightRatio));
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("neutralWeightRatio")));
    }

    [TestMethod]
    public void GetConfig_DeadZoneAtOrAboveTargetDelta_RevertsDeadZoneAndWarns()
    {
        // Equality is a violation too: deadZone == targetDelta makes the chance-curve
        // denominator 1 − e^0 = 0 (division by zero).
        WriteConfig(@"{ ""crushThrough"": { ""skillDeadZone"": 200 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(30, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(200, c.CrushThrough.SkillTargetDelta);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("skillDeadZone")));
    }

    [TestMethod]
    public void GetConfig_DefaultDeadZoneStillAboveTargetDelta_RevertsTargetDeltaToo()
    {
        // Both individually in range, but 25 >= 20; the default dead zone (30) STILL violates
        // against targetDelta 20, so the target delta must revert as well.
        WriteConfig(@"{ ""crushThrough"": { ""skillDeadZone"": 25, ""skillTargetDelta"": 20 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(30, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(200, c.CrushThrough.SkillTargetDelta);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("skillTargetDelta")));
    }

    [TestMethod]
    public void GetConfig_AutoKnockdownBelowNeutral_RevertsAutoKnockdownAndWarns()
    {
        // 2 is individually valid but below the neutral ratio (6): Branch A would auto-knockdown
        // ordinary charges before Branch B's vanilla-parity math ever ran.
        WriteConfig(@"{ ""chargeKnockdown"": { ""autoKnockdownWeightRatio"": 2 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(8f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("autoKnockdownWeightRatio")));
    }

    [TestMethod]
    public void GetConfig_AutoKnockdownStillBelowNeutral_RevertsNeutralToo()
    {
        // Reverting auto to its default (8) still violates against neutral 10 — neutral reverts too.
        WriteConfig(@"{ ""chargeKnockdown"": { ""neutralWeightRatio"": 10, ""autoKnockdownWeightRatio"": 5 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(8f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("neutralWeightRatio")));
    }

    [TestMethod]
    public void GetConfig_MinPenetrationAboveMax_RevertsBothAndWarns()
    {
        WriteConfig(@"{ ""chargeKnockdown"": { ""minPenetrationFactor"": 5, ""maxPenetrationFactor"": 1 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.25f, c.ChargeKnockdown.MinPenetrationFactor, 0.0001f);
        Assert.AreEqual(2.5f, c.ChargeKnockdown.MaxPenetrationFactor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("minPenetrationFactor")));
    }

    [TestMethod]
    public void GetConfig_UnstoppableThresholdOutOfRange_EntryRemovedAndWarned()
    {
        // Parsed-but-unresolvable: an absurd threshold is REMOVED (the creature falls back to
        // having no unstoppable row), not clamped into silently different mechanics.
        WriteConfig(@"{ ""creatures"": { ""unstoppableDamageThresholds"": { ""cave_troll"": 9999, ""spider"": 10 } } }");

        var c = _sut.GetConfig();

        Assert.IsFalse(c.Creatures.UnstoppableDamageThresholds.ContainsKey("cave_troll"));
        Assert.AreEqual(10, c.Creatures.UnstoppableDamageThresholds["spider"]);
        Assert.AreEqual(1, c.Creatures.UnstoppableDamageThresholds.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cave_troll")));
    }

    [TestMethod]
    public void GetConfig_ZeroCorrectionDivisor_RevertsToDefaultAndWarns()
    {
        // Divisor bound is EXCLUSIVE at 0 — dividing shield damage by 0 poisons it with Infinity.
        WriteConfig(@"{ ""shieldPenetration"": { ""runtimeShieldDamageCorrectionDivisor"": 0 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.3f, c.ShieldPenetration.RuntimeShieldDamageCorrectionDivisor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("runtimeShieldDamageCorrectionDivisor")));
    }

    [TestMethod]
    public void GetConfig_UnknownWeaponClassEntry_SkippedAndWarned()
    {
        // The M1 unknown-string trap: a typo'd WeaponClass name would silently match nothing
        // in the consumer — it must be removed with a warning at load, not carried through.
        WriteConfig(@"{ ""shieldPenetration"": { ""weaponClasses"": [ ""Javelin"", ""Jaevlin"" ] } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.ShieldPenetration.WeaponClasses.Count);
        Assert.AreEqual("Javelin", c.ShieldPenetration.WeaponClasses[0]);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("Jaevlin")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ShieldPenetrationOffByDefault()
    {
        // Shipped default is vanilla parity: javelins pierce shields only through the engine's own
        // Throwing.Impale grant in SandboxAgentApplyDamageModel.DecideMissileWeaponFlags.
        //
        // The class-wide grant used to ship ON with a 1/0.3 (3.33x) shield-damage multiplier. That
        // multiplier was justified as correcting a native underestimation for runtime-granted flags,
        // but MissionCombatMechanicsHelper.ComputeBlowDamageOnShield picks the missile multiplier by
        // weapon CLASS first (Javelin -> 0.5f) and never reads the penetration flags, so there was
        // nothing to correct for the one class in the list. The inflated shield damage then fed the
        // penetration gate in Mission.HandleMissileCollisionReaction, so javelins punched through
        // shields that should have stopped them. Compiled defaults are the revert-to target on a
        // validation failure, so they must not resurrect the mechanic.
        var c = _sut.GetConfig();

        Assert.IsFalse(c.ShieldPenetration.Enabled);
        Assert.AreEqual(0, c.ShieldPenetration.WeaponClasses.Count);
        Assert.AreEqual(0, c.ShieldPenetration.ItemIds.Count);
        Assert.IsFalse(c.ShieldPenetration.AddMultiplePenetration);
        Assert.IsFalse(c.ShieldPenetration.RuntimeShieldDamageCorrectionEnabled);
    }

    [TestMethod]
    public void GetConfig_NegativeSwingEnergyBonusFactor_RevertsToDefaultAndWarns()
    {
        // Sign violation: "bonus" fields are directional, a negative flips the mechanic.
        WriteConfig(@"{ ""raceModifiers"": { ""orc"": { ""swingEnergyBonusFactor"": -0.5 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.RaceModifiers.Count);
        Assert.AreEqual(0f, c.RaceModifiers["orc"].SwingEnergyBonusFactor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("swingEnergyBonusFactor")));
    }

    [TestMethod]
    public void GetConfig_NullRaceRow_BecomesNeutralAndWarns()
    {
        WriteConfig(@"{ ""raceModifiers"": { ""elf"": null } }");

        var c = _sut.GetConfig();

        Assert.IsNotNull(c.RaceModifiers["elf"]);
        Assert.AreEqual(0f, c.RaceModifiers["elf"].CtbAttackBonus, 0.0001f);
        Assert.AreEqual(1f, c.RaceModifiers["elf"].KnockdownResistanceMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("elf")));
    }

    [TestMethod]
    public void GetConfig_EmptyCleaveMonsterId_DroppedAndWarned()
    {
        WriteConfig(@"{ ""creatures"": { ""cleaveMonsterIds"": [ ""cave_troll"", """" ] } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.Creatures.CleaveMonsterIds.Count);
        Assert.AreEqual("cave_troll", c.Creatures.CleaveMonsterIds[0]);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cleaveMonsterIds")));
    }

    [TestMethod]
    public void GetConfig_AnyReversion_EmitsSummaryWarning()
    {
        WriteConfig(@"{ ""crushThrough"": { ""maxSkillChance"": NaN } }");

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_ValidJson_PreservesValuesAndLogsInfo()
    {
        WriteConfig(@"{
  ""enabled"": true,
  ""crushThrough"": {
    ""extraCrushThroughEnergyThreshold"": 30,
    ""skillDeadZone"": 20,
    ""skillTargetDelta"": 150,
    ""maxSkillChance"": 0.4,
    ""skillCurveExponent"": 0.01,
    ""energyRampMarginFactor"": 0.5,
    ""nonOverheadPenaltyFactor"": 0.6
  },
  ""chargeKnockdown"": {
    ""neutralWeightRatio"": 5,
    ""autoKnockdownWeightRatio"": 9,
    ""autoKnockdownMinSpeedFactor"": 0.5,
    ""defaultChargeSpeedReference"": 4.0,
    ""minPenetrationFactor"": 0.5,
    ""maxPenetrationFactor"": 2.0,
    ""horseChargePenetration"": 0.35
  },
  ""creatures"": {
    ""cleaveRemainingMomentumFactor"": 0.4,
    ""unstoppableDamageThresholds"": { ""spider"": 12 }
  },
  ""shieldPenetration"": {
    ""weaponClasses"": [ ""Javelin"", ""ThrowingAxe"" ],
    ""runtimeShieldDamageCorrectionDivisor"": 0.25
  },
  ""raceModifiers"": {
    ""dwarf"": { ""ctbDefenseBonus"": 10, ""knockdownResistanceMultiplier"": 2.0 }
  }
}");

        var c = _sut.GetConfig();

        Assert.AreEqual(30f, c.CrushThrough.ExtraCrushThroughEnergyThreshold, 0.0001f);
        Assert.AreEqual(20, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(150, c.CrushThrough.SkillTargetDelta);
        Assert.AreEqual(0.4f, c.CrushThrough.MaxSkillChance, 0.0001f);
        Assert.AreEqual(0.01f, c.CrushThrough.SkillCurveExponent, 0.0001f);
        Assert.AreEqual(0.5f, c.CrushThrough.EnergyRampMarginFactor, 0.0001f);
        Assert.AreEqual(0.6f, c.CrushThrough.NonOverheadPenaltyFactor, 0.0001f);
        Assert.AreEqual(5f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        Assert.AreEqual(9f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);
        Assert.AreEqual(0.5f, c.ChargeKnockdown.AutoKnockdownMinSpeedFactor, 0.0001f);
        Assert.AreEqual(4.0f, c.ChargeKnockdown.DefaultChargeSpeedReference, 0.0001f);
        Assert.AreEqual(0.5f, c.ChargeKnockdown.MinPenetrationFactor, 0.0001f);
        Assert.AreEqual(2.0f, c.ChargeKnockdown.MaxPenetrationFactor, 0.0001f);
        Assert.AreEqual(0.35f, c.ChargeKnockdown.HorseChargePenetration, 0.0001f);
        Assert.AreEqual(0.4f, c.Creatures.CleaveRemainingMomentumFactor, 0.0001f);
        Assert.AreEqual(12, c.Creatures.UnstoppableDamageThresholds["spider"]);
        Assert.AreEqual(2, c.ShieldPenetration.WeaponClasses.Count);
        Assert.AreEqual(0.25f, c.ShieldPenetration.RuntimeShieldDamageCorrectionDivisor, 0.0001f);
        Assert.AreEqual(10f, c.RaceModifiers["dwarf"].CtbDefenseBonus, 0.0001f);
        Assert.AreEqual(2.0f, c.RaceModifiers["dwarf"].KnockdownResistanceMultiplier, 0.0001f);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
