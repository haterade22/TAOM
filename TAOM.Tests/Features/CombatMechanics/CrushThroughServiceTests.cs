using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class CrushThroughServiceTests
{
    private ICombatMechanicsConfigProvider _configProvider;
    private ICombatMechanicsSettingsProvider _settings;
    private IRaceCombatModifiersResolver _resolver;
    private IRaceManager _raceManager;
    private IModLogger _logger;
    private CombatMechanicsConfig _config;
    private CrushThroughService _sut;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _resolver = Substitute.For<IRaceCombatModifiersResolver>();
        _raceManager = Substitute.For<IRaceManager>();
        _logger = Substitute.For<IModLogger>();

        _config = new CombatMechanicsConfig();
        _configProvider.GetConfig().Returns(_config);

        _settings.SkillCrushThroughEnabled.Returns(true);
        _settings.MonsterCrushThroughEnabled.Returns(true);
        _settings.OrcShieldCrushEnabled.Returns(true);
        _settings.CrushThroughMaxChance.Returns(0.5f);

        _resolver.Resolve(Arg.Any<int?>()).Returns(RaceCombatModifiers.Neutral);

        _sut = new CrushThroughService(_configProvider, _settings, _resolver, _raceManager, _logger);
    }

    // Defaults describe an otherwise-crushing attack: overhead swing, delta 200 (== target delta,
    // chance exactly 0.5 at max-chance 0.5), energy 100 (full ramp), roll 0.
    private static CrushThroughContext Ctx(
        float energy = 100f,
        bool isSwing = true,
        bool isOverhead = true,
        bool isPassiveUsage = false,
        bool hasMeleeWeapon = true,
        bool defendItemIsShield = false,
        int attackerSkill = 200,
        int defenderSkill = 0,
        int? attackerRaceId = null,
        int? defenderRaceId = null,
        string? attackerMonsterId = null,
        bool isAiControlled = false,
        float roll = 0f)
    {
        return new CrushThroughContext(
            energy,
            isSwing,
            isOverhead,
            isPassiveUsage,
            hasMeleeWeapon,
            defendItemIsShield,
            attackerSkill,
            defenderSkill,
            attackerRaceId,
            defenderRaceId,
            attackerMonsterId!,
            isAiControlled,
            roll);
    }

    private void SetAttackerRace(int raceId, string raceName)
    {
        _raceManager.IsValidRaceId(raceId).Returns(true);
        _raceManager.GetRaceNameFromId(raceId).Returns(raceName);
    }

    [TestMethod]
    public void DecideCrushThrough_DeltaEqualsDeadZone_ReturnsNull()
    {
        var result = _sut.DecideCrushThrough(Ctx(attackerSkill: 30, roll: 0f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_DeltaJustAboveDeadZone_RollZero_Crushes()
    {
        var result = _sut.DecideCrushThrough(Ctx(attackerSkill: 31, roll: 0f));

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void DecideCrushThrough_DeltaAtTargetDelta_ChanceIsExactlyMaxChance()
    {
        // Numerator and denominator of the curve share the same float shape, so delta == target
        // delta must produce chance == 0.5 EXACTLY: 0.499 crushes, 0.5 does not (strict <).
        var justBelow = _sut.DecideCrushThrough(Ctx(attackerSkill: 200, roll: 0.499f));
        var atChance = _sut.DecideCrushThrough(Ctx(attackerSkill: 200, roll: 0.5f));

        Assert.AreEqual(true, justBelow);
        Assert.IsNull(atChance);
    }

    [TestMethod]
    public void DecideCrushThrough_RollEqualsChance_ReturnsNull()
    {
        var result = _sut.DecideCrushThrough(Ctx(attackerSkill: 200, roll: 0.5f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_EnergyEqualsThreshold_ReturnsNull()
    {
        var result = _sut.DecideCrushThrough(Ctx(energy: 25f, roll: 0f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_EnergyAtFullRampPoint_FullEnergyFactor()
    {
        // threshold × (1 + rampMargin) = 25 × 1.27 = 31.75 → energy factor ≈ 1 (full chance 0.5).
        var result = _sut.DecideCrushThrough(Ctx(energy: 31.75f, attackerSkill: 200, roll: 0.499f));

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void DecideCrushThrough_NonOverheadSwing_HalvesChance()
    {
        // Full chance is 0.5; the non-overhead penalty halves it to 0.25 → a 0.3 roll flips.
        var overhead = _sut.DecideCrushThrough(Ctx(isOverhead: true, roll: 0.3f));
        var nonOverhead = _sut.DecideCrushThrough(Ctx(isOverhead: false, roll: 0.3f));

        Assert.AreEqual(true, overhead);
        Assert.IsNull(nonOverhead);
    }

    [TestMethod]
    public void DecideCrushThrough_AttackerRemovesNonOverheadPenalty_RestoresFullChance()
    {
        _resolver.Resolve((int?)9).Returns(new RaceCombatModifiers { RemoveNonOverheadPenalty = true });

        var result = _sut.DecideCrushThrough(Ctx(isOverhead: false, attackerRaceId: 9, roll: 0.3f));

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void DecideCrushThrough_SwingEnergyBonus_CrossesEnergyGate()
    {
        // Energy 22 alone fails the 25 gate; a 0.15 bonus factor lifts it to 25.3 and crosses.
        _resolver.Resolve((int?)5).Returns(new RaceCombatModifiers { SwingEnergyBonusFactor = 0.15f });

        var withBonus = _sut.DecideCrushThrough(Ctx(energy: 22f, attackerRaceId: 5, roll: 0f));
        var withoutBonus = _sut.DecideCrushThrough(Ctx(energy: 22f, attackerRaceId: null, roll: 0f));

        Assert.AreEqual(true, withBonus);
        Assert.IsNull(withoutBonus);
    }

    [TestMethod]
    public void DecideCrushThrough_MonsterAttackerNonShieldBlock_CrushesRegardlessOfSkillPath()
    {
        _settings.SkillCrushThroughEnabled.Returns(false);

        var result = _sut.DecideCrushThrough(Ctx(
            attackerMonsterId: "cave_troll",
            defendItemIsShield: false,
            isSwing: false,
            hasMeleeWeapon: false,
            roll: 1f));

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void DecideCrushThrough_MonsterAttackerShieldBlock_IneligibleSwing_ReturnsNull()
    {
        var result = _sut.DecideCrushThrough(Ctx(
            attackerMonsterId: "cave_troll",
            defendItemIsShield: true,
            isSwing: false));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_SettlementSuffixMonsterId_NormalizesAndCrushes()
    {
        var settlement = _sut.DecideCrushThrough(Ctx(
            attackerMonsterId: "cave_troll_settlement",
            isSwing: false,
            hasMeleeWeapon: false,
            roll: 1f));
        var settlementFast = _sut.DecideCrushThrough(Ctx(
            attackerMonsterId: "spider_settlement_fast",
            isSwing: false,
            hasMeleeWeapon: false,
            roll: 1f));

        Assert.AreEqual(true, settlement);
        Assert.AreEqual(true, settlementFast);
    }

    [TestMethod]
    public void DecideCrushThrough_AiOrcAttacker_CrushesShieldBlock()
    {
        SetAttackerRace(5, "orc");

        var result = _sut.DecideCrushThrough(Ctx(
            attackerRaceId: 5,
            isAiControlled: true,
            defendItemIsShield: true,
            roll: 0f));

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void DecideCrushThrough_AiOrcAttacker_SkipsNonOverheadPenalty()
    {
        // With the penalty the chance would be 0.25 and a 0.3 roll would miss; orc-qualified
        // attacks keep the full 0.5.
        SetAttackerRace(5, "orc");

        var result = _sut.DecideCrushThrough(Ctx(
            attackerRaceId: 5,
            isAiControlled: true,
            defendItemIsShield: true,
            isOverhead: false,
            roll: 0.3f));

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void DecideCrushThrough_PlayerControlledOrc_DoesNotQualify_ShieldBlockReturnsNull()
    {
        SetAttackerRace(5, "orc");

        var result = _sut.DecideCrushThrough(Ctx(
            attackerRaceId: 5,
            isAiControlled: false,
            defendItemIsShield: true,
            roll: 0f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_SkillToggleOff_OrcQualifiedStillCrushes()
    {
        _settings.SkillCrushThroughEnabled.Returns(false);
        SetAttackerRace(5, "orc");

        var result = _sut.DecideCrushThrough(Ctx(
            attackerRaceId: 5,
            isAiControlled: true,
            defendItemIsShield: true,
            roll: 0f));

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void DecideCrushThrough_InvalidAttackerRaceId_NeverLooksUpRaceName()
    {
        // Validate-before-lookup (Codex #33 class): an invalid id must never reach
        // GetRaceNameFromId, whose "human" fallback could otherwise qualify junk state.
        _raceManager.IsValidRaceId(42).Returns(false);

        var result = _sut.DecideCrushThrough(Ctx(
            attackerRaceId: 42,
            isAiControlled: true,
            defendItemIsShield: true,
            roll: 0f));

        Assert.IsNull(result);
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void DecideCrushThrough_PassiveUsage_ReturnsNull()
    {
        var result = _sut.DecideCrushThrough(Ctx(isPassiveUsage: true, roll: 0f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_ThrustStrike_ReturnsNull()
    {
        var result = _sut.DecideCrushThrough(Ctx(isSwing: false, roll: 0f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_NoMeleeWeapon_ReturnsNull()
    {
        var result = _sut.DecideCrushThrough(Ctx(hasMeleeWeapon: false, roll: 0f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_AllTogglesOff_ReturnsNull()
    {
        _settings.SkillCrushThroughEnabled.Returns(false);
        _settings.MonsterCrushThroughEnabled.Returns(false);
        _settings.OrcShieldCrushEnabled.Returns(false);

        var result = _sut.DecideCrushThrough(Ctx(
            attackerMonsterId: "cave_troll",
            defendItemIsShield: false,
            roll: 0f));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideCrushThrough_MonsterToggleOff_MonsterIdDoesNotCrush()
    {
        _settings.MonsterCrushThroughEnabled.Returns(false);

        var result = _sut.DecideCrushThrough(Ctx(
            attackerMonsterId: "cave_troll",
            defendItemIsShield: false,
            isSwing: false,
            roll: 1f));

        Assert.IsNull(result);
    }
}
