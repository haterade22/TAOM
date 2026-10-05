using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// The tournament reward arithmetic (Mike, 2026-10-02): an MCM bet cap where 0 means unlimited, renown and
/// influence that grow with the heroes in the field and the winner's culture, and skill XP per round won.
/// </summary>
[TestClass]
public class TournamentRewardRulesTests
{
    // --- MaximumBet ---

    [TestMethod]
    public void MaximumBet_CapZero_IsUnlimitedUpToTheSafetyCeiling()
    {
        Assert.AreEqual(TournamentRewardRules.BetCeiling, TournamentRewardRules.MaximumBet(vanillaResult: 150, configuredCap: 0));
    }

    [TestMethod]
    public void MaximumBet_CapNegative_IsUnlimited()
    {
        Assert.AreEqual(TournamentRewardRules.BetCeiling, TournamentRewardRules.MaximumBet(150, -5));
    }

    [TestMethod]
    public void MaximumBet_FiniteCap_WithoutDeepPockets_IsTheCap()
    {
        Assert.AreEqual(1000, TournamentRewardRules.MaximumBet(vanillaResult: 150, configuredCap: 1000));
    }

    [TestMethod]
    public void MaximumBet_FiniteCap_WithDeepPockets_IsDoubled()
    {
        // Vanilla returns 300 with Deep Pockets: the perk's factor is read off vanilla's own answer.
        Assert.AreEqual(2000, TournamentRewardRules.MaximumBet(vanillaResult: 300, configuredCap: 1000));
    }

    [TestMethod]
    public void MaximumBet_FiniteCapAboveTheCeiling_IsClampedWithoutOverflow()
    {
        Assert.AreEqual(TournamentRewardRules.BetCeiling, TournamentRewardRules.MaximumBet(300, int.MaxValue));
    }

    [TestMethod]
    public void MaximumBet_VanillaResultBelowBase_NeverShrinksTheCap()
    {
        // Another mod lowering vanilla's answer must not divide the configured cap away.
        Assert.AreEqual(1000, TournamentRewardRules.MaximumBet(vanillaResult: 0, configuredCap: 1000));
    }

    // --- Renown ---

    [TestMethod]
    public void Renown_AddsOnePerHeroToVanilla()
    {
        Assert.AreEqual(9, TournamentRewardRules.Renown(vanillaRenown: 3, heroCount: 6, cultureFactor: 1f, multiplier: 1f));
    }

    [TestMethod]
    public void Renown_KeepsVanillaPerkBonuses()
    {
        // Duelist and Self Promoter live in vanilla's answer; the hero bonus is added to it.
        Assert.AreEqual(13, TournamentRewardRules.Renown(vanillaRenown: 9, heroCount: 4, cultureFactor: 1f, multiplier: 1f));
    }

    [TestMethod]
    public void Renown_CultureAndMultiplier_Scale_AndRound()
    {
        // (3 + 6) x 1.5 x 1.0 = 13.5, rounded half away from zero.
        Assert.AreEqual(14, TournamentRewardRules.Renown(3, 6, 1.5f, 1f));
    }

