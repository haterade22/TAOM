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
        Assert.AreEqual(20, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(135, c.CrushThrough.SkillTargetDelta);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        Assert.AreEqual(4, c.Creatures.CleaveMonsterIds.Count);
        Assert.AreEqual(7, c.RaceModifiers.Count);
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
        Assert.AreEqual(6f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);
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
        Assert.AreEqual(20, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(6, c.CrushThrough.MonsterCrushMonsterIds.Count);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        Assert.AreEqual(7, c.RaceModifiers.Count);
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
        WriteConfig(@"{ ""crushThrough"": { ""skillDeadZone"": 135 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(20, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(135, c.CrushThrough.SkillTargetDelta);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("skillDeadZone")));
    }

    [TestMethod]
    public void GetConfig_DefaultDeadZoneStillAboveTargetDelta_RevertsTargetDeltaToo()
    {
        // Both individually in range, but 25 >= 15; the default dead zone (20) STILL violates
        // against targetDelta 15, so the target delta must revert as well.
        WriteConfig(@"{ ""crushThrough"": { ""skillDeadZone"": 25, ""skillTargetDelta"": 15 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(20, c.CrushThrough.SkillDeadZone);
        Assert.AreEqual(135, c.CrushThrough.SkillTargetDelta);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("skillTargetDelta")));
    }

    [TestMethod]
    public void GetConfig_AutoKnockdownBelowNeutral_RevertsAutoKnockdownAndWarns()
    {
        // 2 is individually valid but below the neutral ratio (6): Branch A would auto-knockdown
        // ordinary charges before Branch B's vanilla-parity math ever ran.
        WriteConfig(@"{ ""chargeKnockdown"": { ""autoKnockdownWeightRatio"": 2 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(6f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("autoKnockdownWeightRatio")));
    }

    [TestMethod]
    public void GetConfig_AutoKnockdownStillBelowNeutral_RevertsNeutralToo()
    {
        // Reverting auto to its default (8) still violates against neutral 10 — neutral reverts too.
        WriteConfig(@"{ ""chargeKnockdown"": { ""neutralWeightRatio"": 10, ""autoKnockdownWeightRatio"": 5 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(6f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);
        Assert.AreEqual(6f, c.ChargeKnockdown.NeutralWeightRatio, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("neutralWeightRatio")));
    }

    [TestMethod]
    public void GetConfig_MinPenetrationAboveMax_RevertsBothAndWarns()
    {
        WriteConfig(@"{ ""chargeKnockdown"": { ""minPenetrationFactor"": 5, ""maxPenetrationFactor"": 1 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1f, c.ChargeKnockdown.MinPenetrationFactor, 0.0001f);
        Assert.AreEqual(2.5f, c.ChargeKnockdown.MaxPenetrationFactor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("minPenetrationFactor")));
    }

    // ---- chargeDamage (#610) --------------------------------------------------------------------

    [TestMethod]
    public void GetConfig_MissingFile_ChargeDamageDefaultsShipTheKingdomTable()
    {
        var c = _sut.GetConfig();

        Assert.IsTrue(c.ChargeDamage.Enabled);
        Assert.AreEqual(1.5f, c.ChargeDamage.CultureMultipliers["vlandia"], 0.0001f);
        Assert.AreEqual(1.6f, c.ChargeDamage.CultureMultipliers["rivendell"], 0.0001f);
        Assert.AreEqual(1f, c.ChargeDamage.CultureMultipliers["erebor"], 0.0001f);
    }

    [TestMethod]
    public void GetConfig_ChargeDamageMultiplierNaN_DropsTheEntryAndWarns()
    {
        WriteConfig(@"{ ""chargeDamage"": { ""cultureMultipliers"": { ""vlandia"": NaN, ""gondor"": 1.3 } } }");

        var c = _sut.GetConfig();

        Assert.IsFalse(c.ChargeDamage.CultureMultipliers.ContainsKey("vlandia"));
        Assert.AreEqual(1.3f, c.ChargeDamage.CultureMultipliers["gondor"], 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cultureMultipliers['vlandia']")));
    }

    [TestMethod]
    public void GetConfig_ChargeDamageMultiplierOutOfRange_DropsTheEntryAndWarns()
    {
        // A multiplier of 0 would make a culture's cavalry harmless, 50 would one-shot; both are
        // typos, not tuning. Range 0.1 to 5.
        WriteConfig(@"{ ""chargeDamage"": { ""cultureMultipliers"": { ""vlandia"": 0, ""gondor"": 50 } } }");

        var c = _sut.GetConfig();

        Assert.IsFalse(c.ChargeDamage.CultureMultipliers.ContainsKey("vlandia"));
        Assert.IsFalse(c.ChargeDamage.CultureMultipliers.ContainsKey("gondor"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetConfig_ChargeDamageMapReplacesTheDefaultsRatherThanAppending()
    {
        WriteConfig(@"{ ""chargeDamage"": { ""cultureMultipliers"": { ""gondor"": 2.0 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.ChargeDamage.CultureMultipliers.Count);
        Assert.AreEqual(2f, c.ChargeDamage.CultureMultipliers["gondor"], 0.0001f);
    }

    [TestMethod]
    public void GetConfig_NullChargeDamageMap_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""chargeDamage"": { ""cultureMultipliers"": null } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1.5f, c.ChargeDamage.CultureMultipliers["vlandia"], 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cultureMultipliers")));
    }

    [TestMethod]
    public void GetConfig_NullChargeDamageSection_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""chargeDamage"": null }");

        var c = _sut.GetConfig();

        Assert.IsTrue(c.ChargeDamage.Enabled);
        Assert.AreEqual(1.5f, c.ChargeDamage.CultureMultipliers["vlandia"], 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("chargeDamage")));
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

    [TestMethod]
    public void GetConfig_ChargeDamageMultiplierInfinite_DropsBothSignsAndWarns()
    {
        // Infinity passes a bare "< min || > max" check (it IS > max) but -Infinity passes only the
        // second; FiniteFloatValidator runs first so neither reaches the table.
        WriteConfig(@"{ ""chargeDamage"": { ""cultureMultipliers"": { ""vlandia"": Infinity, ""gondor"": -Infinity, ""erebor"": 1.0 } } }");

        var c = _sut.GetConfig();

        Assert.IsFalse(c.ChargeDamage.CultureMultipliers.ContainsKey("vlandia"));
        Assert.IsFalse(c.ChargeDamage.CultureMultipliers.ContainsKey("gondor"));
        Assert.AreEqual(1f, c.ChargeDamage.CultureMultipliers["erebor"], 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cultureMultipliers['vlandia']")));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cultureMultipliers['gondor']")));
    }

    [TestMethod]
    public void GetConfig_ChargeDamageMultiplierAtTheBounds_KeepsBothEnds()
    {
        // The range is inclusive: 0.1 (a tenth of vanilla) and 5 (five times) are legal tuning.
        WriteConfig(@"{ ""chargeDamage"": { ""cultureMultipliers"": { ""vlandia"": 0.1, ""gondor"": 5.0 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.1f, c.ChargeDamage.CultureMultipliers["vlandia"], 0.0001f);
        Assert.AreEqual(5f, c.ChargeDamage.CultureMultipliers["gondor"], 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("cultureMultipliers")));
    }

    [DataTestMethod]
    [DataRow(-5)]
    [DataRow(1001)]
    public void GetConfig_BaseHitPointsOutOfRange_RevertsToUnsetAndWarns(int value)
    {
        WriteConfig(@"{ ""raceModifiers"": { ""cave_troll"": { ""baseHitPoints"": " + value + @" } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0, c.RaceModifiers["cave_troll"].BaseHitPoints);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("baseHitPoints")));
    }

    [TestMethod]
    public void GetConfig_BaseHitPointsInRange_IsKept()
    {
        WriteConfig(@"{ ""raceModifiers"": { ""hill_troll"": { ""baseHitPoints"": 200 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(200, c.RaceModifiers["hill_troll"].BaseHitPoints);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("baseHitPoints")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ShieldBashDefaults()
    {
        var c = _sut.GetConfig().ShieldBash;

        Assert.IsTrue(c.Enabled);
        Assert.AreEqual(10f, c.MinChancePercent, 0.0001f);
        Assert.AreEqual(75f, c.MaxChancePercent, 0.0001f);
        Assert.AreEqual(0.5f, c.KickChanceFactor, 0.0001f);
        Assert.AreEqual(0.5f, c.PlayerVictimChanceFactor, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_ShieldBashValuesInRange_AreKept()
    {
        WriteConfig(@"{ ""shieldBash"": { ""enabled"": false, ""minChancePercent"": 0, ""maxChancePercent"": 100, ""kickChanceFactor"": 0, ""playerVictimChanceFactor"": 1 } }");

        var c = _sut.GetConfig().ShieldBash;

        Assert.IsFalse(c.Enabled);
        Assert.AreEqual(0f, c.MinChancePercent, 0.0001f);
        Assert.AreEqual(100f, c.MaxChancePercent, 0.0001f);
        Assert.AreEqual(0f, c.KickChanceFactor, 0.0001f);
        Assert.AreEqual(1f, c.PlayerVictimChanceFactor, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("shieldBash")));
    }

    private static float ShieldBashField(ShieldBashConfig c, string key) => key switch
    {
        "minChancePercent" => c.MinChancePercent,
        "maxChancePercent" => c.MaxChancePercent,
        "kickChanceFactor" => c.KickChanceFactor,
        _ => c.PlayerVictimChanceFactor,
    };

    [DataTestMethod]
    [DataRow("minChancePercent", "-1", 10f)]
    [DataRow("minChancePercent", "101", 10f)]
    [DataRow("maxChancePercent", "-1", 75f)]
    [DataRow("maxChancePercent", "100.5", 75f)]
    [DataRow("kickChanceFactor", "-0.1", 0.5f)]
    [DataRow("kickChanceFactor", "1.5", 0.5f)]
    [DataRow("playerVictimChanceFactor", "-0.1", 0.5f)]
    [DataRow("playerVictimChanceFactor", "2", 0.5f)]
    public void GetConfig_ShieldBashValueOutOfRange_RevertsThatValueAndWarns(string key, string value, float expectedDefault)
    {
        WriteConfig(@"{ ""shieldBash"": { """ + key + @""": " + value + @" } }");

        var c = _sut.GetConfig().ShieldBash;

        Assert.AreEqual(expectedDefault, ShieldBashField(c, key), 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("shieldBash." + key)));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
    }

    // Newtonsoft reads the bare tokens NaN and Infinity as floats; a hand-edited file can carry them.
    [DataTestMethod]
    [DataRow("minChancePercent", "NaN", 10f)]
    [DataRow("maxChancePercent", "Infinity", 75f)]
    [DataRow("maxChancePercent", "-Infinity", 75f)]
    [DataRow("kickChanceFactor", "NaN", 0.5f)]
    [DataRow("playerVictimChanceFactor", "Infinity", 0.5f)]
    public void GetConfig_ShieldBashNonFiniteValue_RevertsThatValueAndWarns(string key, string value, float expectedDefault)
    {
        WriteConfig(@"{ ""shieldBash"": { """ + key + @""": " + value + @" } }");

        var c = _sut.GetConfig().ShieldBash;

        Assert.AreEqual(expectedDefault, ShieldBashField(c, key), 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("shieldBash." + key)));
    }

    [TestMethod]
    public void GetConfig_ShieldBashSwappedBand_RevertsBothEndsAndWarns()
    {
        WriteConfig(@"{ ""shieldBash"": { ""minChancePercent"": 60, ""maxChancePercent"": 20 } }");

        var c = _sut.GetConfig().ShieldBash;

        Assert.AreEqual(10f, c.MinChancePercent, 0.0001f);
        Assert.AreEqual(75f, c.MaxChancePercent, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("exceeds maxChancePercent")));
    }

    [TestMethod]
    public void GetConfig_ShieldBashEqualEnds_IsAllowed()
    {
        WriteConfig(@"{ ""shieldBash"": { ""minChancePercent"": 30, ""maxChancePercent"": 30 } }");

        var c = _sut.GetConfig().ShieldBash;

        Assert.AreEqual(30f, c.MinChancePercent, 0.0001f);
        Assert.AreEqual(30f, c.MaxChancePercent, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_ShieldBashSectionNull_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""shieldBash"": null }");

        var c = _sut.GetConfig().ShieldBash;

        Assert.IsTrue(c.Enabled);
        Assert.AreEqual(10f, c.MinChancePercent, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("shieldBash section is null")));
    }

    [TestMethod]
    public void GetConfig_ShieldBashSectionAbsent_UsesDefaultsWithoutAWarning()
    {
        WriteConfig(@"{ }");

        var c = _sut.GetConfig().ShieldBash;

        Assert.AreEqual(75f, c.MaxChancePercent, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("shieldBash")));
    }

    // ---- Cleaving Blows ----

    [TestMethod]
    public void GetConfig_MissingFile_CleavingBlowsDefaults()
    {
        var c = _sut.GetConfig().CleavingBlows;

        Assert.IsTrue(c.Enabled);
        Assert.IsFalse(c.AiEnabled);
        Assert.AreEqual(4, c.WeaponClassCarry.Count);
        Assert.AreEqual(0.6f, c.WeaponClassCarry["TwoHandedAxe"], 0.0001f);
        Assert.AreEqual(0.4f, c.WeaponClassCarry["TwoHandedPolearm"], 0.0001f);
        CollectionAssert.AreEqual(new[] { "TwoHandedAxe", "TwoHandedMace", "TwoHandedSword" }, c.AiWeaponClasses);
        Assert.AreEqual(3, c.MaxTargetsPerSwing);
        Assert.IsFalse(c.IncludeMountedAttackers);
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsSectionAbsent_UsesDefaultsWithoutAWarning()
    {
        WriteConfig(@"{ }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(4, c.WeaponClassCarry.Count);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("cleavingBlows")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsSectionNull_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""cleavingBlows"": null }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.IsFalse(c.AiEnabled);
        Assert.AreEqual(4, c.WeaponClassCarry.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cleavingBlows section is null")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsValidBlock_RoundTrips()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""enabled"": false, ""aiEnabled"": true, ""weaponClassCarry"": { ""TwoHandedAxe"": 0.7, ""TwoHandedSword"": 1 }, ""aiWeaponClasses"": [""TwoHandedSword""], ""killCarryBonus"": 0.2, ""skillCarryBonus"": 0, ""maxCarryFactor"": 0.9, ""minRemainingMomentum"": 0.1, ""maxTargetsPerSwing"": 5, ""includeMountedAttackers"": true } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.IsFalse(c.Enabled);
        Assert.IsTrue(c.AiEnabled);
        Assert.AreEqual(2, c.WeaponClassCarry.Count);
        Assert.AreEqual(0.7f, c.WeaponClassCarry["TwoHandedAxe"], 0.0001f);
        Assert.AreEqual(1f, c.WeaponClassCarry["TwoHandedSword"], 0.0001f);
        CollectionAssert.AreEqual(new[] { "TwoHandedSword" }, c.AiWeaponClasses);
        Assert.AreEqual(0.2f, c.KillCarryBonus, 0.0001f);
        Assert.AreEqual(0f, c.SkillCarryBonus, 0.0001f);
        Assert.AreEqual(0.9f, c.MaxCarryFactor, 0.0001f);
        Assert.AreEqual(0.1f, c.MinRemainingMomentum, 0.0001f);
        Assert.AreEqual(5, c.MaxTargetsPerSwing);
        Assert.IsTrue(c.IncludeMountedAttackers);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("cleavingBlows")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsOneEntryCarryMap_ReplacesTheFourDefaults()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""weaponClassCarry"": { ""TwoHandedAxe"": 0.5 }, ""aiWeaponClasses"": [""TwoHandedAxe""] } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(1, c.WeaponClassCarry.Count);
        Assert.AreEqual(0.5f, c.WeaponClassCarry["TwoHandedAxe"], 0.0001f);
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsNullCarryMap_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""weaponClassCarry"": null } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(4, c.WeaponClassCarry.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("weaponClassCarry is null")));
    }

    [DataTestMethod]
    [DataRow("\"Typo\": 0.5")]
    [DataRow("\"5\": 0.5")]
    [DataRow("\"twohandedaxe\": 0.5")]
    [DataRow("\"TwoHandedSword\": 0")]
    [DataRow("\"TwoHandedSword\": 1.5")]
    [DataRow("\"TwoHandedSword\": NaN")]
    [DataRow("\"TwoHandedSword\": Infinity")]
    public void GetConfig_CleavingBlowsBadCarryRow_IsDroppedAndWarns(string badRow)
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""weaponClassCarry"": { ""TwoHandedAxe"": 0.6, " + badRow + @" }, ""aiWeaponClasses"": [""TwoHandedAxe""] } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(1, c.WeaponClassCarry.Count);
        Assert.IsTrue(c.WeaponClassCarry.ContainsKey("TwoHandedAxe"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("weaponClassCarry[")));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsNullAiList_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""aiWeaponClasses"": null } }");

        var c = _sut.GetConfig().CleavingBlows;

        CollectionAssert.AreEqual(new[] { "TwoHandedAxe", "TwoHandedMace", "TwoHandedSword" }, c.AiWeaponClasses);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("aiWeaponClasses is null")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsUnknownAiClass_IsDroppedAndWarns()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""aiWeaponClasses"": [""TwoHandedAxe"", ""Nonsense"", ""7""] } }");

        var c = _sut.GetConfig().CleavingBlows;

        CollectionAssert.AreEqual(new[] { "TwoHandedAxe" }, c.AiWeaponClasses);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("aiWeaponClasses entry 'Nonsense'")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsAiClassWithoutACarryFactor_IsDroppedAndWarns()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""weaponClassCarry"": { ""TwoHandedAxe"": 0.6 }, ""aiWeaponClasses"": [""TwoHandedAxe"", ""TwoHandedSword""] } }");

        var c = _sut.GetConfig().CleavingBlows;

        CollectionAssert.AreEqual(new[] { "TwoHandedAxe" }, c.AiWeaponClasses);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("aiWeaponClasses entry 'TwoHandedSword'")));
    }

    private static float CleaveField(CleavingBlowsConfig c, string key) => key switch
    {
        "killCarryBonus" => c.KillCarryBonus,
        "skillCarryBonus" => c.SkillCarryBonus,
        "maxCarryFactor" => c.MaxCarryFactor,
        _ => c.MinRemainingMomentum,
    };

    [DataTestMethod]
    [DataRow("killCarryBonus", "NaN", 0.15f)]
    [DataRow("killCarryBonus", "-0.1", 0.15f)]
    [DataRow("killCarryBonus", "1.1", 0.15f)]
    [DataRow("skillCarryBonus", "NaN", 0.1f)]
    [DataRow("skillCarryBonus", "-0.1", 0.1f)]
    [DataRow("skillCarryBonus", "1.1", 0.1f)]
    [DataRow("maxCarryFactor", "0", 0.75f)]
    [DataRow("maxCarryFactor", "0.96", 0.75f)]
    [DataRow("maxCarryFactor", "NaN", 0.75f)]
    [DataRow("minRemainingMomentum", "NaN", 0.25f)]
    [DataRow("minRemainingMomentum", "-0.1", 0.25f)]
    [DataRow("minRemainingMomentum", "1.1", 0.25f)]
    public void GetConfig_CleavingBlowsBadFloat_RevertsThatValueAndWarns(string key, string value, float expectedDefault)
    {
        WriteConfig(@"{ ""cleavingBlows"": { """ + key + @""": " + value + @" } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(expectedDefault, CleaveField(c, key), 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cleavingBlows." + key)));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsFloorAtOrAboveCeiling_RevertsBothAndWarns()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""minRemainingMomentum"": 0.6, ""maxCarryFactor"": 0.5 } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(0.25f, c.MinRemainingMomentum, 0.0001f);
        Assert.AreEqual(0.75f, c.MaxCarryFactor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("must be below maxCarryFactor")));
    }

    [TestMethod]
    public void GetConfig_CleavingBlowsFloorEqualToCeiling_RevertsBoth()
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""minRemainingMomentum"": 0.5, ""maxCarryFactor"": 0.5 } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(0.25f, c.MinRemainingMomentum, 0.0001f);
        Assert.AreEqual(0.75f, c.MaxCarryFactor, 0.0001f);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(6)]
    [DataRow(0)]
    public void GetConfig_CleavingBlowsMaxTargetsOutsideTheSlider_RevertsToDefaultAndWarns(int value)
    {
        WriteConfig(@"{ ""cleavingBlows"": { ""maxTargetsPerSwing"": " + value + @" } }");

        var c = _sut.GetConfig().CleavingBlows;

        Assert.AreEqual(3, c.MaxTargetsPerSwing);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cleavingBlows.maxTargetsPerSwing")));
    }
}
