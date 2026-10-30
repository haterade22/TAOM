using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class ShieldBashKnockdownServiceTests
{
    private ICombatMechanicsConfigProvider _configProvider = null!;
    private ICombatMechanicsSettingsProvider _settings = null!;
    private IRaceCombatModifiersResolver _raceModifiers = null!;
    private CombatMechanicsConfig _config = null!;
    private ShieldBashKnockdownService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _raceModifiers = Substitute.For<IRaceCombatModifiersResolver>();

        _config = new CombatMechanicsConfig();
        _configProvider.GetConfig().Returns(_config);
        _settings.ShieldBashKnockdownEnabled.Returns(true);
        _raceModifiers.Resolve(Arg.Any<int?>()).Returns(RaceCombatModifiers.Neutral);

        _sut = CreateSut();
    }

    // Config is cached at construction; tests that change _config.ShieldBash build a fresh SUT.
    private ShieldBashKnockdownService CreateSut() => new ShieldBashKnockdownService(_configProvider, _settings, _raceModifiers);

    // Defaults: a shield bash by a man 50 Athletics ahead of his victim, who is neither player, immune nor of a resistant race
    // and has no ability, with a roll of 0 (the lowest, so every chance above 0 wins).
    private static ShieldBashContext Context(
        bool isAlternativeAttack = true,
        bool isHorseCharge = false,
        bool attackerWeaponIsNull = false,
        bool attackerWeaponIsShield = true,
        bool attackerIsHuman = true,
        bool victimIsImmune = false,
        int attackerAthletics = 100,
        int victimAthletics = 50,
        int? victimRaceId = null,
        float victimAbilityResistanceFactor = 1f,
        bool victimIsPlayer = false,
        float randomRoll = 0f,
        System.Func<float, float>? victimCareerResistanceScale = null)
        => new ShieldBashContext(isAlternativeAttack, isHorseCharge, attackerWeaponIsNull, attackerWeaponIsShield,
            attackerIsHuman, victimIsImmune, attackerAthletics, victimAthletics, victimRaceId, victimAbilityResistanceFactor,
            victimIsPlayer, randomRoll, victimCareerResistanceScale);

    // The chance is in percent; the roll is 0..1, so a roll of chance / 100 is exactly the boundary.
    private static float RollFor(float chancePercent) => chancePercent / 100f;

    private static RaceCombatModifiers Resistance(float multiplier) => new RaceCombatModifiers { KnockdownResistanceMultiplier = multiplier };

    // ---- gates ----

    [TestMethod]
    public void Decide_ToggleOff_ReturnsNull()
    {
        _settings.ShieldBashKnockdownEnabled.Returns(false);

        Assert.IsNull(_sut.Decide(Context()));
    }

    [TestMethod]
    public void Decide_NotAnAlternativeAttack_ReturnsNull()
    {
        Assert.IsNull(_sut.Decide(Context(isAlternativeAttack: false)));
    }

    [TestMethod]
    public void Decide_HorseCharge_ReturnsNull()
    {
        Assert.IsNull(_sut.Decide(Context(isHorseCharge: true)));
    }

    [TestMethod]
    public void Decide_BashWithANonShieldWeapon_ReturnsNull()
    {
        Assert.IsNull(_sut.Decide(Context(attackerWeaponIsNull: false, attackerWeaponIsShield: false)));
    }

    // The three deliberate immunities (creature stagger immunity, a live shrug-off ability, a hero career passive) arrive as
    // one verdict. The ShrugOff blow flag is not an input: vanilla sets it on nearly every bash and kick.
    [TestMethod]
    public void Decide_ImmuneVictim_ReturnsNull()
    {
        Assert.IsNull(_sut.Decide(Context(victimIsImmune: true)));
    }

    [TestMethod]
    public void Decide_NonHumanAttacker_ReturnsNull()
    {
        Assert.IsNull(_sut.Decide(Context(attackerIsHuman: false)));
        Assert.IsNull(_sut.Decide(Context(attackerIsHuman: false, attackerWeaponIsNull: true, attackerWeaponIsShield: false)));
    }

    [TestMethod]
    public void Decide_ShieldBashThatBeatsTheRoll_ReturnsTrue()
    {
        Assert.AreEqual(true, _sut.Decide(Context()));
    }

    [TestMethod]
    public void Decide_KickWithNoWeapon_IsEligible()
    {
        Assert.AreEqual(true, _sut.Decide(Context(attackerWeaponIsNull: true, attackerWeaponIsShield: false)));
    }

    // ---- the Athletics lead and the band ----

    [TestMethod]
    public void Decide_LeadInsideTheBand_UsesTheLeadAsThePercent()
    {
        // lead 40 -> 40%
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 90, victimAthletics: 50, randomRoll: RollFor(39.9f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 90, victimAthletics: 50, randomRoll: RollFor(40f))));
    }

    [TestMethod]
    public void Decide_LeadAboveTheCeiling_ClampsToTheMaxChance()
    {
        // lead 200 -> 75%
        var atCeiling = Context(attackerAthletics: 250, victimAthletics: 50, randomRoll: RollFor(74.9f));
        var pastCeiling = Context(attackerAthletics: 250, victimAthletics: 50, randomRoll: RollFor(75f));

        Assert.AreEqual(true, _sut.Decide(atCeiling));
        Assert.IsNull(_sut.Decide(pastCeiling));
    }

    [TestMethod]
    public void Decide_LeadAtTheCeiling_IsTheMaxChance()
    {
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 125, victimAthletics: 50, randomRoll: RollFor(74.9f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 125, victimAthletics: 50, randomRoll: RollFor(75f))));
    }

    [TestMethod]
    public void Decide_NegativeLead_ClampsToTheMinChance()
    {
        // lead -80 -> 10%
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 20, victimAthletics: 100, randomRoll: RollFor(9.9f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 20, victimAthletics: 100, randomRoll: RollFor(10f))));
    }

    [TestMethod]
    public void Decide_LeadAtTheFloor_IsTheMinChance()
    {
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 60, victimAthletics: 50, randomRoll: RollFor(9.9f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 60, victimAthletics: 50, randomRoll: RollFor(10f))));
    }

    [TestMethod]
    public void Decide_EqualSkills_IsTheMinChance()
    {
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 70, victimAthletics: 70, randomRoll: RollFor(10f))));
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 70, victimAthletics: 70, randomRoll: RollFor(9.9f))));
    }

    [TestMethod]
    public void Decide_ConfiguredBand_IsUsed()
    {
        _config.ShieldBash.MinChancePercent = 20f;
        _config.ShieldBash.MaxChancePercent = 30f;
        _sut = CreateSut();

        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 0, victimAthletics: 100, randomRoll: RollFor(19.9f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 0, victimAthletics: 100, randomRoll: RollFor(20f))));
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 300, victimAthletics: 0, randomRoll: RollFor(29.9f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 300, victimAthletics: 0, randomRoll: RollFor(30f))));
    }

    // ---- the roll ----

    [TestMethod]
    public void Decide_RollOfZero_WinsAnyPositiveChance()
    {
        Assert.AreEqual(true, _sut.Decide(Context(randomRoll: 0f)));
    }

    [TestMethod]
    public void Decide_RollJustUnderTheChance_Wins()
    {
        // lead 50 -> 50%
        Assert.AreEqual(true, _sut.Decide(Context(randomRoll: 0.4999f)));
    }

    [TestMethod]
    public void Decide_RollAtTheChance_Loses()
    {
        Assert.IsNull(_sut.Decide(Context(randomRoll: 0.5f)));
    }

    [TestMethod]
    public void Decide_RollAboveTheChance_Loses()
    {
        Assert.IsNull(_sut.Decide(Context(randomRoll: 0.9f)));
    }

    [TestMethod]
    public void Decide_RollOfOne_LosesEvenAtTheCeiling()
    {
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 300, victimAthletics: 0, randomRoll: 1f)));
    }

    [TestMethod]
    public void Decide_ZeroChanceBand_NeverWins()
    {
        _config.ShieldBash.MinChancePercent = 0f;
        _config.ShieldBash.MaxChancePercent = 0f;
        _sut = CreateSut();

        Assert.IsNull(_sut.Decide(Context(randomRoll: 0f)));
    }

    [TestMethod]
    public void Decide_NeverReturnsFalse()
    {
        foreach (var roll in new[] { 0f, 0.3f, 0.5f, 0.99f, 1f })
        {
            foreach (var weaponIsShield in new[] { true, false })
            {
                var verdict = _sut.Decide(Context(attackerWeaponIsShield: weaponIsShield, randomRoll: roll));
                Assert.AreNotEqual(false, verdict, $"roll {roll} shield {weaponIsShield}");
            }
        }
    }

    // ---- race resistance ----

    [TestMethod]
    public void Decide_VictimRace_IsResolvedFromTheContextRaceId()
    {
        _sut.Decide(Context(victimRaceId: 7));

        _raceModifiers.Received().Resolve(7);
    }

    [TestMethod]
    public void Decide_DwarfVictim_DividesTheChanceByTwoPointFive()
    {
        _raceModifiers.Resolve(3).Returns(Resistance(2.5f));

        // lead 50 -> 50% / 2.5 = 20%
        Assert.AreEqual(true, _sut.Decide(Context(victimRaceId: 3, randomRoll: RollFor(19.9f))));
        Assert.IsNull(_sut.Decide(Context(victimRaceId: 3, randomRoll: RollFor(20f))));
    }

    [TestMethod]
    public void Decide_TrollVictimAtTheFloor_IsTwoPointFivePercent()
    {
        _raceModifiers.Resolve(5).Returns(Resistance(4f));

        // the floor 10% / 4 = 2.5%
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 0, victimAthletics: 100, victimRaceId: 5, randomRoll: RollFor(2.4f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 0, victimAthletics: 100, victimRaceId: 5, randomRoll: RollFor(2.5f))));
    }

    [TestMethod]
    public void Decide_TrollVictimAtTheCeiling_IsEighteenPointSevenFivePercent()
    {
        _raceModifiers.Resolve(5).Returns(Resistance(4f));

        // the ceiling 75% / 4 = 18.75%
        Assert.AreEqual(true, _sut.Decide(Context(attackerAthletics: 300, victimAthletics: 0, victimRaceId: 5, randomRoll: RollFor(18.7f))));
        Assert.IsNull(_sut.Decide(Context(attackerAthletics: 300, victimAthletics: 0, victimRaceId: 5, randomRoll: RollFor(18.8f))));
    }

    [TestMethod]
    public void Decide_VictimWithResistanceBelowOne_RaisesTheChance()
    {
        _raceModifiers.Resolve(2).Returns(Resistance(0.5f));

        // 50% / 0.5 = 100%: only a roll of 1 or more loses
        Assert.AreEqual(true, _sut.Decide(Context(victimRaceId: 2, randomRoll: 0.99f)));
    }

    // As ChargeKnockdownService: a row of 0 means no resistance, so the chance divides to infinity and the man always falls.
    [TestMethod]
    public void Decide_ZeroRaceMultiplier_AlwaysFloors()
    {
        _raceModifiers.Resolve(4).Returns(Resistance(0f));

        Assert.AreEqual(true, _sut.Decide(Context(victimRaceId: 4, randomRoll: 0.999f)));
        Assert.AreEqual(true, _sut.Decide(Context(victimRaceId: 4, attackerAthletics: 0, victimAthletics: 100, randomRoll: 0.999f)));
    }

    // ---- a live race ability's knockdown resistance ----

    [TestMethod]
    public void Decide_AbilityResistanceOfTwo_HalvesTheChance()
    {
        // 50% / 2 = 25%
        Assert.AreEqual(true, _sut.Decide(Context(victimAbilityResistanceFactor: 2f, randomRoll: RollFor(24.9f))));
        Assert.IsNull(_sut.Decide(Context(victimAbilityResistanceFactor: 2f, randomRoll: RollFor(25f))));
    }

    [TestMethod]
    public void Decide_AbilityResistanceAndRaceMultiplier_BothDivide()
    {
        _raceModifiers.Resolve(3).Returns(Resistance(2.5f));

        // 50% / 2.5 / 2 = 10%
        Assert.AreEqual(true, _sut.Decide(Context(victimRaceId: 3, victimAbilityResistanceFactor: 2f, randomRoll: RollFor(9.9f))));
        Assert.IsNull(_sut.Decide(Context(victimRaceId: 3, victimAbilityResistanceFactor: 2f, randomRoll: RollFor(10f))));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(-2f)]
    public void Decide_UnusableAbilityFactor_CountsAsOne(float factor)
    {
        // 50% unchanged
        Assert.AreEqual(true, _sut.Decide(Context(victimAbilityResistanceFactor: factor, randomRoll: RollFor(49.9f))));
        Assert.IsNull(_sut.Decide(Context(victimAbilityResistanceFactor: factor, randomRoll: RollFor(50f))));
    }

    // ---- kicks ----

    [TestMethod]
    public void Decide_Kick_HalvesTheChance()
    {
        // lead 50 -> 50% x 0.5 = 25%
        var kick = Context(attackerWeaponIsNull: true, attackerWeaponIsShield: false, randomRoll: RollFor(24.9f));
        var kickAtChance = Context(attackerWeaponIsNull: true, attackerWeaponIsShield: false, randomRoll: RollFor(25f));

        Assert.AreEqual(true, _sut.Decide(kick));
        Assert.IsNull(_sut.Decide(kickAtChance));
    }

    [TestMethod]
    public void Decide_ShieldBashAtTheKickRoll_IsNotHalved()
    {
        Assert.AreEqual(true, _sut.Decide(Context(randomRoll: RollFor(25f))));
    }

    [TestMethod]
    public void Decide_KickFactorOfZero_NeverKnocksDown()
    {
        _config.ShieldBash.KickChanceFactor = 0f;
        _sut = CreateSut();

        Assert.IsNull(_sut.Decide(Context(attackerWeaponIsNull: true, attackerWeaponIsShield: false, randomRoll: 0f)));
        Assert.AreEqual(true, _sut.Decide(Context(randomRoll: 0f)));
    }

    // The factor is applied after the clamp, so it scales the floor too: 10% x 0.5 = 5%.
    [TestMethod]
    public void Decide_KickAtTheFloor_IsScaledBelowTheMinChance()
    {
        var kick = Context(attackerAthletics: 0, victimAthletics: 100, attackerWeaponIsNull: true, attackerWeaponIsShield: false);

        Assert.AreEqual(true, _sut.Decide(WithRoll(kick, RollFor(4.9f))));
        Assert.IsNull(_sut.Decide(WithRoll(kick, RollFor(5f))));
    }

    // ---- the player as victim ----

    [TestMethod]
    public void Decide_PlayerVictim_HalvesTheChance()
    {
        Assert.AreEqual(true, _sut.Decide(Context(victimIsPlayer: true, randomRoll: RollFor(24.9f))));
        Assert.IsNull(_sut.Decide(Context(victimIsPlayer: true, randomRoll: RollFor(25f))));
    }

    [TestMethod]
    public void Decide_NonPlayerVictim_IsNotHalved()
    {
        Assert.AreEqual(true, _sut.Decide(Context(victimIsPlayer: false, randomRoll: RollFor(25f))));
    }

    [TestMethod]
    public void Decide_PlayerFactorOfZero_NeverKnocksThePlayerDown()
    {
        _config.ShieldBash.PlayerVictimChanceFactor = 0f;
        _sut = CreateSut();

        Assert.IsNull(_sut.Decide(Context(victimIsPlayer: true, randomRoll: 0f)));
    }

    [TestMethod]
    public void Decide_PlayerFactorOfOne_TreatsThePlayerLikeAnyoneElse()
    {
        _config.ShieldBash.PlayerVictimChanceFactor = 1f;
        _sut = CreateSut();

        Assert.AreEqual(true, _sut.Decide(Context(victimIsPlayer: true, randomRoll: RollFor(49.9f))));
    }

    [TestMethod]
    public void Decide_KickOnThePlayer_AppliesBothFactors()
    {
        // 50% x 0.5 x 0.5 = 12.5%
        var kick = Context(attackerWeaponIsNull: true, attackerWeaponIsShield: false, victimIsPlayer: true);

        Assert.AreEqual(true, _sut.Decide(WithRoll(kick, RollFor(12.4f))));
        Assert.IsNull(_sut.Decide(WithRoll(kick, RollFor(12.5f))));
    }

    [TestMethod]
    public void Decide_DwarfPlayerHitByAKick_AppliesRaceAndBothFactors()
    {
        _raceModifiers.Resolve(3).Returns(Resistance(2.5f));

        // 50% / 2.5 = 20% x 0.5 x 0.5 = 5%
        var kick = Context(attackerWeaponIsNull: true, attackerWeaponIsShield: false, victimIsPlayer: true, victimRaceId: 3);

        Assert.AreEqual(true, _sut.Decide(WithRoll(kick, RollFor(4.9f))));
        Assert.IsNull(_sut.Decide(WithRoll(kick, RollFor(5f))));
    }

    // ---- the career knockdown counter (#769 E10) ----

    // The scale runs only once every gate has passed, so the career counter it feeds never counts a bash Hammerhand ignores.
    [TestMethod]
    public void Decide_CareerScale_DividesTheChanceOfAnEligibleRoll()
    {
        _config.ShieldBash.MinChancePercent = 0f;
        _config.ShieldBash.MaxChancePercent = 100f;
        _sut = CreateSut();
        var calls = 0;
        System.Func<float, float> doubled = f => { calls++; return f * 2f; };

        // 100 against 50 Athletics: a 50% chance, halved to 25% by the scale.
        Assert.AreEqual(true, _sut.Decide(Context(randomRoll: RollFor(30f))));
        Assert.IsNull(_sut.Decide(Context(randomRoll: RollFor(30f), victimCareerResistanceScale: doubled)));
        Assert.AreEqual(true, _sut.Decide(Context(randomRoll: RollFor(24.9f), victimCareerResistanceScale: doubled)));
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void Decide_ToggleOff_NeverAsksTheCareerScale()
    {
        _settings.ShieldBashKnockdownEnabled.Returns(false);
        var calls = 0;

        Assert.IsNull(_sut.Decide(Context(victimCareerResistanceScale: f => { calls++; return f; })));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void Decide_ImmuneVictim_NeverAsksTheCareerScale()
    {
        var calls = 0;

        Assert.IsNull(_sut.Decide(Context(victimIsImmune: true, victimCareerResistanceScale: f => { calls++; return f; })));
        Assert.AreEqual(0, calls);
    }

    // The strict < already refuses a NaN roll; MBRandom.RandomFloat is finite, so this row only pins the polarity.
    [TestMethod]
    public void Decide_NaNRoll_ReturnsNull()
    {
        Assert.IsNull(_sut.Decide(Context(randomRoll: float.NaN)));
    }

    [TestMethod]
    public void Constructor_NullShieldBashSection_UsesTheDefaults()
    {
        _config.ShieldBash = null!;
        _sut = CreateSut();

        Assert.AreEqual(true, _sut.Decide(Context(randomRoll: RollFor(49.9f))));
        Assert.IsNull(_sut.Decide(Context(randomRoll: RollFor(50f))));
    }

    private static ShieldBashContext WithRoll(ShieldBashContext c, float roll)
        => new ShieldBashContext(c.IsAlternativeAttack, c.IsHorseCharge, c.AttackerWeaponIsNull, c.AttackerWeaponIsShield,
            c.AttackerIsHuman, c.VictimIsImmune, c.AttackerAthletics, c.VictimAthletics, c.VictimRaceId, c.VictimAbilityResistanceFactor,
            c.VictimIsPlayer, roll, c.VictimCareerResistanceScale);
}
