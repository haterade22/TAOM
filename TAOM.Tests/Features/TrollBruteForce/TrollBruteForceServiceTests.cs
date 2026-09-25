using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrollBruteForce;

// The pure decisions behind the cave troll's Brute Force smash (#649): who gets the tree, when the smash
// may start, where it lands, when the clip has reached its impact, and what each enemy in the ring takes.
// Every input is an engine float, so each rule is also pinned against NaN and infinity (fail closed).

namespace TAOM.Tests.Features.TrollBruteForce;

[TestClass]
public class TrollBruteForceServiceTests
{
    private TrollBruteForceService _service = null!;

    [TestInitialize]
    public void Setup() => _service = new TrollBruteForceService();

    [TestMethod]
    public void IsCaveTroll_TheBattleMonster_IsTrue()
    {
        Assert.IsTrue(_service.IsCaveTroll("cave_troll"));
    }

    [DataTestMethod]
    [DataRow("cave_troll_settlement")]
    [DataRow("cave_troll_child")]
    [DataRow("hill_troll")]
    [DataRow("CAVE_TROLL")]
    [DataRow("")]
    [DataRow(null)]
    public void IsCaveTroll_AnyOtherMonster_IsFalse(string? monsterId)
    {
        Assert.IsFalse(_service.IsCaveTroll(monsterId));
    }

    [TestMethod]
    public void IsOffCooldown_NeverFired_IsTrue()
    {
        Assert.IsTrue(_service.IsOffCooldown(null, 100f));
    }

    [TestMethod]
    public void IsOffCooldown_ExactlyTheCooldownLater_IsTrue()
    {
        Assert.IsTrue(_service.IsOffCooldown(100f, 100f + TrollBruteForceConfig.CooldownSeconds));
    }

    [TestMethod]
    public void IsOffCooldown_JustInsideTheCooldown_IsFalse()
    {
        Assert.IsFalse(_service.IsOffCooldown(100f, 100f + TrollBruteForceConfig.CooldownSeconds - 0.01f));
    }

