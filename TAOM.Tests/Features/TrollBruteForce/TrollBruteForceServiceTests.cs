using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrollBruteForce;

// The pure decisions behind the trolls' Brute Force smash (#649): who gets the tree, when the smash
// may start, where it lands, when the clip has reached its impact, and what each enemy in the ring takes.
// Every input is an engine float, so each rule is also pinned against NaN and infinity (fail closed).

namespace TAOM.Tests.Features.TrollBruteForce;

[TestClass]
public class TrollBruteForceServiceTests
{
    private TrollBruteForceService _service = null!;

    [TestInitialize]
    public void Setup() => _service = new TrollBruteForceService();

    [DataTestMethod]
    [DataRow("cave_troll")]
    [DataRow("hill_troll")]   // the hill troll joined on 2026-09-25, on its own skeleton with its own set
    public void IsBruteForceTroll_TheBattleMonsters_AreTrue(string monsterId)
    {
        Assert.IsTrue(_service.IsBruteForceTroll(monsterId));
    }

    [DataTestMethod]
    [DataRow("cave_troll_settlement")]
    [DataRow("cave_troll_child")]
    [DataRow("hill_troll_settlement")]
    [DataRow("hill_troll_child")]
    [DataRow("CAVE_TROLL")]
    [DataRow("")]
    [DataRow(null)]
    public void IsBruteForceTroll_AnyOtherMonster_IsFalse(string? monsterId)
    {
        Assert.IsFalse(_service.IsBruteForceTroll(monsterId));
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
    // ── Body size (Mike, 2026-09-25): AgentScale times the Monster's eye height over the human's 1.70 ──────────
    // The cave troll is a human skeleton scaled 1.9 with the human's eye height, so AgentScale alone measured it;
    // the hill troll's size is in its own skeleton (eye height 3.58) at a scale near 1.09.

    [TestMethod]
    public void BodySize_CaveTroll_EqualsItsAgentScale()
    {
        Assert.AreEqual(1.9f, _service.BodySize(1.9f, 1.70f), 1e-5f);
    }

    [TestMethod]
    public void BodySize_HillTroll_GrowsWithItsOwnHeight()
    {
        Assert.AreEqual(1.09f * 3.58f / 1.70f, _service.BodySize(1.09f, 3.58f), 1e-4f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void BodySize_BadEyeHeight_KeepsTheAgentScale(float eyeHeight)
    {
        Assert.AreEqual(1.09f, _service.BodySize(1.09f, eyeHeight), 1e-6f);
    }

    [TestMethod]
    public void BodySize_HillTroll_ReachesFartherThanItsAgentScaleAlone()
    {
        // The whole point of the change: the hill troll's trigger range follows its real height.
        var size = _service.BodySize(1.09f, 3.58f);

        Assert.IsTrue(_service.ShouldEngage(5.5f, 1f, size, busy: false), "an enemy 5.5 m ahead is in reach of a 3.6 m troll");
        Assert.IsFalse(_service.ShouldEngage(5.5f, 1f, 1.09f, busy: false), "AgentScale alone kept it out of reach");
    }

    // Formation spacing: the engine spaces every foot unit for a 0.76 m human (Formation.UnitDiameter is
    // BipedalRadius x 2 whatever the Monster), so trolls stood inside each other. Vanilla spaces a whole formation
    // for horses once riders are a tenth of it (CalculateHasSignificantNumberOfMounted); trolls follow that rule,
    // spaced for the widest troll's measured shoulders times its AgentScale.

    [TestMethod]
    public void TrollWidth_HillTroll_IsItsMeasuredShouldersTimesItsScale()
    {
        Assert.AreEqual(2.43f * 1.11f, _service.TrollWidth("hill_troll", 1.11f), 1e-5f);
    }

    [TestMethod]
    public void TrollWidth_CaveTroll_IsItsMeasuredShouldersTimesItsScale()
    {
        Assert.AreEqual(0.75f * 1.9f, _service.TrollWidth("cave_troll", 1.9f), 1e-5f);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("human")]
    [DataRow("cave_troll_settlement")]
    public void TrollWidth_NotATroll_IsZero(string? monsterId)
    {
        Assert.AreEqual(0f, _service.TrollWidth(monsterId, 1.5f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void TrollWidth_BadScale_IsZero(float scale)
    {
        Assert.AreEqual(0f, _service.TrollWidth("hill_troll", scale));
    }

    [TestMethod]
    public void FormationUnitDiameter_AllTrolls_SpacesForTheWidestTroll()
    {
        Assert.AreEqual(2.7f, _service.FormationUnitDiameter(0.76f, 11, 11, 2.7f)!.Value, 1e-5f);
    }

    [TestMethod]
    public void FormationUnitDiameter_ATenthTrolls_WidensLikeVanillaCavalry()
    {
        Assert.IsNotNull(_service.FormationUnitDiameter(0.76f, 100, 10, 1.43f));
    }

    [TestMethod]
    public void FormationUnitDiameter_UnderATenthTrolls_KeepsVanilla()
    {
        Assert.IsNull(_service.FormationUnitDiameter(0.76f, 100, 9, 1.43f));
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(10, 0)]
    [DataRow(-1, 1)]
    public void FormationUnitDiameter_NoTrollsOrNoUnits_KeepsVanilla(int units, int trolls)
    {
        Assert.IsNull(_service.FormationUnitDiameter(0.76f, units, trolls, 2f));
    }

    [TestMethod]
    public void FormationUnitDiameter_CapsTheWidth()
    {
        Assert.AreEqual(TrollBruteForceConfig.MaxFormationUnitWidth,
            _service.FormationUnitDiameter(0.76f, 5, 5, 40f)!.Value, 1e-5f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0.76f)]
    [DataRow(0.5f)]
    public void FormationUnitDiameter_NoWiderThanAHuman_KeepsVanilla(float widest)
    {
        Assert.IsNull(_service.FormationUnitDiameter(0.76f, 5, 5, widest));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-0.76f)]
    public void FormationUnitDiameter_BadVanillaDiameter_KeepsVanilla(float vanilla)
    {
        Assert.IsNull(_service.FormationUnitDiameter(vanilla, 5, 5, 2f));
    }
}
