using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

// War-elephant trample — pure decision-service tests (1-for-1 with ADOD's gates + damage formula).
// The service has no TaleWorlds dependencies, so every branch is reachable without mocks; the engine
// values (distance, facing dot, random roll, blocking) are supplied by ElephantMissionBehavior in-game.

namespace TAOM.Tests.Features.Elephant;

[TestClass]
public class ElephantAttackServiceTests
{
    private ElephantAttackService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new ElephantAttackService();

    // ------------------------------------------------------------------ IsElephantMonster

    [TestMethod]
    public void IsElephantMonster_ElephantId_ReturnsTrue()
        => Assert.IsTrue(_sut.IsElephantMonster(ElephantConfig.ElephantMonsterId));

    [TestMethod]
    public void IsElephantMonster_OtherId_ReturnsFalse()
        => Assert.IsFalse(_sut.IsElephantMonster("horse"));

    [TestMethod]
    public void IsElephantMonster_Null_ReturnsFalse()
        => Assert.IsFalse(_sut.IsElephantMonster(null));

    // ------------------------------------------------------------------ ShouldAiTrample (gate exhaustion)
    // All-pass baseline: close (1m < 3), facing (0.9 > 0.25), roll passes (0 < 0.001), not attacking.

    [TestMethod]
    public void ShouldAiTrample_AllGatesPass_ReturnsTrue()
        => Assert.IsTrue(_sut.ShouldAiTrample(distanceToTarget: 1f, facingDot: 0.9f, randomRoll: 0f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldAiTrample_AlreadyAttacking_ReturnsFalse()
        => Assert.IsFalse(_sut.ShouldAiTrample(1f, 0.9f, 0f, alreadyAttacking: true));

    [TestMethod]
    public void ShouldAiTrample_TargetOutOfRange_ReturnsFalse()
        => Assert.IsFalse(_sut.ShouldAiTrample(distanceToTarget: 5f, facingDot: 0.9f, randomRoll: 0f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldAiTrample_NotFacingTarget_ReturnsFalse()
        => Assert.IsFalse(_sut.ShouldAiTrample(1f, facingDot: 0.1f, randomRoll: 0f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldAiTrample_RandomRollFailsProbability_ReturnsFalse()
        // 0.5 is well above the 0.001 per-tick chance → no trample this tick.
        => Assert.IsFalse(_sut.ShouldAiTrample(1f, 0.9f, randomRoll: 0.5f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldAiTrample_DistanceExactlyAtRange_ReturnsFalse()
        // Strict "< TrampleTargetRange" (ADOD uses `< 3f`), so exactly 3m does not trample.
        => Assert.IsFalse(_sut.ShouldAiTrample(distanceToTarget: ElephantConfig.TrampleTargetRange, facingDot: 0.9f, randomRoll: 0f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldAiTrample_FacingDotExactlyAtThreshold_ReturnsFalse()
        // Strict "> TrampleFacingDot" (ADOD uses `> 0.25f`), so exactly at the threshold does not trample.
        => Assert.IsFalse(_sut.ShouldAiTrample(distanceToTarget: 1f, facingDot: ElephantConfig.TrampleFacingDot, randomRoll: 0f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldAiTrample_RandomRollExactlyAtChance_ReturnsFalse()
        // Strict "< TrampleChancePerTick" (ADOD uses `< 0.001f`), so a roll equal to the chance does not trample.
        => Assert.IsFalse(_sut.ShouldAiTrample(distanceToTarget: 1f, facingDot: 0.9f, randomRoll: ElephantConfig.TrampleChancePerTick, alreadyAttacking: false));

    // ------------------------------------------------------------------ ComputeInflictedDamage

    [TestMethod]
    public void ComputeInflictedDamage_TargetNotBlocking_ReturnsTwentyDamage()
        // ADOD: round(10 * 1) * 2 = 20.
        => Assert.AreEqual(20, _sut.ComputeInflictedDamage(targetBlocking: false));

    [TestMethod]
    public void ComputeInflictedDamage_TargetBlocking_ReturnsReducedDamage()
        // ADOD: round(10 * 0.25 = 2.5) * 2. Math.Round(2.5) = 2 (banker's rounding) → 4.
        => Assert.AreEqual(4, _sut.ComputeInflictedDamage(targetBlocking: true));
}