    [TestMethod]
    public void IsOffCooldown_StampInTheFuture_IsFalse()
    {
        Assert.IsFalse(_service.IsOffCooldown(200f, 100f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 100f)]
    [DataRow(100f, float.NaN)]
    [DataRow(float.PositiveInfinity, 100f)]
    [DataRow(100f, float.PositiveInfinity)]
    public void IsOffCooldown_NonFiniteTime_IsFalse(float lastFired, float now)
    {
        Assert.IsFalse(_service.IsOffCooldown(lastFired, now));
    }

    [TestMethod]
    public void ShouldEngage_EnemyInReachAndInFront_IsTrue()
    {
        Assert.IsTrue(_service.ShouldEngage(1.5f, 0.9f, 1f, busy: false));
    }

    [TestMethod]
    public void ShouldEngage_WhileBusy_IsFalse()
    {
        Assert.IsFalse(_service.ShouldEngage(1.5f, 0.9f, 1f, busy: true));
    }

    [TestMethod]
    public void ShouldEngage_EnemyBeyondReach_IsFalse()
    {
        Assert.IsFalse(_service.ShouldEngage(TrollBruteForceConfig.TriggerRange + 0.1f, 0.9f, 1f, busy: false));
    }

    [TestMethod]
    public void ShouldEngage_ReachScalesWithTheBody()
    {
        float distance = TrollBruteForceConfig.TriggerRange * 1.5f;

        Assert.IsTrue(_service.ShouldEngage(distance, 0.9f, 2f, busy: false));
    }

    [TestMethod]
    public void ShouldEngage_EnemyAtTheFacingLimit_IsFalse()
    {
        Assert.IsFalse(_service.ShouldEngage(1.5f, TrollBruteForceConfig.FacingDot, 1f, busy: false));
    }

    [TestMethod]
    public void ShouldEngage_NoEnemyFound_IsFalse()
    {
        // The scan passes -1 facing when no live enemy is in range, as the elephant-like trees do.
        Assert.IsFalse(_service.ShouldEngage(0f, -1f, 1f, busy: false));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0.9f)]
    [DataRow(1.5f, float.NaN)]
    [DataRow(float.PositiveInfinity, 0.9f)]
    [DataRow(-1f, 0.9f)]
    public void ShouldEngage_BadDistanceOrFacing_IsFalse(float distance, float facingDot)
    {
        Assert.IsFalse(_service.ShouldEngage(distance, facingDot, 1f, busy: false));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-2f)]
    public void ShouldEngage_BadBodyScale_ReadsAsOne(float bodyScale)
    {
        Assert.IsTrue(_service.ShouldEngage(TrollBruteForceConfig.TriggerRange - 0.1f, 0.9f, bodyScale, busy: false));
        Assert.IsFalse(_service.ShouldEngage(TrollBruteForceConfig.TriggerRange + 0.1f, 0.9f, bodyScale, busy: false));
    }

    [TestMethod]
    public void ShouldEngage_HugeBodyScale_IsCapped()
    {
        float beyondTheCap = TrollBruteForceConfig.TriggerRange * TrollBruteForceConfig.MaxBodyScale + 0.1f;

        Assert.IsFalse(_service.ShouldEngage(beyondTheCap, 0.9f, 1000f, busy: false));
    }

    [TestMethod]
    public void TryGetImpactCentre_LandsAheadOfTheTroll()
    {
        bool ok = _service.TryGetImpactCentre(10f, 20f, 0f, 1f, 1f, out float x, out float y);

        Assert.IsTrue(ok);
        Assert.AreEqual(10f, x, 1e-4f);
        Assert.AreEqual(20f + TrollBruteForceConfig.ImpactForward, y, 1e-4f);
    }

    [TestMethod]
    public void TryGetImpactCentre_NormalisesTheLookDirection_AndScalesWithTheBody()
    {
        bool ok = _service.TryGetImpactCentre(0f, 0f, 3f, 0f, 2f, out float x, out float y);

        Assert.IsTrue(ok);
        Assert.AreEqual(TrollBruteForceConfig.ImpactForward * 2f, x, 1e-4f);
        Assert.AreEqual(0f, y, 1e-4f);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f, 0f, 1f)]
    [DataRow(0f, float.PositiveInfinity, 0f, 1f)]
    [DataRow(0f, 0f, float.NaN, 1f)]
    [DataRow(0f, 0f, 0f, 0f)]
    public void TryGetImpactCentre_BadPositionOrLook_Fails(float px, float py, float lookX, float lookY)
    {
        Assert.IsFalse(_service.TryGetImpactCentre(px, py, lookX, lookY, 1f, out _, out _));
    }

    [TestMethod]
    public void HasReachedImpact_BeforeTheImpactFrame_IsFalse()
    {
        Assert.IsFalse(_service.HasReachedImpact(TrollBruteForceConfig.ImpactFraction - 0.01f));
    }

    [TestMethod]
    public void HasReachedImpact_AtTheImpactFrame_IsTrue()
    {
        Assert.IsTrue(_service.HasReachedImpact(TrollBruteForceConfig.ImpactFraction));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void HasReachedImpact_NonFiniteProgress_IsFalse(float progress)
    {
        Assert.IsFalse(_service.HasReachedImpact(progress));
    }

    [TestMethod]
    public void DecideRingBlow_InsideTheInnerRadius_FullDamageAndKnockDown()
    {
        BruteForceBlow? blow = _service.DecideRingBlow(0.5f, 1f, shieldBlocked: false);

        Assert.IsNotNull(blow);
        Assert.AreEqual(TrollBruteForceConfig.CentreDamage, blow.Value.Damage);
        Assert.IsTrue(blow.Value.KnockDown);
    }

    [TestMethod]
    public void DecideRingBlow_AtTheOuterEdge_TakesOneNinth()
    {
        BruteForceBlow? blow = _service.DecideRingBlow(TrollBruteForceConfig.OuterRadius, 1f, shieldBlocked: false);

        Assert.IsNotNull(blow);
        Assert.AreEqual((int)System.Math.Round(TrollBruteForceConfig.CentreDamage / 9f), blow.Value.Damage);
    }

    [TestMethod]
    public void DecideRingBlow_BeyondTheRing_IsNull()
    {
        Assert.IsNull(_service.DecideRingBlow(TrollBruteForceConfig.OuterRadius + 0.1f, 1f, shieldBlocked: false));
    }

    [TestMethod]
    public void DecideRingBlow_RingScalesWithTheBody()
    {
        Assert.IsNotNull(_service.DecideRingBlow(TrollBruteForceConfig.OuterRadius * 1.5f, 2f, shieldBlocked: false));
    }

    [TestMethod]
    public void DecideRingBlow_ShieldBlocked_ScaledDamageAndNoKnockDown()
    {
        BruteForceBlow? blow = _service.DecideRingBlow(0.5f, 1f, shieldBlocked: true);

        Assert.IsNotNull(blow);
        Assert.AreEqual((int)System.Math.Round(TrollBruteForceConfig.CentreDamage * TrollBruteForceConfig.ShieldBlockedMultiplier), blow.Value.Damage);
        Assert.IsFalse(blow.Value.KnockDown);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-0.5f)]
    public void DecideRingBlow_BadDistance_IsNull(float distance)
    {
        Assert.IsNull(_service.DecideRingBlow(distance, 1f, shieldBlocked: false));
    }

    [TestMethod]
    public void OuterRadius_ScalesWithTheBody_AndReadsABadScaleAsOne()
    {
        Assert.AreEqual(TrollBruteForceConfig.OuterRadius * 2f, _service.OuterRadius(2f), 1e-4f);
        Assert.AreEqual(TrollBruteForceConfig.OuterRadius, _service.OuterRadius(float.NaN), 1e-4f);
    }
}
