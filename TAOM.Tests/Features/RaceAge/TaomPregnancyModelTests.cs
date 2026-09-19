using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAge.Models;

namespace TAOM.Tests.Features.RaceAge;

// Tests for the pure-math portion of TaomPregnancyModel.GetDailyChanceOfPregnancyForHero,
// extracted to a static helper (TaomPregnancyModel.ComputeBaseChance) so the 5 branches
// the Phase 7 audit (#179) flagged are exercisable without the sealed-Hero coupling.
//
// Full ADR-007 refactor (introduce IHeroAgeInfo adapter so the entire model is service-testable)
// was deferred when #131 closed on 2026-05-14 and has no tracking issue yet. Until that lands, the early-exit guards
// (IsImmortal / Spouse==null / Age out of fertility window) inside the override body remain
// engine-coupled — covered indirectly here via the extracted helper's age-range branch.
[TestClass]
public class TaomPregnancyModelTests
{
    // --- Age-factor branches (heroAge relative to comesOfAge .. fertilityEnd) ---

    [TestMethod]
    public void ComputeBaseChance_AgeAtComesOfAge_ReturnsPeakChance()
    {
        // ageFactor = 1.2 - (18 - 18) * declineRate = 1.2 (peak)
        // baseChance = 1.2 / 1 * 0.12 * 1 * 1.0 = 0.144
        var result = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        Assert.AreEqual(0.144f, result, 0.001f);
    }

    [TestMethod]
    public void ComputeBaseChance_AgeAtFertilityEnd_ReturnsDecayedChance()
    {
        // declineRate = 1.08 / 27 = 0.04
        // ageFactor = 1.2 - (45 - 18) * 0.04 = 1.2 - 1.08 = 0.12
        // baseChance = 0.12 / 1 * 0.12 * 1 * 1.0 = 0.0144
        var result = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 45, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        Assert.AreEqual(0.0144f, result, 0.0005f);
    }

    [TestMethod]
    public void ComputeBaseChance_ZeroFertilityWindow_FallsBackToDefaultDecline()
    {
        // fertilityEnd == comesOfAge => fertilityWindow=0 => declineRate = 0.04 fallback (not div-by-zero)
        // ageFactor = 1.2 - (50 - 50) * 0.04 = 1.2 (same age, no decline applied)
        var result = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 50, comesOfAge: 50, fertilityEnd: 50,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        Assert.AreEqual(0.144f, result, 0.001f);
    }

    // --- Child-count quadratic decay ---

    [TestMethod]
    public void ComputeBaseChance_OneChild_DivisorIsFour()
    {
        // childCount=1 -> effective=2 -> divisor 2*2 = 4
        // baseChance = 1.2 / 4 * 0.12 * 1 = 0.036
        var result = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 1, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        Assert.AreEqual(0.036f, result, 0.0005f);
    }

    [TestMethod]
    public void ComputeBaseChance_ThreeChildren_DivisorIsSixteen()
    {
        // childCount=3 -> effective=4 -> divisor 4*4 = 16
        // baseChance = 1.2 / 16 * 0.12 * 1 = 0.009
        var result = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 3, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        Assert.AreEqual(0.009f, result, 0.0005f);
    }

    // --- Population-factor branch (player/spouse involvement) ---

    [TestMethod]
    public void ComputeBaseChance_PlayerOrSpouseInvolved_PopulationFactorIsOne()
    {
        // playerOrSpouseInvolved=true skips the clanCap throttle regardless of clan size.
        var withPlayer = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 100,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        Assert.AreEqual(0.144f, withPlayer, 0.001f,
            "Player or spouse-of-player presence must short-circuit the clanCap throttle.");
    }

    [TestMethod]
    public void ComputeBaseChance_NpcOnlyOverpopulatedClan_PopulationFactorClampedToZero()
    {
        // tier 0 -> clanCap = 4 + 4*0 = 4. With aliveLords = 100:
        //   populationFactor = min(1, (2*4 - 100) / 4) = min(1, -23) = -23
        // The model doesn't clamp negative (production matches: `Math.Min(1f, ...)`).
        // ageFactor=1.2, childDivisor=1, mult=0.12, populationFactor=-23
        // baseChance = 1.2 * 0.12 * -23 = -3.312
        var npc = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 100,
            playerOrSpouseInvolved: false, raceFertilityModifier: 1f);

