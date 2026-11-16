using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The pure decisions behind a signature strike. Every input is a primitive the boundary read off
/// the engine's <c>AttackCollisionData</c>, so each rejection here is one engine state the feature
/// must stay silent on: a parry, a kick, a throw, a horse charge, a hit that is still inside the
/// cooldown. The verdicts share the cooldown with the ring on purpose: a slam is ONE package
/// (guaranteed knockdown + ring + fear) at most once per cooldown, and every other overhead is a
/// vanilla hit. Runs against the compiled defaults: Sauron at index 0, the Nine at index 1.
/// </summary>
[TestClass]
public class SignatureStrikeServiceTests
{
    private SignatureStrikesConfig _config = null!;
    private ISignatureStrikesConfigProvider _configProvider = null!;
    private ISignatureStrikesSettingsProvider _settings = null!;
    private SignatureStrikeService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new SignatureStrikesConfig();
        _configProvider = Substitute.For<ISignatureStrikesConfigProvider>();
        _configProvider.GetConfig().Returns(_ => _config);
        _settings = Substitute.For<ISignatureStrikesSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.CooldownMultiplier.Returns(1f);
        _sut = new SignatureStrikeService(_configProvider, _settings);
    }

    // Positions in the compiled default Signatures list, the one the service indexes.
    private const int Sauron = 0;
    private const int Nazgul = 1;

    private static StrikeKindTimes Struck(StrikeKind kind, float at) => default(StrikeKindTimes).With(kind, at);

    // A clean overhead strike by Sauron on an unmounted human, well outside every cooldown.
    private static StrikeContext Overhead() => new StrikeContext(
        IsSignatureAttacker: true,
        SignatureIndex: Sauron,
        Direction: StrikeDirection.Overhead,
        Collision: StrikeCollision.StrikeAgent,
        IsCanceled: false,
        IsColliderAgent: true,
        IsAlternativeAttack: false,
        IsMissile: false,
        IsHorseCharge: false,
        AttackBlockedWithShield: false,
        HasMeleeWeapon: true,
        VictimIsHuman: true,
        VictimIsMounted: false,
        HasShrugOff: false,
        InflictedDamage: 100,
        MissionTime: 30f,
        LastStrikeTimes: default);

    private static StrikeContext Left() => Overhead() with { Direction = StrikeDirection.Left };

    private static StrikeContext NazgulOverhead() => Overhead() with { SignatureIndex = Nazgul };

    // ---- The Nine's SCREAM (#645) ---------------------------------------------------------------

    [TestMethod]
    public void Evaluate_NazgulOverhead_ReturnsAScreamAroundTheWraith()
    {
        var effect = _sut.Evaluate(NazgulOverhead());

        Assert.IsTrue(effect.HasValue);
        Assert.AreEqual(StrikeKind.Scream, effect!.Value.Kind);
        Assert.AreEqual(StrikeOrigin.Self, effect.Value.Origin);
        Assert.AreEqual(100, effect.Value.DamageBasis);
        Assert.AreEqual(6f, effect.Value.OuterRadius, 0.001f);
        Assert.AreEqual(2.5f, effect.Value.InnerRadius, 0.001f);
        Assert.AreEqual(0.3f, effect.Value.DamageFraction, 0.001f);
        Assert.AreEqual(25f, effect.Value.FearMorale, 0.001f);
        Assert.IsTrue(effect.Value.KnockBack);
        Assert.IsFalse(effect.Value.KnockDown);
        Assert.AreEqual("LOTR/Mordor/Nazgul/nazgul_scream", effect.Value.Sound);
        Assert.AreEqual("nazgul", effect.Value.SignatureId);
    }

    [TestMethod]
    public void Evaluate_NazgulSideSwings_AlsoScream()
    {
        Assert.AreEqual(StrikeKind.Scream, _sut.Evaluate(NazgulOverhead() with { Direction = StrikeDirection.Left })!.Value.Kind);
        Assert.AreEqual(StrikeKind.Scream, _sut.Evaluate(NazgulOverhead() with { Direction = StrikeDirection.Right })!.Value.Kind);
    }

    [TestMethod]
    public void Evaluate_NazgulThrust_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(NazgulOverhead() with { Direction = StrikeDirection.Thrust }));
    }

    [TestMethod]
    public void Evaluate_NazgulSideSwingInsideTheCooldownOfAnOverheadScream_ReturnsNull()
    {
        // One timer for all three directions: the overhead scream 10 s ago holds the side swing.
        Assert.IsNull(_sut.Evaluate(NazgulOverhead() with
        {
            Direction = StrikeDirection.Left,
            LastStrikeTimes = Struck(StrikeKind.Scream, 20f),
            MissionTime = 30f,
        }));
    }

    [TestMethod]
    public void Evaluate_NazgulExactlyAtTheScreamCooldown_ReturnsEffect()
    {
        Assert.IsNotNull(_sut.Evaluate(NazgulOverhead() with { LastStrikeTimes = Struck(StrikeKind.Scream, 15f), MissionTime = 30f }));
    }

    [TestMethod]
    public void Evaluate_ScreamCooldownIgnoresTheOtherKindsStamps()
    {
        // A stamp under another kind is not this signature's timer.
        Assert.IsNotNull(_sut.Evaluate(NazgulOverhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, 29f), MissionTime = 30f }));
    }

    [TestMethod]
    public void Evaluate_NazgulOverheadIntoTheGround_ReturnsNull()
    {
        // The scream has no world-hit basis: it answers a hit on a foe, not on the ground.
        Assert.IsNull(_sut.Evaluate(NazgulOverhead() with
        {
            Collision = StrikeCollision.HitWorld,
            IsColliderAgent = false,
            InflictedDamage = 0,
        }));
    }

    [TestMethod]
    public void DecideKnockdown_Scream_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(NazgulOverhead()));
    }

    [TestMethod]
    public void DecideKnockback_ScreamOnUnmountedHuman_ReturnsTrue()
    {
        Assert.AreEqual(true, _sut.DecideKnockback(NazgulOverhead()));
        Assert.AreEqual(true, _sut.DecideKnockback(NazgulOverhead() with { Direction = StrikeDirection.Right }));
    }

    [TestMethod]
    public void DecideKnockback_ScreamInsideTheCooldown_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockback(NazgulOverhead() with { LastStrikeTimes = Struck(StrikeKind.Scream, 20f), MissionTime = 30f }));
    }

    [TestMethod]
    public void Evaluate_SauronSlam_KeepsItsOwnIdentity()
    {
        var effect = _sut.Evaluate(Overhead());

        Assert.AreEqual("sauron", effect!.Value.SignatureId);
        Assert.AreEqual(StrikeOrigin.Impact, effect.Value.Origin);
        Assert.IsNull(effect.Value.Sound);
    }

    [TestMethod]
    public void Evaluate_ProfileWithUnknownOrigin_ReturnsNull()
    {
        // The three names the service parses fail closed alike: a ring centred on the wrong point
        // is worse than no ring. The provider already reverts an unknown origin; this pins the
        // service for any config that did not come through it.
        _config.Signatures[0].Strikes["Overhead"].Origin = "Sky";

        Assert.IsNull(_sut.Evaluate(Overhead()));
    }

    [TestMethod]
    public void Evaluate_SignatureIndexOutOfRange_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { SignatureIndex = 4 }));
        Assert.IsNull(_sut.Evaluate(Overhead() with { SignatureIndex = -1 }));
    }

    [TestMethod]
    public void Evaluate_NoSignaturesConfigured_ReturnsNull()
    {
        _config.Signatures = new System.Collections.Generic.List<SignatureConfig>();

        Assert.IsNull(_sut.Evaluate(Overhead()));
    }

    // ---- Evaluate: direction mapping ------------------------------------------------------

    [TestMethod]
    public void Evaluate_OverheadStrike_ReturnsSlamBasedOnTheInflictedDamage()
    {
        var effect = _sut.Evaluate(Overhead());

        Assert.IsTrue(effect.HasValue);
        Assert.AreEqual(StrikeKind.Slam, effect!.Value.Kind);
        Assert.AreEqual(100, effect.Value.DamageBasis);
        Assert.AreEqual(4f, effect.Value.OuterRadius, 0.001f);
        Assert.AreEqual(1.5f, effect.Value.InnerRadius, 0.001f);
        Assert.AreEqual(0.6f, effect.Value.DamageFraction, 0.001f);
        Assert.IsTrue(effect.Value.KnockDown);
        Assert.IsFalse(effect.Value.KnockBack);
        Assert.AreEqual(15f, effect.Value.FearMorale, 0.001f);
    }

    [TestMethod]
    public void Evaluate_LeftSwing_ReturnsSweep()
    {
        var effect = _sut.Evaluate(Left());

        Assert.IsTrue(effect.HasValue);
        Assert.AreEqual(StrikeKind.Sweep, effect!.Value.Kind);
        Assert.IsTrue(effect.Value.KnockBack);
        Assert.IsFalse(effect.Value.KnockDown);
        Assert.AreEqual(0f, effect.Value.FearMorale, 0.001f);
    }

    [TestMethod]
    public void Evaluate_RightSwing_ReturnsSweep()
    {
        var effect = _sut.Evaluate(Overhead() with { Direction = StrikeDirection.Right });

        Assert.AreEqual(StrikeKind.Sweep, effect!.Value.Kind);
    }

    [TestMethod]
    public void Evaluate_Thrust_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { Direction = StrikeDirection.Thrust }));
    }

    [TestMethod]
    public void Evaluate_NoDirection_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { Direction = StrikeDirection.None }));
    }

    // ---- Evaluate: rejections ---------------------------------------------------------------

    [TestMethod]
    public void Evaluate_NotASignatureAttacker_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { IsSignatureAttacker = false }));
    }

    [TestMethod]
    public void Evaluate_FeatureDisabled_ReturnsNull()
    {
        _settings.IsEnabled.Returns(false);

        Assert.IsNull(_sut.Evaluate(Overhead()));
    }

    [TestMethod]
    public void Evaluate_CanceledHit_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { IsCanceled = true }));
    }

    [TestMethod]
    public void Evaluate_KickOrBash_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { IsAlternativeAttack = true }));
    }

    [TestMethod]
    public void Evaluate_MissileHit_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { IsMissile = true }));
    }

    [TestMethod]
    public void Evaluate_NoMeleeWeaponInHand_ReturnsNull()
    {
        // Bare hands, a bow used as a club, a thrown weapon swung in melee mode.
        Assert.IsNull(_sut.Evaluate(Overhead() with { HasMeleeWeapon = false }));
    }

    [TestMethod]
    public void Evaluate_HorseCharge_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { IsHorseCharge = true }));
    }

    [TestMethod]
    public void Evaluate_Parried_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { Collision = StrikeCollision.Parried, IsCanceled = true }));
    }

    [TestMethod]
    public void Evaluate_ChamberBlocked_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { Collision = StrikeCollision.ChamberBlocked, IsCanceled = true }));
    }

    [TestMethod]
    public void Evaluate_BlockedWithAWeapon_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { Collision = StrikeCollision.Blocked, AttackBlockedWithShield = false }));
    }

    [TestMethod]
    public void Evaluate_ZeroDamageStrike_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { InflictedDamage = 0 }));
    }

    [TestMethod]
    public void Evaluate_NoCollision_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { Collision = StrikeCollision.None }));
    }

    // ---- Evaluate: damage basis -------------------------------------------------------------

    [TestMethod]
    public void Evaluate_ShieldBlockThatDamagesTheShield_UsesAQuarterOfTheShieldDamage()
    {
        var effect = _sut.Evaluate(Overhead() with
        {
            Collision = StrikeCollision.Blocked,
            AttackBlockedWithShield = true,
            InflictedDamage = 100,
        });

        Assert.AreEqual(25, effect!.Value.DamageBasis);
    }

    [TestMethod]
    public void Evaluate_ShieldBlockBasisPastIntRange_ReturnsNull()
    {
        // Codex review 114, O2: the ring cast was guarded, the shield-basis cast was not. Both
        // now go through one RoundToDamage.
        _config.ShieldBlockedMultiplier = 1f;

        Assert.IsNull(_sut.Evaluate(Overhead() with
        {
            Collision = StrikeCollision.Blocked,
            AttackBlockedWithShield = true,
            InflictedDamage = int.MaxValue,
        }));
    }

    [TestMethod]
    public void Evaluate_ShieldBlockWithZeroShieldDamage_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with
        {
            Collision = StrikeCollision.Blocked,
            AttackBlockedWithShield = true,
            InflictedDamage = 0,
        }));
    }

    [TestMethod]
    public void Evaluate_OverheadIntoTheGround_UsesTheWorldHitBaseDamage()
    {
        var effect = _sut.Evaluate(Overhead() with
        {
            Collision = StrikeCollision.HitWorld,
            IsColliderAgent = false,
            InflictedDamage = 0,
            VictimIsHuman = false,
        });

        Assert.IsTrue(effect.HasValue);
        Assert.AreEqual(60, effect!.Value.DamageBasis);
    }

    [TestMethod]
    public void Evaluate_SideSwingIntoTheGround_ReturnsNullBecauseSweepHasNoWorldHitDamage()
    {
        Assert.IsNull(_sut.Evaluate(Left() with
        {
            Collision = StrikeCollision.HitWorld,
            IsColliderAgent = false,
            InflictedDamage = 0,
        }));
    }

    // ---- Evaluate: cooldown -----------------------------------------------------------------

    [TestMethod]
    public void Evaluate_InsideTheSlamCooldown_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, 20f), MissionTime = 30f }));
    }

    [TestMethod]
    public void Evaluate_ExactlyAtTheSlamCooldown_ReturnsEffect()
    {
        Assert.IsNotNull(_sut.Evaluate(Overhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, 10f), MissionTime = 30f }));
    }

    [TestMethod]
    public void Evaluate_SweepCooldownIsIndependentOfTheSlamCooldown()
    {
        // A slam a second ago does not gate a sweep.
        Assert.IsNotNull(_sut.Evaluate(Left() with { LastStrikeTimes = Struck(StrikeKind.Slam, 29f), MissionTime = 30f }));
    }

    [TestMethod]
    public void Evaluate_InsideTheSweepCooldown_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Left() with { LastStrikeTimes = Struck(StrikeKind.Sweep, 25f), MissionTime = 30f }));
    }

    [TestMethod]
    public void Evaluate_CooldownMultiplierScalesTheCooldown()
    {
        _settings.CooldownMultiplier.Returns(2f);

        Assert.IsNull(_sut.Evaluate(Overhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, 0f), MissionTime = 30f }));
        Assert.IsNotNull(_sut.Evaluate(Overhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, 0f), MissionTime = 40f }));
    }

    [TestMethod]
    public void Evaluate_NaNMissionTime_ReturnsNull()
    {
        Assert.IsNull(_sut.Evaluate(Overhead() with { MissionTime = float.NaN }));
    }

    [TestMethod]
    public void Evaluate_NaNLastStrikeTime_MeansNeverStruck()
    {
        Assert.IsNotNull(_sut.Evaluate(Overhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, float.NaN), MissionTime = 0f }));
    }

    [TestMethod]
    public void Evaluate_InfiniteLastStrikeTime_MeansNeverStruck()
    {
        Assert.IsNotNull(_sut.Evaluate(Overhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, float.PositiveInfinity), MissionTime = 0f }));
    }

    // ---- DecideKnockdown ---------------------------------------------------------------------

    [TestMethod]
    public void DecideKnockdown_SignatureOverheadOnUnmountedHuman_ReturnsTrue()
    {
        Assert.AreEqual(true, _sut.DecideKnockdown(Overhead()));
    }

    [TestMethod]
    public void DecideKnockdown_SideSwing_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Left()));
    }

    [TestMethod]
    public void DecideKnockdown_NotASignatureAttacker_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { IsSignatureAttacker = false }));
    }

    [TestMethod]
    public void DecideKnockdown_FeatureDisabled_ReturnsNull()
    {
        _settings.IsEnabled.Returns(false);

        Assert.IsNull(_sut.DecideKnockdown(Overhead()));
    }

    [TestMethod]
    public void DecideKnockdown_HorseCharge_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { IsHorseCharge = true }));
    }

    [TestMethod]
    public void DecideKnockdown_WorldHit_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { Collision = StrikeCollision.HitWorld, IsColliderAgent = false }));
    }

    [TestMethod]
    public void DecideKnockdown_ShieldBlock_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { Collision = StrikeCollision.Blocked, AttackBlockedWithShield = true }));
    }

    [TestMethod]
    public void DecideKnockdown_NotAColliderAgent_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { IsColliderAgent = false }));
    }

    [TestMethod]
    public void DecideKnockdown_VictimNotHuman_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { VictimIsHuman = false }));
    }

    [TestMethod]
    public void DecideKnockdown_VictimMounted_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { VictimIsMounted = true }));
    }

    [TestMethod]
    public void DecideKnockdown_ShruggedOff_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { HasShrugOff = true }));
    }

    [TestMethod]
    public void DecideKnockdown_ZeroDamage_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { InflictedDamage = 0 }));
    }

    [TestMethod]
    public void DecideKnockdown_KickOrBash_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { IsAlternativeAttack = true }));
    }

    [TestMethod]
    public void DecideKnockdown_NoMeleeWeapon_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { HasMeleeWeapon = false }));
    }

    [TestMethod]
    public void DecideKnockdown_InsideTheSlamCooldown_ReturnsNull()
    {
        // The guaranteed knockdown is part of the slam package, not a permanent buff.
        Assert.IsNull(_sut.DecideKnockdown(Overhead() with { LastStrikeTimes = Struck(StrikeKind.Slam, 20f), MissionTime = 30f }));
    }

    // ---- DecideKnockback ---------------------------------------------------------------------

    [TestMethod]
    public void DecideKnockback_SignatureLeftSwingOnUnmountedHuman_ReturnsTrue()
    {
        Assert.AreEqual(true, _sut.DecideKnockback(Left()));
    }

    [TestMethod]
    public void DecideKnockback_SignatureRightSwing_ReturnsTrue()
    {
        Assert.AreEqual(true, _sut.DecideKnockback(Overhead() with { Direction = StrikeDirection.Right }));
    }

    [TestMethod]
    public void DecideKnockback_Overhead_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockback(Overhead()));
    }

    [TestMethod]
    public void DecideKnockback_VictimMounted_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockback(Left() with { VictimIsMounted = true }));
    }

    [TestMethod]
    public void DecideKnockback_ShruggedOff_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockback(Left() with { HasShrugOff = true }));
    }

    [TestMethod]
    public void DecideKnockback_HorseCharge_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockback(Left() with { IsHorseCharge = true }));
    }

    [TestMethod]
    public void DecideKnockback_InsideTheSweepCooldown_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockback(Left() with { LastStrikeTimes = Struck(StrikeKind.Sweep, 25f), MissionTime = 30f }));
    }

    [TestMethod]
    public void DecideKnockback_NotASignatureAttacker_ReturnsNull()
    {
        Assert.IsNull(_sut.DecideKnockback(Left() with { IsSignatureAttacker = false }));
    }

    // ---- ComputeRingDamage ------------------------------------------------------------------

    [TestMethod]
    public void ComputeRingDamage_FullFalloff_IsBasisTimesFraction()
    {
        Assert.AreEqual(60, _sut.ComputeRingDamage(100, 0.6f, 1f, victimBlocking: false));
    }

    [TestMethod]
    public void ComputeRingDamage_EdgeFalloff_RoundsToNearest()
    {
        // 100 * 0.6 / 9 = 6.67 -> 7
        Assert.AreEqual(7, _sut.ComputeRingDamage(100, 0.6f, 1f / 9f, victimBlocking: false));
    }

    [TestMethod]
    public void ComputeRingDamage_ShieldBlockingVictim_TakesTheBlockedMultiplier()
    {
        Assert.AreEqual(15, _sut.ComputeRingDamage(100, 0.6f, 1f, victimBlocking: true));
    }

    [TestMethod]
    public void ComputeRingDamage_ZeroFalloff_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.ComputeRingDamage(100, 0.6f, 0f, victimBlocking: false));
    }

    [TestMethod]
    public void ComputeRingDamage_NaNFalloff_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.ComputeRingDamage(100, 0.6f, float.NaN, victimBlocking: false));
    }

    [TestMethod]
    public void ComputeRingDamage_NaNFraction_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.ComputeRingDamage(100, float.NaN, 1f, victimBlocking: false));
    }

    [TestMethod]
    public void ComputeRingDamage_NegativeBasis_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.ComputeRingDamage(-5, 0.6f, 1f, victimBlocking: false));
    }

    [TestMethod]
    public void ComputeRingDamage_ProductPastIntRange_ReturnsZero()
    {
        // (int) of a float at or past int.MaxValue is int.MinValue on net472; guard the cast.
        Assert.AreEqual(0, _sut.ComputeRingDamage(int.MaxValue, 1f, 1f, victimBlocking: false));
    }

    // ---- Instant kill (#1359) ---------------------------------------------------------------

    private const int Aragorn = 2;
    private const int Gimli = 3;

    [TestMethod]
    public void Evaluate_CleanHitWithKillChance_CarriesChance()
    {
        Assert.AreEqual(0.15f, _sut.Evaluate(Overhead() with { SignatureIndex = Aragorn })!.Value.KillChance, 0.0001f);
        Assert.AreEqual(0.35f, _sut.Evaluate(Overhead() with { SignatureIndex = Gimli })!.Value.KillChance, 0.0001f);
    }

    [TestMethod]
    public void Evaluate_ProfileWithoutKillChance_CarriesZero()
    {
        Assert.AreEqual(0f, _sut.Evaluate(Overhead())!.Value.KillChance, 0.0001f);
    }

    [TestMethod]
    public void Evaluate_ShieldBlocked_KillChanceZero()
    {
        var effect = _sut.Evaluate(Overhead() with
        {
            SignatureIndex = Gimli,
            Collision = StrikeCollision.Blocked,
            AttackBlockedWithShield = true,
        });

        Assert.AreEqual(0f, effect!.Value.KillChance, 0.0001f);
    }

    [TestMethod]
    public void Evaluate_HitWorld_KillChanceZero()
    {
        _config.Signatures[Gimli].Strikes["Overhead"].WorldHitBaseDamage = 40;

        var effect = _sut.Evaluate(Overhead() with { SignatureIndex = Gimli, Collision = StrikeCollision.HitWorld });

        Assert.AreEqual(0f, effect!.Value.KillChance, 0.0001f);
    }

    [TestMethod]
    public void Evaluate_VictimNotHuman_KillChanceZero()
    {
        var effect = _sut.Evaluate(Overhead() with { SignatureIndex = Gimli, VictimIsHuman = false });

        Assert.AreEqual(0f, effect!.Value.KillChance, 0.0001f);
    }

    private bool Kill(
        float chance = 0.5f, float roll = 0.1f, bool human = true, bool hero = false, bool player = false,
        bool general = false, bool bearer = false, bool immune = false, bool shrugging = false, float health = 50f)
        => _sut.DecideInstantKill(chance, roll, human, hero, player, general, bearer, immune, shrugging, health);

    [TestMethod]
    public void DecideInstantKill_RollBelowChance_True() => Assert.IsTrue(Kill());

    [TestMethod]
    public void DecideInstantKill_RollAtChance_False() => Assert.IsFalse(Kill(chance: 0.5f, roll: 0.5f));

    [TestMethod]
    public void DecideInstantKill_NotHuman_False() => Assert.IsFalse(Kill(human: false));

    [TestMethod]
    public void DecideInstantKill_Hero_False() => Assert.IsFalse(Kill(hero: true));

    [TestMethod]
    public void DecideInstantKill_Player_False() => Assert.IsFalse(Kill(player: true));

    [TestMethod]
    public void DecideInstantKill_General_False() => Assert.IsFalse(Kill(general: true));

    [TestMethod]
    public void DecideInstantKill_SignatureBearer_False() => Assert.IsFalse(Kill(bearer: true));

    [TestMethod]
    public void DecideInstantKill_ImmuneRace_False() => Assert.IsFalse(Kill(immune: true));

    [TestMethod]
    public void DecideInstantKill_ShruggingOff_False() => Assert.IsFalse(Kill(shrugging: true));

    [TestMethod]
    public void DecideInstantKill_NaNChance_False() => Assert.IsFalse(Kill(chance: float.NaN));

    [TestMethod]
    public void DecideInstantKill_NaNRoll_False() => Assert.IsFalse(Kill(roll: float.NaN));

    [TestMethod]
    public void DecideInstantKill_ZeroChance_False() => Assert.IsFalse(Kill(chance: 0f, roll: -1f));

    [TestMethod]
    public void DecideInstantKill_ZeroHealth_False() => Assert.IsFalse(Kill(health: 0f));

    [TestMethod]
    public void DecideInstantKill_NaNHealth_False() => Assert.IsFalse(Kill(health: float.NaN));

    [TestMethod]
    public void InstantKillDamage_ExceedsHealth()
    {
        Assert.AreEqual(51, _sut.InstantKillDamage(50f));
        Assert.AreEqual(52, _sut.InstantKillDamage(50.2f));
    }

    [TestMethod]
    public void InstantKillDamage_Huge_Zero()
    {
        Assert.AreEqual(0, _sut.InstantKillDamage(3e9f));
        Assert.AreEqual(0, _sut.InstantKillDamage(float.PositiveInfinity));
    }

    [TestMethod]
    public void InstantKillDamage_NaN_Zero() => Assert.AreEqual(0, _sut.InstantKillDamage(float.NaN));

    [TestMethod]
    public void InstantKillDamage_NotPositive_Zero()
    {
        Assert.AreEqual(0, _sut.InstantKillDamage(0f));
        Assert.AreEqual(0, _sut.InstantKillDamage(-4f));
    }
}