    [TestMethod]
    public void Renown_NegativeHeroCount_TreatedAsNone()
    {
        Assert.AreEqual(3, TournamentRewardRules.Renown(3, -2, 1f, 1f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(1f, float.NaN)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(-1f, 1f)]
    public void Renown_InvalidFactor_FallsBackToVanilla(float cultureFactor, float multiplier)
    {
        Assert.AreEqual(3, TournamentRewardRules.Renown(3, 6, cultureFactor, multiplier));
    }

    [DataTestMethod]
    [DataRow(TournamentRewardRules.MaxMultiplier + 0.01f)]
    [DataRow(1e9f)]
    public void Renown_MultiplierAboveTheSliderMaximum_FallsBackToVanilla(float multiplier)
    {
        // The slider stops at MaxMultiplier, and a hand-edited settings file can exceed it.
        Assert.AreEqual(3, TournamentRewardRules.Renown(3, 6, 1f, multiplier));
    }

    [TestMethod]
    public void Renown_MultiplierAtTheSliderMaximum_Applies()
    {
        // (3 + 6) x 1 x 5 = 45.
        Assert.AreEqual(45, TournamentRewardRules.Renown(3, 6, 1f, TournamentRewardRules.MaxMultiplier));
    }

    [TestMethod]
    public void Renown_ResultBeyondIntRange_FallsBackToVanilla()
    {
        // (3 + 6) x 1e9 x 5 is about 4.5e10: the unchecked cast would give int.MinValue (net472 x64), which
        // Clan.AddRenown ignores, so the award would silently vanish.
        Assert.AreEqual(3, TournamentRewardRules.Renown(3, 6, cultureFactor: 1e9f, multiplier: 5f));
    }

    // --- Influence (whole points: the engine's GetInfluenceReward is an int) ---

    [TestMethod]
    public void Influence_NotTheWinnersKingdomsTown_IsVanilla()
    {
        Assert.AreEqual(0, TournamentRewardRules.Influence(0, heroCount: 8, ownKingdomTown: false, 1.5f, 1f));
    }

    [TestMethod]
    public void Influence_OwnKingdomsTown_IsTwoPlusOnePerFourHeroes()
    {
        // 0 + 2 + 8 / 4 = 4.
        Assert.AreEqual(4, TournamentRewardRules.Influence(0, heroCount: 8, ownKingdomTown: true, 1f, 1f));
    }

    [TestMethod]
    public void Influence_PartialFourHeroes_CountsOnlyWholeFours()
    {
        Assert.AreEqual(3, TournamentRewardRules.Influence(0, heroCount: 7, ownKingdomTown: true, 1f, 1f));
    }

    [TestMethod]
    public void Influence_CultureAndMultiplier_Scale()
    {
        // (0 + 2 + 2) x 1.5 x 2 = 12.
        Assert.AreEqual(12, TournamentRewardRules.Influence(0, 8, true, 1.5f, 2f));
    }

    [TestMethod]
    public void Influence_KeepsVanillasAnswerInTheBase()
    {
        // (5 + 2 + 2) x 1.5 x 1 = 13.5, rounded half away from zero.
        Assert.AreEqual(14, TournamentRewardRules.Influence(5, 8, true, 1.5f, 1f));
    }

    [TestMethod]
    public void Influence_ScaledHalf_RoundsAwayFromZero()
    {
        // (0 + 2 + 1) x 1.5 x 1 = 4.5: the rule rounds, as Renown does.
        Assert.AreEqual(5, TournamentRewardRules.Influence(0, heroCount: 4, ownKingdomTown: true, 1.5f, 1f));
    }

    [TestMethod]
    public void Influence_NegativeHeroCount_TreatedAsNone()
    {
        // 0 + 2 + 0 = 2.
        Assert.AreEqual(2, TournamentRewardRules.Influence(0, heroCount: -8, ownKingdomTown: true, 1f, 1f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.NegativeInfinity)]
    public void Influence_InvalidFactor_FallsBackToVanilla(float factor)
    {
        Assert.AreEqual(0, TournamentRewardRules.Influence(0, 8, true, factor, 1f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-1f)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(TournamentRewardRules.MaxMultiplier + 0.01f)]
    public void Influence_InvalidMultiplier_FallsBackToVanilla(float multiplier)
    {
        // The multiplier is its own gate: the factor tests above pass the bad value as the culture factor.
        Assert.AreEqual(7, TournamentRewardRules.Influence(7, 8, true, 1f, multiplier));
    }

    [TestMethod]
    public void Influence_ResultBeyondIntRange_FallsBackToVanilla()
    {
        // (7 + 2 + 2) x 1e9 x 5 is about 5.5e10, past what an int holds: the unchecked cast gives
        // int.MinValue, which GainKingdomInfluenceAction would add to the clan's influence.
        Assert.AreEqual(7, TournamentRewardRules.Influence(7, 8, true, cultureFactor: 1e9f, multiplier: 5f));
    }

    // --- SkillXp ---

    [TestMethod]
    public void SkillXp_WinningTheTournament_IsFourRoundsPlusTheBonus()
    {
        Assert.AreEqual(750, TournamentRewardRules.SkillXp(roundsWon: 4, wonTournament: true, cultureFactor: 1f));
    }

    [TestMethod]
    public void SkillXp_EliminatedInTheThirdRound_IsTwoRounds()
    {
        Assert.AreEqual(250, TournamentRewardRules.SkillXp(roundsWon: 2, wonTournament: false, cultureFactor: 1f));
    }

    [TestMethod]
    public void SkillXp_EliminatedInTheFirstRound_IsNothing()
    {
        Assert.AreEqual(0, TournamentRewardRules.SkillXp(0, false, 1f));
    }

    [DataTestMethod]
    [DataRow(1.10f, 825)]
    [DataRow(1.40f, 1050)]
    public void SkillXp_CultureFactor_ScalesTheWholeAward(float factor, int expected)
    {
        Assert.AreEqual(expected, TournamentRewardRules.SkillXp(4, true, factor));
    }

    [TestMethod]
    public void SkillXp_RoundsWonOutOfRange_ClampedToTheFourRounds()
    {
        Assert.AreEqual(750, TournamentRewardRules.SkillXp(9, true, 1f));
        Assert.AreEqual(0, TournamentRewardRules.SkillXp(-1, false, 1f));
    }

    [TestMethod]
    public void SkillXp_InvalidFactor_UsesNoBonus()
    {
        Assert.AreEqual(750, TournamentRewardRules.SkillXp(4, true, float.NaN));
    }

    [TestMethod]
    public void SkillXp_FactorBeyondIntRange_UsesNoBonus()
    {
        // 750 x 1e9 is past what an int holds; the unchecked cast would give int.MinValue.
        Assert.AreEqual(750, TournamentRewardRules.SkillXp(4, true, 1e9f));
    }

    // --- CombatSkillIds ---

    [TestMethod]
    public void CombatSkillIds_AreTheEightEngineCombatSkills()
    {
        CollectionAssert.AreEquivalent(
            new[] { "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing", "Riding", "Athletics" },
            new System.Collections.Generic.List<string>(TournamentRewardRules.CombatSkillIds));
    }
}
