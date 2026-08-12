using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.Enlistment.Content;

/// <summary>
/// The duty and camp-option check, tested directly.
///
/// This file exists because a 2026-08-12 deep review found the formula had no direct coverage at
/// all: `Passes` was only reachable through `FieldDutyRuntimeTests`, which MOCKS `ISkillCheckService`
/// and therefore never executes a line of it. A `Math.Min` for `Math.Max` slip inside the refactor
/// that split out <see cref="SkillCheckService.EffectiveSkill"/> and
/// <see cref="SkillCheckService.TrustBonus"/> would have passed the entire 6,444-test suite.
///
/// The two statics are public precisely so a caller reporting an outcome can quote the same
/// arithmetic the check consumed, so they are pinned here as a contract, not as internals.
/// </summary>
[TestClass]
public class SkillCheckServiceTests
{
    private IRandomProvider _random = null!;
    private SkillCheckService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _random = Substitute.For<IRandomProvider>();
        _sut = new SkillCheckService(_random);
    }

    /// <summary>Pins the roll to an exact value so every Passes case below is deterministic.</summary>
    private void Roll(int value) => _random.Next(SkillCheckService.RollRange).Returns(value);

    // ---- EffectiveSkill ----

    [TestMethod]
    public void EffectiveSkill_TwoSupportSkills_TakesTheBetter_NeverTheSum()
    {
        // Summing would make a two-skill duty far easier than a one-skill duty of equal difficulty,
        // which is not what the difficulty numbers were authored against.
        Assert.AreEqual(30, SkillCheckService.EffectiveSkill(30, 12));
        Assert.AreEqual(30, SkillCheckService.EffectiveSkill(12, 30));
    }

    [TestMethod]
    public void EffectiveSkill_NoSecondarySkill_UsesThePrimary()
    {
        // The absent secondary is represented as int.MinValue inside Math.Max. If that sentinel ever
        // leaks out as the result, every single-skill duty becomes unpassable.
        Assert.AreEqual(17, SkillCheckService.EffectiveSkill(17, null));
        Assert.AreEqual(0, SkillCheckService.EffectiveSkill(0, null));
    }

    [TestMethod]
    public void EffectiveSkill_BothZero_IsZero_NotTheSentinel()
        => Assert.AreEqual(0, SkillCheckService.EffectiveSkill(0, 0));

    // ---- TrustBonus ----

    [TestMethod]
    public void TrustBonus_PositiveTrust_IsWorthTwoPerPoint()
    {
        Assert.AreEqual(0, SkillCheckService.TrustBonus(0));
        Assert.AreEqual(2, SkillCheckService.TrustBonus(1));
        Assert.AreEqual(30, SkillCheckService.TrustBonus(15));
    }

    [TestMethod]
    public void TrustBonus_NegativeTrust_ContributesNothing_AndNeverSubtracts()
    {
        // A run of failed duties drives trust negative. Letting that subtract would compound the
        // hole and make recovery arithmetically harder the more you failed.
        Assert.AreEqual(0, SkillCheckService.TrustBonus(-1));
        Assert.AreEqual(0, SkillCheckService.TrustBonus(-20));
    }

    // ---- Passes: the assembled formula ----

    [TestMethod]
    public void Passes_TotalExactlyMeetsDifficulty_Succeeds()
    {
        // The comparison is >=, so landing exactly on the difficulty is a pass. Off-by-one here
        // silently costs the player every borderline check.
        Roll(10);
        Assert.IsTrue(_sut.Passes(20, null, trust: 5, rankBonus: 4, difficulty: 44)); // 20+10+4+10
    }

    [TestMethod]
    public void Passes_TotalOneShortOfDifficulty_Fails()
    {
        Roll(10);
        Assert.IsFalse(_sut.Passes(20, null, trust: 5, rankBonus: 4, difficulty: 45));
    }

    [TestMethod]
    public void Passes_UsesTheBetterSkill_NotThePrimaryAndNotTheSum()
    {
        // Guards the EffectiveSkill wiring inside Passes: with the sum (42) this clears 50, with the
        // primary alone (12) it does not, and only the better-of (30) gives the intended verdict.
        Roll(20);
        Assert.IsTrue(_sut.Passes(12, 30, trust: 0, rankBonus: 0, difficulty: 50));
        Assert.IsFalse(_sut.Passes(12, 30, trust: 0, rankBonus: 0, difficulty: 51));
    }

    [TestMethod]
    public void Passes_NegativeTrust_DoesNotDragTheTotalDown()
    {
        // Same inputs, trust 0 vs trust -10, must give the same verdict.
        Roll(25);
        Assert.IsTrue(_sut.Passes(20, null, trust: 0, rankBonus: 0, difficulty: 45));
        Roll(25);
        Assert.IsTrue(_sut.Passes(20, null, trust: -10, rankBonus: 0, difficulty: 45));
    }

    [TestMethod]
    public void Passes_DrawsFromTheDeclaredRollRange()
    {
        // RollRange is the EXCLUSIVE upper bound handed to Next, so the reachable roll is 0..50.
        // The duty result line prints "roll 0-{RollRange-1}" off this same constant; if the argument
        // here ever stops being RollRange the log starts lying about the odds.
        Roll(0);
        _sut.Passes(0, null, 0, 0, 1);
        _random.Received().Next(SkillCheckService.RollRange);
    }

    [TestMethod]
    public void Passes_MaximumRoll_CannotClearADifficultyMoreThanFiftyAboveTheDeterministicTotal()
    {
        // The property FieldDutyReachabilityTests enforces across the shipped duty rows, asserted
        // here on the formula itself: 50 is the best possible roll, so a gap of 51 is impossible
        // rather than merely hard. This is the arithmetic behind the 2026-08-12 finding that a
        // skill-0 Recruit cannot pass a difficulty-54 duty.
        Roll(SkillCheckService.RollRange - 1);
        Assert.IsTrue(_sut.Passes(0, null, trust: 0, rankBonus: 0, difficulty: 50));
        Assert.IsFalse(_sut.Passes(0, null, trust: 0, rankBonus: 0, difficulty: 51));
    }

    [TestMethod]
    public void Passes_MatchesTheStaticsCallersUseToReportIt()
    {
        // The whole reason EffectiveSkill and TrustBonus are public: FieldDutyRuntime rebuilds the
        // deterministic half of this sum for its log line. If the two ever diverge, the log reports
        // odds the check did not use. This pins them to one formula.
        const int primary = 12, secondary = 30, trust = 7, rankBonus = 8, roll = 3;
        var deterministic = SkillCheckService.EffectiveSkill(primary, secondary)
            + SkillCheckService.TrustBonus(trust) + rankBonus;

        Roll(roll);
        Assert.IsTrue(_sut.Passes(primary, secondary, trust, rankBonus, deterministic + roll));
        Roll(roll);
        Assert.IsFalse(_sut.Passes(primary, secondary, trust, rankBonus, deterministic + roll + 1));
    }
}