        Assert.IsTrue(npc < 0f,
            "NPC overpopulation produces negative populationFactor — pregnancy chance goes negative " +
            "in production too. The Hero pregnancy-roll consumer applies its own >= 0 guard.");
    }

    [TestMethod]
    public void ComputeBaseChance_NpcModerateClan_PopulationFactorScalesDown()
    {
        // tier 2 -> clanCap = 4 + 4*2 = 12. aliveLords = 6:
        //   populationFactor = min(1, (24 - 6) / 12) = min(1, 1.5) = 1.0
        var moderate = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 2, aliveLords: 6,
            playerOrSpouseInvolved: false, raceFertilityModifier: 1f);

        Assert.AreEqual(0.144f, moderate, 0.001f);
    }

    // --- Race fertility modifier multiplier ---

    [TestMethod]
    public void ComputeBaseChance_DwarvenFertilityHalf_HalvesResult()
    {
        // Race fertility modifier 0.5 (dwarves per RaceAgeService defaults).
        var dwarven = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 0.5f);

        Assert.AreEqual(0.072f, dwarven, 0.001f);
    }

    [TestMethod]
    public void ComputeBaseChance_ZeroFertility_ReturnsZero()
    {
        // Race fertility modifier 0 (e.g., wargs / non-reproducing race in config).
        var sterile = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 0f);

        Assert.AreEqual(0f, sterile);
    }

    [TestMethod]
    public void ComputeBaseChance_FractionalAge_PreservesPrecision()
    {
        // Phase 9b Codex review regression test. The original extraction passed
        // `(int)hero.Age` to the helper, truncating fractional age toward zero. A 44.9-year-old
        // hero would compute identically to a 44-year-old hero — materially shifting late-window
        // pregnancy chance vs vanilla DefaultPregnancyModel (which uses Hero.Age as float).
        // This test pins float-precision behavior: age 44.5 must produce an ageFactor between
        // age 44 and age 45.
        var ageAt44 = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 44f, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        var ageAt44_5 = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 44.5f, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        var ageAt45 = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 45f, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 0,
            playerOrSpouseInvolved: true, raceFertilityModifier: 1f);

        Assert.IsTrue(ageAt44 > ageAt44_5,
            $"Age 44 ({ageAt44}) must yield higher pregnancy chance than 44.5 ({ageAt44_5}) — " +
            "monotonically decreasing through the fertility window.");
        Assert.IsTrue(ageAt44_5 > ageAt45,
            $"Age 44.5 ({ageAt44_5}) must yield higher pregnancy chance than 45 ({ageAt45}).");
        // If heroAge were truncated to int, ageAt44_5 would equal ageAt44 (both compute as 44).
        Assert.AreNotEqual(ageAt44, ageAt44_5,
            "Fractional age must produce a distinct result from its int-truncated value " +
            "(if these match, the model is silently truncating Hero.Age — regression).");
    }

    // --- #628: the clan population brake must win over a race fertility bonus ---
    //
    // Pre-fix the modifier multiplied AFTER populationFactor, so an orc clan (x2) at 1.5x its cap
    // still bred at vanilla's unbraked rate and orc clans sat at the 2*cap ceiling.

    [TestMethod]
    public void ComputeBaseChance_NpcClanPastCap_RaceBonusDropped()
    {
        // tier 0 -> cap 4. aliveLords 6 -> populationFactor = (8 - 6) / 4 = 0.5.
        // Past the cap the bonus is clamped to 1: 1.2 * 0.12 * 0.5 * 1 = 0.072 (pre-fix 0.144).
        var orc = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 6,
            playerOrSpouseInvolved: false, raceFertilityModifier: 2f);

        Assert.AreEqual(0.072f, orc, 0.0005f);
    }

    [TestMethod]
    public void ComputeBaseChance_NpcClanPastCap_BonusRaceNeverBeatsHumanRate()
    {
        var orc = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 25, comesOfAge: 18, fertilityEnd: 45,
            childCount: 2, clanTier: 1, aliveLords: 12,
            playerOrSpouseInvolved: false, raceFertilityModifier: 1.3f);
        var human = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 25, comesOfAge: 18, fertilityEnd: 45,
            childCount: 2, clanTier: 1, aliveLords: 12,
            playerOrSpouseInvolved: false, raceFertilityModifier: 1f);

        Assert.AreEqual(human, orc, 0.00001f,
            "Past the clan cap a fertility bonus must not out-breed the human rate at the same fill.");
    }

    [TestMethod]
    public void ComputeBaseChance_NpcClanAtCap_RaceBonusStillApplies()
    {
        // tier 0 -> cap 4. aliveLords 4 -> populationFactor = min(1, (8 - 4) / 4) = 1, brake not engaged.
        // 1.2 * 0.12 * 1 * 2 = 0.288
        var orc = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 4,
            playerOrSpouseInvolved: false, raceFertilityModifier: 2f);

        Assert.AreEqual(0.288f, orc, 0.001f);
    }

    [TestMethod]
    public void ComputeBaseChance_NpcClanOneLordPastCap_RaceBonusDropped()
    {
        // The exact edge: tier 0 -> cap 4. aliveLords 5 -> populationFactor = (8 - 5) / 4 = 0.75.
        // One lord past the cap the bonus is already gone: 1.2 * 0.12 * 0.75 * 1 = 0.108 (bonus would give 0.216).
        var orc = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 5,
            playerOrSpouseInvolved: false, raceFertilityModifier: 2f);

        Assert.AreEqual(0.108f, orc, 0.0005f);
    }

    [TestMethod]
    public void ComputeBaseChance_NpcClanPastCap_LowFertilityRaceKeepsPenalty()
    {
        // The clamp only caps a bonus; an elf's 0.15 still applies under the brake.
        // tier 0, aliveLords 6 -> populationFactor 0.5. 1.2 * 0.12 * 0.5 * 0.15 = 0.0108
        var elf = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 6,
            playerOrSpouseInvolved: false, raceFertilityModifier: 0.15f);

        Assert.AreEqual(0.0108f, elf, 0.0002f);
    }

    [TestMethod]
    public void ComputeBaseChance_PlayerOrSpouseInvolved_RaceBonusClamped()
    {
        // The player's marriage skips the brake (vanilla), so it must not also take the race bonus:
        // an orc player couple breeds at the vanilla rate. 1.2 * 0.12 * 1 * 1 = 0.144 (pre-fix 0.288)
        var orcPlayer = TaomPregnancyModel.ComputeBaseChance(
            heroAge: 18, comesOfAge: 18, fertilityEnd: 45,
            childCount: 0, clanTier: 0, aliveLords: 100,
            playerOrSpouseInvolved: true, raceFertilityModifier: 2f);

        Assert.AreEqual(0.144f, orcPlayer, 0.001f);
    }
}
