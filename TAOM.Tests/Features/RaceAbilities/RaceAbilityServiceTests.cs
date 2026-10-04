using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

// The pure decisions behind the race abilities: when one fires, how hard it hits for a soldier's tier,
// and what it does to a crush-through, a hit's damage and a resistance. Engine floats can be NaN, so every
// gate is pinned to fail closed (csharp-architecture.md "Engine-Float Decision Gates").

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityServiceTests
{
    private RaceAbilityService _sut = null!;
    private RaceAbilitySenses _senses = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new RaceAbilityService();
        _senses = new RaceAbilitySenses { Now = 100f, HealthFraction = 1f };
    }

    private static RaceAbilityTrigger T(RaceAbilityTriggerKind kind, float range = 0f, float seconds = 0f,
        float fraction = 0f, int count = 0) =>
        new RaceAbilityTrigger { ParsedKind = kind, Kind = kind.ToString(), Range = range, Seconds = seconds, Fraction = fraction, Count = count };

    private bool Holds(RaceAbilityTrigger trigger) =>
        _sut.FiringTrigger(new RaceAbilityProfile { AnyOf = { trigger } }, _senses).HasValue;

    private bool Fires(RaceAbilityProfile profile) => _sut.FiringTrigger(profile, _senses).HasValue;

    // --- cooldown ---

    // --- took damage, sampled once a decision ---

    [DataTestMethod]
    [DataRow(100f, 90f, true)]
    [DataRow(100f, 100f, false)]
    [DataRow(90f, 100f, false)]          // healed
    [DataRow(-1f, 50f, false)]           // the first decision has nothing to compare
    [DataRow(float.NaN, 50f, false)]
    [DataRow(100f, float.NaN, false)]
    public void TookDamage_ComparesWithThePreviousDecision(float lastHealth, float health, bool expected) =>
        Assert.AreEqual(expected, RaceAbilityService.TookDamage(lastHealth, health));

    [TestMethod]
    public void IsOffCooldown_NeverFired_IsTrue() => Assert.IsTrue(_sut.IsOffCooldown(null, 100f, 15f));

    [TestMethod]
    public void IsOffCooldown_ExactlyTheCooldownLater_IsTrue() => Assert.IsTrue(_sut.IsOffCooldown(85f, 100f, 15f));

    [TestMethod]
    public void IsOffCooldown_InsideTheCooldown_IsFalse() => Assert.IsFalse(_sut.IsOffCooldown(85.5f, 100f, 15f));

    [TestMethod]
    public void IsOffCooldown_StampInTheFuture_IsFalse() => Assert.IsFalse(_sut.IsOffCooldown(120f, 100f, 15f));

    [DataTestMethod]
    [DataRow(float.NaN, 100f)]
    [DataRow(85f, float.NaN)]
    [DataRow(float.NegativeInfinity, 100f)]
    public void IsOffCooldown_NonFiniteTime_IsFalse(float lastFired, float now) =>
        Assert.IsFalse(_sut.IsOffCooldown(lastFired, now, 15f));

    // --- requires and anyOf ---

    [TestMethod]
    public void FiringTrigger_EveryRequiresHolds_AndNoAnyOf_Fires()
    {
        _senses.Enemies.Add(new EnemySense(2f, 1f, false));
        var profile = new RaceAbilityProfile { Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 3f) } };

        Assert.IsTrue(Fires(profile));
    }

    [TestMethod]
    public void FiringTrigger_ARequiresFails_DoesNotFire()
    {
        _senses.Enemies.Add(new EnemySense(5f, 1f, false));
        var profile = new RaceAbilityProfile
        {
            Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 3f) },
            AnyOf = { T(RaceAbilityTriggerKind.Always) },
        };

        Assert.IsFalse(Fires(profile));
    }

    [TestMethod]
    public void FiringTrigger_RequiresHold_ButNoAnyOfHolds_DoesNotFire()
    {
        _senses.Enemies.Add(new EnemySense(2f, 1f, false));
        var profile = new RaceAbilityProfile
        {
            Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 3f) },
            AnyOf = { T(RaceAbilityTriggerKind.HealthBelow, fraction: 0.5f), T(RaceAbilityTriggerKind.TookDamage) },
        };

        Assert.IsFalse(Fires(profile));
    }

    [TestMethod]
    public void FiringTrigger_OneAnyOfHolds_Fires()
    {
        _senses.TookDamage = true;
        var profile = new RaceAbilityProfile
        {
            AnyOf = { T(RaceAbilityTriggerKind.HealthBelow, fraction: 0.5f), T(RaceAbilityTriggerKind.TookDamage) },
        };

        Assert.IsTrue(Fires(profile));
    }

    [TestMethod]
    public void FiringTrigger_NoTriggersAtAll_DoesNotFire() =>
        Assert.IsFalse(Fires(new RaceAbilityProfile()));

    [TestMethod]
    public void FiringTrigger_NotFiring_IsNull() =>
        Assert.IsNull(_sut.FiringTrigger(new RaceAbilityProfile { AnyOf = { T(RaceAbilityTriggerKind.TookDamage) } }, _senses));

    [TestMethod]
    public void FiringTrigger_NamesTheFirstAnyOfThatHolds()
    {
        _senses.TookDamage = true;
        _senses.HealthFraction = 0.3f;
        var profile = new RaceAbilityProfile
        {
            AnyOf = { T(RaceAbilityTriggerKind.CavalryClosing, range: 20f), T(RaceAbilityTriggerKind.HealthBelow, fraction: 0.5f), T(RaceAbilityTriggerKind.TookDamage) },
        };

        Assert.AreEqual(RaceAbilityTriggerKind.HealthBelow, _sut.FiringTrigger(profile, _senses));
    }

    [TestMethod]
    public void FiringTrigger_RequiresOnly_NamesTheFirstRequires()
    {
        _senses.Enemies.Add(new EnemySense(2f, 1f, false));
        var profile = new RaceAbilityProfile { Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 3f), T(RaceAbilityTriggerKind.Always) } };

        Assert.AreEqual(RaceAbilityTriggerKind.EnemyWithin, _sut.FiringTrigger(profile, _senses));
    }

    // --- each kind ---

    [TestMethod]
    public void Always_Holds() => Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.Always)));

    [TestMethod]
    public void EnemyWithin_AtTheRange_Holds()
    {
        _senses.Enemies.Add(new EnemySense(3f, 1f, false));
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.EnemyWithin, range: 3f)));
    }

    [TestMethod]
    public void EnemyWithin_NoEnemyInRange_DoesNotHold()
    {
        _senses.Enemies.Add(new EnemySense(3.1f, 1f, false));
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.EnemyWithin, range: 3f)));
    }

    [TestMethod]
    public void EnemyWithin_NaNDistance_DoesNotHold()
    {
        _senses.Enemies.Add(new EnemySense(float.NaN, 1f, false));
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.EnemyWithin, range: 3f)));
    }

    [TestMethod]
    public void EnemiesWithin_EnoughInRange_Holds()
    {
        _senses.Enemies.Add(new EnemySense(1f, 1f, false));
        _senses.Enemies.Add(new EnemySense(4f, 1f, false));
        _senses.Enemies.Add(new EnemySense(5f, 1f, false));
        _senses.Enemies.Add(new EnemySense(9f, 1f, false));
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.EnemiesWithin, range: 5f, count: 3)));
    }

    [TestMethod]
    public void EnemiesWithin_TooFewInRange_DoesNotHold()
    {
        _senses.Enemies.Add(new EnemySense(1f, 1f, false));
        _senses.Enemies.Add(new EnemySense(4f, 1f, false));
        _senses.Enemies.Add(new EnemySense(9f, 1f, false));
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.EnemiesWithin, range: 5f, count: 3)));
    }

    [DataTestMethod]
    [DataRow(0.75f, true)]
    [DataRow(0.5f, true)]
    [DataRow(0.76f, false)]
    [DataRow(float.NaN, false)]
    public void HealthBelow_ComparesTheFraction(float health, bool expected)
    {
        _senses.HealthFraction = health;
        Assert.AreEqual(expected, Holds(T(RaceAbilityTriggerKind.HealthBelow, fraction: 0.75f)));
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void TookDamage_ReadsTheSense(bool tookDamage, bool expected)
    {
        _senses.TookDamage = tookDamage;
        Assert.AreEqual(expected, Holds(T(RaceAbilityTriggerKind.TookDamage)));
    }

    [DataTestMethod]
    [DataRow(9f, 96f, true)]     // in range, 4 s ago
    [DataRow(11f, 96f, false)]   // out of range
    [DataRow(9f, 94f, false)]    // 6 s ago, too old
    [DataRow(9f, 101f, false)]   // in the future
    [DataRow(9f, float.NaN, false)]
    public void KinFell_RecentAndNear_Holds(float distance, float at, bool expected)
    {
        _senses.FallenKin.Add(new FallenSense(distance, at));
        Assert.AreEqual(expected, Holds(T(RaceAbilityTriggerKind.KinFell, range: 10f, seconds: 5f)));
    }

    [TestMethod]
    public void LandedKill_Recent_Holds()
    {
        _senses.LastKillAt = 99f;
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.LandedKill, seconds: 1.5f)));
    }

    [TestMethod]
    public void LandedKill_TooOld_DoesNotHold()
    {
        _senses.LastKillAt = 98f;
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.LandedKill, seconds: 1.5f)));
    }

    [TestMethod]
    public void LandedKill_Never_DoesNotHold() =>
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.LandedKill, seconds: 1.5f)));

    [TestMethod]
    public void LandedKill_NaNStamp_DoesNotHold()
    {
        _senses.LastKillAt = float.NaN;
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.LandedKill, seconds: 1.5f)));
    }

    [TestMethod]
    public void WoundedEnemyWithin_WoundedAndNear_Holds()
    {
        _senses.Enemies.Add(new EnemySense(2f, 0.4f, false));
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.WoundedEnemyWithin, range: 3f, fraction: 0.5f)));
    }

    [TestMethod]
    public void WoundedEnemyWithin_HealthyNearAndWoundedFar_DoesNotHold()
    {
        _senses.Enemies.Add(new EnemySense(2f, 0.9f, false));
        _senses.Enemies.Add(new EnemySense(6f, 0.1f, false));
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.WoundedEnemyWithin, range: 3f, fraction: 0.5f)));
    }

    [TestMethod]
    public void CavalryClosing_ClosingRiderInRange_Holds()
    {
        _senses.Enemies.Add(new EnemySense(15f, 1f, true));
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.CavalryClosing, range: 20f)));
    }

    [TestMethod]
    public void CavalryClosing_FootmanOrDistantRider_DoesNotHold()
    {
        _senses.Enemies.Add(new EnemySense(5f, 1f, false));
        _senses.Enemies.Add(new EnemySense(25f, 1f, true));
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.CavalryClosing, range: 20f)));
    }

    [TestMethod]
    public void RangedTargetWithin_ArcherWithATarget_Holds()
    {
        _senses.WieldsRanged = true;
        _senses.Enemies.Add(new EnemySense(25f, 1f, false));
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.RangedTargetWithin, range: 30f)));
    }

    [TestMethod]
    public void RangedTargetWithin_NotWieldingARangedWeapon_DoesNotHold()
    {
        _senses.Enemies.Add(new EnemySense(25f, 1f, false));
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.RangedTargetWithin, range: 30f)));
    }

    [TestMethod]
    public void KinWithin_EnoughKin_Holds()
    {
        _senses.KinDistances.Add(2f);
        _senses.KinDistances.Add(5f);
        _senses.KinDistances.Add(6f);
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.KinWithin, range: 6f, count: 3)));
    }

    [TestMethod]
    public void KinWithin_TooFewKin_DoesNotHold()
    {
        _senses.KinDistances.Add(2f);
        _senses.KinDistances.Add(7f);
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.KinWithin, range: 6f, count: 2)));
    }

    // --- what the sensor gathers ---

    [TestMethod]
    public void PlanScan_IsTheWidestTriggerThatCanStillHold()
    {
        var profile = new RaceAbilityProfile
        {
            Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 20f) },
            AnyOf = { T(RaceAbilityTriggerKind.CavalryClosing, range: 25f), T(RaceAbilityTriggerKind.KinFell, range: 35f, seconds: 5f) },
        };

        var plan = _sut.PlanScan(profile, _senses);

        Assert.AreEqual(25f, plan.EnemyRange, 0.0001f);
        Assert.AreEqual(25f, plan.ClosingRange, 0.0001f);
        Assert.AreEqual(0f, plan.KinRange, 0.0001f);
        Assert.IsTrue(plan.FallenKin);   // a fallen kinsman is remembered, not scanned for
    }

    [TestMethod]
    public void PlanScan_NoSpatialTrigger_ScansNothing()
    {
        _senses.TookDamage = true;

        var plan = _sut.PlanScan(new RaceAbilityProfile { AnyOf = { T(RaceAbilityTriggerKind.TookDamage) } }, _senses);

        Assert.AreEqual(0f, plan.EnemyRange, 0.0001f);
        Assert.AreEqual(0f, plan.KinRange, 0.0001f);
        Assert.IsFalse(plan.FallenKin);
    }

    [TestMethod]
    public void PlanScan_ARequiresHisOwnStateFails_ScansNothing()
    {
        // Rohan's riders: a footman never fires, so he never scans 30 m for it.
        var profile = new RaceAbilityProfile { Requires = { T(RaceAbilityTriggerKind.Mounted), T(RaceAbilityTriggerKind.EnemyWithin, range: 30f) } };

        Assert.AreEqual(0f, _sut.PlanScan(profile, _senses).EnemyRange, 0.0001f);
        _senses.Mounted = true;
        Assert.AreEqual(30f, _sut.PlanScan(profile, _senses).EnemyRange, 0.0001f);
    }

    [TestMethod]
    public void PlanScan_RequiredRangedTargetWithoutARangedWeapon_ScansNothing() =>
        Assert.AreEqual(0f, _sut.PlanScan(new RaceAbilityProfile { Requires = { T(RaceAbilityTriggerKind.RangedTargetWithin, range: 40f) } }, _senses).EnemyRange, 0.0001f);

    [TestMethod]
    public void PlanScan_ARangedTargetOnlyWidensTheScanForARangedWeapon()
    {
        var profile = new RaceAbilityProfile
        {
            AnyOf = { T(RaceAbilityTriggerKind.RangedTargetWithin, range: 35f), T(RaceAbilityTriggerKind.EnemyWithin, range: 4f) },
        };

        Assert.AreEqual(4f, _sut.PlanScan(profile, _senses).EnemyRange, 0.0001f);
        _senses.WieldsRanged = true;
        Assert.AreEqual(35f, _sut.PlanScan(profile, _senses).EnemyRange, 0.0001f);
    }

    [TestMethod]
    public void PlanScan_NoAnyOfCanStillHold_ScansNothing()
    {
        // Umbar's corsairs: unhurt and with no fresh kill, nothing in the list can hold.
        var profile = new RaceAbilityProfile
        {
            Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 4f) },
            AnyOf = { T(RaceAbilityTriggerKind.TookDamage), T(RaceAbilityTriggerKind.LandedKill, seconds: 2f) },
        };

        Assert.AreEqual(0f, _sut.PlanScan(profile, _senses).EnemyRange, 0.0001f);
        _senses.TookDamage = true;
        Assert.AreEqual(4f, _sut.PlanScan(profile, _senses).EnemyRange, 0.0001f);
    }

    [TestMethod]
    public void PlanScan_KinReachIsTheKinTrigger_NotTheBonus()
    {
        // The kin bonus counts its own kin at activation, so it widens neither scan.
        var profile = new RaceAbilityProfile
        {
            Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 4f), T(RaceAbilityTriggerKind.KinWithin, range: 6f, count: 3) },
            KinBonus = new RaceAbilityKinBonus { Radius = 9f, PerKinPercent = 3f, MaxKin = 5 },
        };

        var plan = _sut.PlanScan(profile, _senses);

        Assert.AreEqual(4f, plan.EnemyRange, 0.0001f);
        Assert.AreEqual(6f, plan.KinRange, 0.0001f);
        Assert.AreEqual(0f, plan.ClosingRange, 0.0001f);
    }

    [DataTestMethod]
    [DataRow(RaceAbilityTriggerKind.EnemiesWithin)]
    [DataRow(RaceAbilityTriggerKind.NoEnemyWithin)]
    [DataRow(RaceAbilityTriggerKind.WoundedEnemyWithin)]
    public void PlanScan_EveryEnemyTriggerSetsTheReach(RaceAbilityTriggerKind kind) =>
        Assert.AreEqual(7f, _sut.PlanScan(new RaceAbilityProfile { Requires = { T(kind, range: 7f, fraction: 0.5f, count: 1) } }, _senses).EnemyRange, 0.0001f);

    [TestMethod]
    public void RequiresHoldBeforeKin_AFailingEnemyRequirement_SkipsTheKin()
    {
        var profile = new RaceAbilityProfile
        {
            Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 4f), T(RaceAbilityTriggerKind.KinWithin, range: 6f, count: 3) },
        };

        Assert.IsFalse(_sut.RequiresHoldBeforeKin(profile, _senses));
        _senses.Enemies.Add(new EnemySense(3f, 1f, false));
        Assert.IsTrue(_sut.RequiresHoldBeforeKin(profile, _senses));   // the kin requirement waits for the kin scan
    }

    [TestMethod]
    public void RequiresHoldBeforeKin_LeavesKinFellToTheMemory() =>
        Assert.IsTrue(_sut.RequiresHoldBeforeKin(new RaceAbilityProfile { Requires = { T(RaceAbilityTriggerKind.KinFell, range: 10f, seconds: 5f) } }, _senses));

    // The plan may only leave out what cannot change the answer. For every shipped profile, across a grid of
    // the soldier's own states in one crowded scene, FiringTrigger on what the plan gathers (as the sensor
    // gathers it) equals FiringTrigger on everything.
    [TestMethod]
    public void PlanScan_ShippedProfiles_GatherEnoughForTheSameAnswer()
    {
        var config = ShippedRaceAbilitiesConfigTests.Load(ShippedRaceAbilitiesConfigTests.ModuleDataPath, NSubstitute.Substitute.For<TAOM.Core.Logging.IModLogger>());
        var profiles = config.Races.Values.Concat(config.Cultures.Values).Distinct().ToList();
        var enemies = new[]
        {
            new EnemySense(2f, 0.4f, false), new EnemySense(5f, 1f, false), new EnemySense(12f, 1f, true),
            new EnemySense(25f, 1f, true), new EnemySense(38f, 1f, false),
        };
        var kin = new[] { 2f, 3f, 5f, 8f };
        var fallen = new FallenSense(4f, 98f);
        int compared = 0, fired = 0, skipped = 0;

        foreach (var profile in profiles)
        foreach (var mounted in new[] { false, true })
        foreach (var ranged in new[] { false, true })
        foreach (var health in new[] { 1f, 0.4f })
        foreach (var hurt in new[] { false, true })
        foreach (var lastKill in new float?[] { null, 99f })
        foreach (var morale in new[] { 80f, 30f })
        {
            RaceAbilitySenses Self() => new RaceAbilitySenses
            {
                Now = 100f, HealthFraction = health, Morale = morale, TookDamage = hurt, WieldsRanged = ranged,
                Mounted = mounted, LastKillAt = lastKill,
            };
            var everything = Self();
            everything.Enemies.AddRange(enemies);
            everything.KinDistances.AddRange(kin);
            everything.FallenKin.Add(fallen);

            var gathered = Self();
            var plan = _sut.PlanScan(profile, gathered);
            foreach (var enemy in enemies)
                if (enemy.Distance <= plan.EnemyRange)
                    gathered.Enemies.Add(new EnemySense(enemy.Distance, enemy.HealthFraction,
                        plan.ClosingRange > 0f && enemy.Distance <= plan.ClosingRange && enemy.CavalryClosing));
            if (_sut.RequiresHoldBeforeKin(profile, gathered))
            {
                foreach (var distance in kin)
                    if (distance <= plan.KinRange)
                        gathered.KinDistances.Add(distance);
                if (plan.FallenKin)
                    gathered.FallenKin.Add(fallen);
            }

            var expected = _sut.FiringTrigger(profile, everything);
            Assert.AreEqual(expected, _sut.FiringTrigger(profile, gathered),
                $"{profile.AbilityId}: mounted={mounted} ranged={ranged} health={health} hurt={hurt} kill={lastKill} morale={morale}");
            compared++;
            if (expected.HasValue)
                fired++;
            if (gathered.Enemies.Count < enemies.Length)
                skipped++;
        }

        Assert.AreEqual(18, profiles.Count);
        Assert.IsTrue(fired > 0 && fired < compared, $"the grid must both fire and not fire ({fired} of {compared})");
        Assert.IsTrue(skipped > 0, "the plan must leave something out, or it saves nothing");
    }

    // --- tier scaling ---

    [DataTestMethod]
    [DataRow(3, 1f)]
    [DataRow(5, 1.1f)]
    [DataRow(7, 1.2f)]     // 1.2 is the cap
    [DataRow(1, 0.9f)]
    [DataRow(0, 0.85f)]    // 0.85 is the floor
    public void MagnitudeFactor_FollowsTheTier(int tier, float expected) =>
        Assert.AreEqual(expected, _sut.MagnitudeFactor(tier, isHero: false, new RaceAbilityTierScaling()), 0.0001f);

    [TestMethod]
    public void MagnitudeFactor_Hero_TakesTheHeroFactor() =>
        Assert.AreEqual(1.25f, _sut.MagnitudeFactor(7, isHero: true, new RaceAbilityTierScaling()), 0.0001f);

    [TestMethod]
    public void Scale_MultipliesPercentagesHealAndFear()
    {
        var effects = new RaceAbilityEffects
        {
            MeleeDamagePercent = 20f, BlockAbilityPercent = -60f, HealPerKill = 8f, FearOnKillMorale = 4f,
            FearOnKillRadius = 6f, MoraleFloor = 30f, ForceCrushThrough = true,
        };

        var scaled = _sut.Scale(effects, 1.1f);

        Assert.AreEqual(22f, scaled.MeleeDamagePercent, 0.0001f);
        Assert.AreEqual(-66f, scaled.BlockAbilityPercent, 0.0001f);
        Assert.AreEqual(8.8f, scaled.HealPerKill, 0.0001f);
        Assert.AreEqual(4.4f, scaled.FearOnKillMorale, 0.0001f);
        Assert.AreEqual(6f, scaled.FearOnKillRadius, 0.0001f);    // a radius is not a magnitude
        Assert.AreEqual(30f, scaled.MoraleFloor, 0.0001f);        // nor is a floor
        Assert.IsTrue(scaled.ForceCrushThrough);
    }

    [TestMethod]
    public void Scale_KeepsEachFieldInsideItsValidRange()
    {
        var scaled = _sut.Scale(new RaceAbilityEffects { BlockAbilityPercent = -90f, DamageReductionPercent = 80f }, 1.25f);

        Assert.AreEqual(-95f, scaled.BlockAbilityPercent, 0.0001f);
        Assert.AreEqual(90f, scaled.DamageReductionPercent, 0.0001f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void Scale_BadFactor_IsOne(float factor) =>
        Assert.AreEqual(20f, _sut.Scale(new RaceAbilityEffects { MeleeDamagePercent = 20f }, factor).MeleeDamagePercent, 0.0001f);

    // --- kill extension ---

    [TestMethod]
    public void ExtendOnKill_AddsTheExtension()
    {
        var profile = new RaceAbilityProfile { KillExtensionSeconds = 2f, MaxDurationSeconds = 14f };
        Assert.AreEqual(110f, _sut.ExtendOnKill(108f, activatedAt: 100f, profile), 0.0001f);
    }

    [TestMethod]
    public void ExtendOnKill_StopsAtTheMaximum()
    {
        var profile = new RaceAbilityProfile { KillExtensionSeconds = 2f, MaxDurationSeconds = 14f };
        Assert.AreEqual(114f, _sut.ExtendOnKill(113f, activatedAt: 100f, profile), 0.0001f);
    }

    [TestMethod]
    public void ExtendOnKill_NoExtension_KeepsTheEnd()
    {
        var profile = new RaceAbilityProfile { MaxDurationSeconds = 8f };
        Assert.AreEqual(108f, _sut.ExtendOnKill(108f, activatedAt: 100f, profile), 0.0001f);
    }

    // --- crush-through ---

    [TestMethod]
    public void CrushVerdict_RagingSwing_Crushes() =>
        Assert.AreEqual(true, _sut.CrushVerdict(new RaceAbilityEffects { ForceCrushThrough = true }, null, isSwing: true, isPassive: false));

    [TestMethod]
    public void CrushVerdict_RagingThrust_HasNoOpinion() =>
        Assert.IsNull(_sut.CrushVerdict(new RaceAbilityEffects { ForceCrushThrough = true }, null, isSwing: false, isPassive: false));

    [TestMethod]
    public void CrushVerdict_RagingPassiveUse_HasNoOpinion() =>
        Assert.IsNull(_sut.CrushVerdict(new RaceAbilityEffects { ForceCrushThrough = true }, null, isSwing: true, isPassive: true));

    [TestMethod]
    public void CrushVerdict_HoldBeatsForce() =>
        Assert.AreEqual(false, _sut.CrushVerdict(new RaceAbilityEffects { ForceCrushThrough = true },
            new RaceAbilityEffects { HoldAgainstCrush = true }, isSwing: true, isPassive: false));

    [TestMethod]
    public void CrushVerdict_HoldingDefender_AgainstAnyone_Holds() =>
        Assert.AreEqual(false, _sut.CrushVerdict(null, new RaceAbilityEffects { HoldAgainstCrush = true }, isSwing: true, isPassive: false));

    [TestMethod]
    public void CrushVerdict_NoAbility_HasNoOpinion() =>
        Assert.IsNull(_sut.CrushVerdict(null, null, isSwing: true, isPassive: false));

    // --- damage ---

    [TestMethod]
    public void AmplifyHit_Melee_AddsThePercentage() =>
        Assert.AreEqual(60f, _sut.AmplifyHit(50f, new RaceAbilityEffects { MeleeDamagePercent = 20f }, isMissile: false, isHorseCharge: false, isFallDamage: false), 0.0001f);

    [TestMethod]
    public void AmplifyHit_Melee_NoAbility_KeepsTheDamage() => Assert.AreEqual(50f, _sut.AmplifyHit(50f, null, isMissile: false, isHorseCharge: false, isFallDamage: false), 0.0001f);

    [TestMethod]
    public void AmplifyHit_Melee_NaNDamage_PassesItThrough() =>
        Assert.IsTrue(float.IsNaN(_sut.AmplifyHit(float.NaN, new RaceAbilityEffects { MeleeDamagePercent = 20f }, isMissile: false, isHorseCharge: false, isFallDamage: false)));

    [TestMethod]
    public void Reduce_TakesThePercentageOff() =>
        Assert.AreEqual(40f, _sut.Reduce(50f, new RaceAbilityEffects { DamageReductionPercent = 20f }, victimIsMount: false, isFallDamage: false), 0.0001f);

    [TestMethod]
    public void Reduce_NoAbility_KeepsTheDamage() => Assert.AreEqual(50f, _sut.Reduce(50f, null, victimIsMount: false, isFallDamage: false), 0.0001f);

    [TestMethod]
    public void Reduce_AFall_KeepsItsDamage() =>
        Assert.AreEqual(50f, _sut.Reduce(50f, new RaceAbilityEffects { DamageReductionPercent = 20f }, victimIsMount: false, isFallDamage: true), 0.0001f);

    [TestMethod]
    public void Reduce_NaNDamage_PassesItThrough() =>
        Assert.IsTrue(float.IsNaN(_sut.Reduce(float.NaN, new RaceAbilityEffects { DamageReductionPercent = 20f }, victimIsMount: false, isFallDamage: false)));

    // --- the percentage arithmetic every effect goes through ---

    [DataTestMethod]
    [DataRow(1f, 20f, 1.2f)]
    [DataRow(1f, -20f, 0.8f)]
    [DataRow(0.93f, 0f, 0.93f)]
    public void Times_AppliesThePercentage(float value, float percent, float expected) =>
        Assert.AreEqual(expected, RaceAbilityService.Times(value, percent), 0.0001f);

    [TestMethod]
    public void Times_NaNValue_PassesItThrough() =>
        Assert.IsTrue(float.IsNaN(RaceAbilityService.Times(float.NaN, 20f)));

    [TestMethod]
    public void Times_AnOverflow_KeepsTheValue() =>
        Assert.AreEqual(float.MaxValue, RaceAbilityService.Times(float.MaxValue, 200f));

    [TestMethod]
    public void Over_FasterAccelerationShortensTheTime() =>
        Assert.AreEqual(2f, RaceAbilityService.Over(2.5f, 25f), 0.0001f);

    [TestMethod]
    public void Over_ZeroPercent_KeepsTheValue() =>
        Assert.AreEqual(2.5f, RaceAbilityService.Over(2.5f, 0f), 0.0001f);

    [DataTestMethod]
    [DataRow(0.9f, -60f, 0.36f)]
    [DataRow(0.9f, 25f, 1f)]       // a probability stops at 1
    [DataRow(0.5f, 20f, 0.6f)]
    public void Chance_AppliesThePercentageWithinZeroToOne(float value, float percent, float expected) =>
        Assert.AreEqual(expected, RaceAbilityService.Chance(value, percent), 0.0001f);

    [TestMethod]
    public void Chance_NaNValue_PassesItThrough() =>
        Assert.IsTrue(float.IsNaN(RaceAbilityService.Chance(float.NaN, 20f)));

    // --- resistances and morale ---

    [TestMethod]
    public void ScaleResistance_AddsThePercentage() =>
        Assert.AreEqual(1.2f, _sut.ScaleResistance(0.4f, 200f), 0.0001f);

    [TestMethod]
    public void ScaleResistance_ANonHumansMaxValue_StaysFinite() =>
        Assert.AreEqual(float.MaxValue, _sut.ScaleResistance(float.MaxValue, 200f));

    [TestMethod]
    public void ScaleResistance_NaNBase_PassesItThrough() =>
        Assert.IsTrue(float.IsNaN(_sut.ScaleResistance(float.NaN, 200f)));

    [DataTestMethod]
    [DataRow(10f, 30f, 20f)]
    [DataRow(35f, 30f, 0f)]
    [DataRow(10f, 0f, 0f)]
    [DataRow(float.NaN, 30f, 0f)]
    public void MoraleTopUp_RaisesToTheFloor(float morale, float floor, float expected) =>
        Assert.AreEqual(expected, _sut.MoraleTopUp(morale, floor), 0.0001f);

    // --- triggers added with the orc family and the human cultures ---

    [DataTestMethod]
    [DataRow(55f, true)]
    [DataRow(60f, true)]
    [DataRow(61f, false)]
    [DataRow(-1f, false)]      // no morale component: never "low"
    [DataRow(float.NaN, false)]
    public void MoraleBelow_ComparesMoraleOverOneHundred(float morale, bool expected)
    {
        _senses.Morale = morale;
        Assert.AreEqual(expected, Holds(T(RaceAbilityTriggerKind.MoraleBelow, fraction: 0.6f)));
    }

    [TestMethod]
    public void NoEnemyWithin_NobodyClose_Holds()
    {
        _senses.Enemies.Add(new EnemySense(10f, 1f, false));
        Assert.IsTrue(Holds(T(RaceAbilityTriggerKind.NoEnemyWithin, range: 4f)));
    }

    [TestMethod]
    public void NoEnemyWithin_SomeoneClose_DoesNotHold()
    {
        _senses.Enemies.Add(new EnemySense(3f, 1f, false));
        Assert.IsFalse(Holds(T(RaceAbilityTriggerKind.NoEnemyWithin, range: 4f)));
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void Mounted_ReadsTheSense(bool mounted, bool expected)
    {
        _senses.Mounted = mounted;
        Assert.AreEqual(expected, Holds(T(RaceAbilityTriggerKind.Mounted)));
    }

    [TestMethod]
    public void HuntersRush_FiresOnlyBetweenTheTwoRanges()
    {
        var profile = new RaceAbilityProfile
        {
            Requires = { T(RaceAbilityTriggerKind.EnemyWithin, range: 15f), T(RaceAbilityTriggerKind.NoEnemyWithin, range: 4f) },
        };
        _senses.Enemies.Add(new EnemySense(10f, 1f, false));
        Assert.IsTrue(Fires(profile));

        _senses.Enemies.Add(new EnemySense(2f, 1f, false));
        Assert.IsFalse(Fires(profile));
    }

    // --- rules the engine boundary asks for ---

    [DataTestMethod]
    [DataRow(-10f, 0f, 5f, 0f, false)]   // riding away
    [DataRow(10f, 0f, 2f, 0f, false)]    // towards, too slowly
    [DataRow(10f, 0f, 5f, 0f, true)]     // towards, fast
    [DataRow(10f, 0f, 0f, 9f, false)]    // across, not towards
    [DataRow(0f, 0f, 9f, 0f, false)]     // on top of him: no direction
    [DataRow(float.NaN, 0f, 9f, 0f, false)]
    public void IsClosing_NeedsSpeedTowardsTheSoldier(float offsetX, float offsetY, float vx, float vy, bool expected) =>
        Assert.AreEqual(expected, _sut.IsClosing(offsetX, offsetY, vx, vy));

    [TestMethod]
    public void IsRallyRecruit_ReadyAiKinsman_Joins() =>
        Assert.IsTrue(_sut.IsRallyRecruit(isActive: true, isAiControlled: true, isRetreating: false, sameTeam: true, sameProfile: true, offCooldown: true));

    [DataTestMethod]
    [DataRow(false, true, false, true, true, true)]   // dead or gone
    [DataRow(true, false, false, true, true, true)]   // the player's own character
    [DataRow(true, true, true, true, true, true)]     // fleeing
    [DataRow(true, true, false, false, true, true)]   // another team
    [DataRow(true, true, false, true, false, true)]   // another ability
    [DataRow(true, true, false, true, true, false)]   // on cooldown
    public void IsRallyRecruit_AnyGateFails_StaysOut(bool active, bool ai, bool retreating, bool sameTeam, bool sameProfile, bool offCooldown) =>
        Assert.IsFalse(_sut.IsRallyRecruit(active, ai, retreating, sameTeam, sameProfile, offCooldown));

    [DataTestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(3, true)]
    [DataRow(6, true)]
    public void ShoutsOnRally_EveryThirdJoiner(int rallied, bool expected) =>
        Assert.AreEqual(expected, _sut.ShoutsOnRally(rallied));

    [TestMethod]
    public void CreditsKill_AnEnemyKilledByALiveSoldierWithAnAbility_Counts() =>
        Assert.IsTrue(_sut.CreditsKill(victimDied: true, victimIsSoldier: true, killerIsVictim: false, killerAlive: true, sameTeam: false,
            killerHasProfile: true));

    [DataTestMethod]
    [DataRow(false, true, false, true, false, true)]   // the victim lived (fled or was removed)
    [DataRow(true, false, false, true, false, true)]   // a horse: mounts carry no team, so only this gate keeps it out
    [DataRow(true, true, true, true, false, true)]     // he killed himself (a fall)
    [DataRow(true, true, false, false, false, true)]   // the killer is gone
    [DataRow(true, true, false, true, true, true)]     // a teamkill, which vanilla does not count either
    [DataRow(true, true, false, true, false, false)]   // the killer has no ability
    public void CreditsKill_AnyGateFails_NoCredit(bool died, bool soldier, bool self, bool alive, bool sameTeam, bool hasProfile) =>
        Assert.IsFalse(_sut.CreditsKill(died, soldier, self, alive, sameTeam, hasProfile));

    [DataTestMethod]
    [DataRow(50f, 100f, 8f, 58f)]
    [DataRow(97f, 100f, 8f, 100f)]   // never above the limit
    [DataRow(50f, 100f, 0f, 50f)]
    [DataRow(50f, float.NaN, 8f, 50f)]
    [DataRow(float.NaN, 100f, 8f, float.NaN)]
    public void HealOnKill_HealsWithinTheLimit(float health, float limit, float heal, float expected)
    {
        var result = _sut.HealOnKill(health, limit, heal);
        if (float.IsNaN(expected))
            Assert.IsTrue(float.IsNaN(result));
        else
            Assert.AreEqual(expected, result, 0.0001f);
    }

    [DataTestMethod]
    [DataRow(0, 0f)]
    [DataRow(3, 9f)]
    [DataRow(8, 15f)]   // capped at five kin
    public void KinBonusPercent_PerKinUpToTheCap(int kin, float expected) =>
        Assert.AreEqual(expected, _sut.KinBonusPercent(new RaceAbilityKinBonus { Radius = 6f, PerKinPercent = 3f, MaxKin = 5 }, kin), 0.0001f);

    [TestMethod]
    public void KinBonusPercent_NoBonus_IsZero() => Assert.AreEqual(0f, _sut.KinBonusPercent(null, 9), 0.0001f);

    [TestMethod]
    public void Scale_AddsTheKinBonusBeforeTheFactor() =>
        Assert.AreEqual(22f, _sut.Scale(new RaceAbilityEffects { MeleeDamagePercent = 5f }, 1.1f, extraMeleePercent: 15f).MeleeDamagePercent, 0.0001f);

    [TestMethod]
    public void Scale_NewFieldsScaleAndThePricesDoNot()
    {
        var scaled = _sut.Scale(new RaceAbilityEffects
        {
            MountSpeedPercent = 10f, RangedDamagePercent = 20f, DismountResistancePercent = 100f, FearAuraMoralePerSecond = 2f,
            FearAuraRadius = 8f, MoraleOnEnd = -8f,
        }, 1.2f);

        Assert.AreEqual(12f, scaled.MountSpeedPercent, 0.0001f);
        Assert.AreEqual(24f, scaled.RangedDamagePercent, 0.0001f);
        Assert.AreEqual(120f, scaled.DismountResistancePercent, 0.0001f);
        Assert.AreEqual(2.4f, scaled.FearAuraMoralePerSecond, 0.0001f);
        Assert.AreEqual(8f, scaled.FearAuraRadius, 0.0001f);
        Assert.AreEqual(-8f, scaled.MoraleOnEnd, 0.0001f);
    }

    [TestMethod]
    public void AmplifyHit_Missile_TakesTheRangedPercentage() =>
        Assert.AreEqual(60f, _sut.AmplifyHit(50f, new RaceAbilityEffects { RangedDamagePercent = 20f, MeleeDamagePercent = 50f },
            isMissile: true, isHorseCharge: false, isFallDamage: false), 0.0001f);

    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void AmplifyHit_HorseChargeOrFall_KeepsItsDamage(bool charge, bool fall) =>
        Assert.AreEqual(50f, _sut.AmplifyHit(50f, new RaceAbilityEffects { MeleeDamagePercent = 20f },
            isMissile: false, isHorseCharge: charge, isFallDamage: fall), 0.0001f);

    [TestMethod]
    public void Reduce_AHitOnTheHorse_IsTheHorses() =>
        Assert.AreEqual(50f, _sut.Reduce(50f, new RaceAbilityEffects { DamageReductionPercent = 20f }, victimIsMount: true, isFallDamage: false), 0.0001f);

    [TestMethod]
    public void MoraleOnEnd_AnActiveWindowEnding_PaysThePrice()
    {
        var before = new RaceAbilityState(new RaceAbilityProfile(), RaceAbilityPhase.Active, 100f, 108f,
            new RaceAbilityEffects { MoraleOnEnd = -8f }, new RaceAbilityEffects());

        Assert.AreEqual(-8f, _sut.MoraleOnEnd(before), 0.0001f);
    }

    [TestMethod]
    public void MoraleOnEnd_ASpentPhaseEnding_CostsNothing()
    {
        var before = new RaceAbilityState(new RaceAbilityProfile(), RaceAbilityPhase.Spent, 100f, 111f,
            new RaceAbilityEffects { MoraleOnEnd = -8f }, new RaceAbilityEffects());

        Assert.AreEqual(0f, _sut.MoraleOnEnd(before), 0.0001f);
    }

    private static RaceAbilityState GlowState(RaceAbilityPhase phase, uint? color) =>
        new RaceAbilityState(new RaceAbilityProfile { GlowColor = color }, phase, 100f, 108f,
            new RaceAbilityEffects(), new RaceAbilityEffects());

    [TestMethod]
    public void GlowFor_AnActiveAbilityWithAColour_ReturnsTheColour() =>
        Assert.AreEqual(0xFFE03A2Eu, _sut.GlowFor(GlowState(RaceAbilityPhase.Active, 0xFFE03A2E)));

    [DataTestMethod]
    [DataRow(RaceAbilityPhase.Spent)]
    [DataRow(RaceAbilityPhase.Ready)]
    public void GlowFor_NotActive_IsDark(RaceAbilityPhase phase) =>
        Assert.IsNull(_sut.GlowFor(GlowState(phase, 0xFFE03A2E)));

    [TestMethod]
    public void GlowFor_AnAbilityWithNoColour_IsDark() =>
        Assert.IsNull(_sut.GlowFor(GlowState(RaceAbilityPhase.Active, null)));

    [TestMethod]
    public void GlowFor_NoState_IsDark() =>
        Assert.IsNull(_sut.GlowFor(null));

    [TestMethod]
    public void HoldsNerve_OnlyWithAMoraleFloor()
    {
        Assert.IsTrue(_sut.HoldsNerve(new RaceAbilityEffects { MoraleFloor = 30f }));
        Assert.IsFalse(_sut.HoldsNerve(new RaceAbilityEffects()));
        Assert.IsFalse(_sut.HoldsNerve(null));
    }

    [DataTestMethod]
    [DataRow(2f, 0.5f, 1f)]
    [DataRow(-2f, 0.5f, 0f)]
    [DataRow(float.NaN, 0.5f, 0f)]
    public void AuraDrain_PerPulse(float perSecond, float seconds, float expected) =>
        Assert.AreEqual(expected, _sut.AuraDrain(perSecond, seconds), 0.0001f);

    [TestMethod]
    public void CountKin_CountsWithinTheRange() =>
        Assert.AreEqual(2, RaceAbilityService.CountKin(new System.Collections.Generic.List<float> { 1f, 6f, 6.1f }, 6f));

    [TestMethod]
    public void ExtendOnKill_NaNEnd_KeepsTheEnd()
    {
        var profile = new RaceAbilityProfile { KillExtensionSeconds = 2f, MaxDurationSeconds = 14f };

        Assert.IsTrue(float.IsNaN(_sut.ExtendOnKill(float.NaN, activatedAt: 100f, profile)));
        Assert.AreEqual(108f, _sut.ExtendOnKill(108f, activatedAt: float.NaN, profile), 0.0001f);
    }

    [TestMethod]
    public void Scale_CapsHealFearAndResistances()
    {
        var scaled = _sut.Scale(new RaceAbilityEffects
        {
            HealPerKill = 90f, FearOnKillMorale = 90f, FearAuraMoralePerSecond = 45f, KnockdownResistancePercent = 900f,
            KnockbackResistancePercent = 900f, DismountResistancePercent = 900f, MeleeDamagePercent = 290f,
        }, 1.25f);

        Assert.AreEqual(100f, scaled.HealPerKill, 0.0001f);
        Assert.AreEqual(100f, scaled.FearOnKillMorale, 0.0001f);
        Assert.AreEqual(50f, scaled.FearAuraMoralePerSecond, 0.0001f);
        Assert.AreEqual(1000f, scaled.KnockdownResistancePercent, 0.0001f);
        Assert.AreEqual(1000f, scaled.KnockbackResistancePercent, 0.0001f);
        Assert.AreEqual(1000f, scaled.DismountResistancePercent, 0.0001f);
        Assert.AreEqual(300f, scaled.MeleeDamagePercent, 0.0001f);
    }
}
