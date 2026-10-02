using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// The reward service between the engine's hooks and the rules: which hero count a reward is scaled by, which
/// culture's factor applies, and the chosen skill that a round won or a tournament won trains.
/// </summary>
[TestClass]
public class TournamentRewardsServiceTests
{
    private ITournamentRewardsSettingsProvider _settings = null!;
    private ITournamentRewardsConfigProvider _config = null!;
    private IHeroSkillXpAdapter _xp = null!;
    private TournamentRewardsService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ITournamentRewardsSettingsProvider>();
        _settings.MaxBetPerRound.Returns(0);
        _settings.RenownMultiplier.Returns(1f);
        _settings.InfluenceMultiplier.Returns(1f);
        _config = Substitute.For<ITournamentRewardsConfigProvider>();
        _config.GetCatalog().Returns(new TournamentRewardsCatalog(
            new System.Collections.Generic.Dictionary<string, TournamentCultureFactors>
            {
                ["vlandia"] = new(1.5f, 1.25f, 1f),
                ["mordor"] = new(1.5f, 1f, 1.4f),
            },
            TournamentCultureFactors.Neutral));
        _xp = Substitute.For<IHeroSkillXpAdapter>();
        _xp.AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>()).Returns(true);
        _sut = new TournamentRewardsService(_settings, _config, _xp);
    }

    // --- MaximumBet ---

    [TestMethod]
    public void MaximumBet_SettingZero_IsUnlimited()
    {
        Assert.AreEqual(TournamentRewardRules.BetCeiling, _sut.MaximumBet(150));
    }

    [TestMethod]
    public void MaximumBet_SettingFinite_IsTheCapWithThePerk()
    {
        _settings.MaxBetPerRound.Returns(500);

        Assert.AreEqual(1000, _sut.MaximumBet(300));
    }

    // --- RenownReward ---

    [TestMethod]
    public void RenownReward_AfterTheTournamentInThatTown_CountsItsHeroes()
    {
        _sut.NoteTournamentFinished("town_A", heroCount: 6);

        Assert.AreEqual(9, _sut.RenownReward(3, "town_A", "rivendell"));
    }

    [TestMethod]
    public void RenownReward_ForAnotherTown_CountsNoHeroes()
    {
        _sut.NoteTournamentFinished("town_A", 6);

        Assert.AreEqual(3, _sut.RenownReward(3, "town_B", "rivendell"));
    }

    [TestMethod]
    public void RenownReward_NoTournamentNoted_CountsNoHeroes()
    {
        Assert.AreEqual(3, _sut.RenownReward(3, "town_A", "rivendell"));
    }

    [TestMethod]
    public void RenownReward_UsesTheWinnersCultureAndTheMultiplier()
    {
        _settings.RenownMultiplier.Returns(2f);
        _sut.NoteTournamentFinished("town_A", 7);

        // (3 + 7) x 1.5 x 2 = 30.
        Assert.AreEqual(30, _sut.RenownReward(3, "town_A", "vlandia"));
    }

    [TestMethod]
    public void RenownReward_SameValueForTheWinnerPanelAsForTheAward()
    {
        // The engine awards, then the winner panel asks again for the same town: both must agree.
        _sut.NoteTournamentFinished("town_A", 5);

        Assert.AreEqual(_sut.RenownReward(3, "town_A", "vlandia"), _sut.RenownReward(3, "town_A", "vlandia"));
    }

    // --- InfluenceReward ---

    [TestMethod]
    public void InfluenceReward_WinnersKingdomOwnsTheTown_PaysTheBonus()
    {
        _sut.NoteTournamentFinished("town_A", 8);

        Assert.AreEqual(5, _sut.InfluenceReward(0, "town_A", "vlandia", winnerKingdomId: "rohan_k", townKingdomId: "rohan_k"));
    }

    [TestMethod]
    public void InfluenceReward_AnotherKingdomsTown_IsVanilla()
    {
        _sut.NoteTournamentFinished("town_A", 8);

        Assert.AreEqual(0, _sut.InfluenceReward(0, "town_A", "vlandia", "rohan_k", "gondor_k"));
    }

    [DataTestMethod]
    [DataRow(null, "rohan_k")]
    [DataRow("rohan_k", null)]
    [DataRow(null, null)]
    [DataRow("", "")]
    public void InfluenceReward_AKingdomUnknown_IsVanilla(string? winnerKingdom, string? townKingdom)
    {
        _sut.NoteTournamentFinished("town_A", 8);

        Assert.AreEqual(0, _sut.InfluenceReward(0, "town_A", "vlandia", winnerKingdom, townKingdom));
    }

    [TestMethod]
    public void InfluenceReward_UsesTheInfluenceMultiplier()
    {
        _settings.InfluenceMultiplier.Returns(0.5f);
        _sut.NoteTournamentFinished("town_A", 8);

        // (0 + 2 + 2) x 1.0 x 0.5 = 2.
        Assert.AreEqual(2, _sut.InfluenceReward(0, "town_A", "rivendell", "k", "k"));
    }

    // --- Skill XP ---

    [TestMethod]
    public void AwardPlayerSkillXp_WithAChoiceForThatTown_TrainsTheChosenSkill()
    {
        _sut.RememberSkillChoice("town_A", "Polearm");

        var awarded = _sut.AwardPlayerSkillXp("town_A", roundsWon: 4, wonTournament: true, "main_hero", "gondor");

        Assert.AreEqual(750, awarded);
        _xp.Received(1).AddSkillXp("main_hero", "Polearm", 750f);
    }

    [TestMethod]
    public void AwardPlayerSkillXp_UsesThePlayersCultureFactor()
    {
        _sut.RememberSkillChoice("town_A", "OneHanded");

        _sut.AwardPlayerSkillXp("town_A", 4, true, "main_hero", "mordor");

        _xp.Received(1).AddSkillXp("main_hero", "OneHanded", 1050f);
    }

    [TestMethod]
    public void AwardPlayerSkillXp_ConsumesTheChoice()
    {
        _sut.RememberSkillChoice("town_A", "Bow");
        _sut.AwardPlayerSkillXp("town_A", 2, false, "main_hero", null);

        Assert.AreEqual(0, _sut.AwardPlayerSkillXp("town_A", 4, true, "main_hero", null));
        _xp.Received(1).AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void AwardPlayerSkillXp_NoChoiceForThatTown_AwardsNothing()
    {
        _sut.RememberSkillChoice("town_A", "Bow");

        Assert.AreEqual(0, _sut.AwardPlayerSkillXp("town_B", 4, true, "main_hero", null));
        _xp.DidNotReceive().AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void AwardPlayerSkillXp_EliminatedInTheFirstRound_ConsumesTheChoiceWithoutXp()
    {
        _sut.RememberSkillChoice("town_A", "Bow");

        Assert.AreEqual(0, _sut.AwardPlayerSkillXp("town_A", 0, false, "main_hero", null));
        _xp.DidNotReceive().AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        Assert.AreEqual(0, _sut.AwardPlayerSkillXp("town_A", 4, true, "main_hero", null));
    }

    [TestMethod]
    public void AwardPlayerSkillXp_NoPlayerHero_AwardsNothing()
    {
        _sut.RememberSkillChoice("town_A", "Bow");

        Assert.AreEqual(0, _sut.AwardPlayerSkillXp("town_A", 4, true, null, null));
    }

    [TestMethod]
    public void AwardPlayerSkillXp_AdapterRefuses_ReportsNothingAwarded()
    {
        _xp.AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>()).Returns(false);
        _sut.RememberSkillChoice("town_A", "Bow");

        Assert.AreEqual(0, _sut.AwardPlayerSkillXp("town_A", 4, true, "main_hero", null));
    }

    [TestMethod]
    public void RememberSkillChoice_UnknownSkill_Ignored()
    {
        _sut.RememberSkillChoice("town_A", "Smithing");

        Assert.AreEqual(0, _sut.AwardPlayerSkillXp("town_A", 4, true, "main_hero", null));
    }

    [TestMethod]
    public void ChosenSkill_ReportsTheRememberedSkill()
    {
        _sut.RememberSkillChoice("town_A", "Riding");

        Assert.AreEqual("Riding", _sut.ChosenSkill("town_A"));
        Assert.IsNull(_sut.ChosenSkill("town_B"));
    }

    // --- ResetForNewSession ---

    [TestMethod]
    public void ResetForNewSession_ForgetsTheChoiceAndTheHeroCount()
    {
        _sut.RememberSkillChoice("town_A", "Bow");
        _sut.NoteTournamentFinished("town_A", 8);

        _sut.ResetForNewSession();

        Assert.IsNull(_sut.ChosenSkill("town_A"));
        Assert.AreEqual(3, _sut.RenownReward(3, "town_A", "rivendell"));
    }
}
