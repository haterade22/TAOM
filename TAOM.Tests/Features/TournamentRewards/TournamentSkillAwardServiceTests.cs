using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// When the player's tournament ends: a win trains the chosen skill for all four rounds plus the win bonus, an
/// elimination for the rounds won before it, and the player is told what was gained.
/// </summary>
[TestClass]
public class TournamentSkillAwardServiceTests
{
    private IHeroSkillXpAdapter _xp = null!;
    private ITournamentChoicePresenter _presenter = null!;
    private IDedicatedServerProvider _server = null!;
    private TournamentRewardsService _rewards = null!;
    private TournamentSkillAwardService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _xp = Substitute.For<IHeroSkillXpAdapter>();
        _xp.AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>()).Returns(true);
        var config = Substitute.For<ITournamentRewardsConfigProvider>();
        config.GetCatalog().Returns(TournamentRewardsCatalog.AllNeutral());
        _rewards = new TournamentRewardsService(Substitute.For<ITournamentRewardsSettingsProvider>(), config, _xp);
        _presenter = Substitute.For<ITournamentChoicePresenter>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _sut = new TournamentSkillAwardService(_rewards, _presenter, _server);
        _rewards.RememberSkillChoice("town_A", "Polearm");
    }

    [TestMethod]
    public void OnTournamentFinished_PlayerWon_AwardsTheFullTournamentAndSaysSo()
    {
        _sut.OnTournamentFinished(winnerIsPlayer: true, "town_A", "main_hero", "gondor");

        _xp.Received(1).AddSkillXp("main_hero", "Polearm", 750f);
        _presenter.Received(1).ShowSkillXpGained("Polearm", 750);
    }

    [TestMethod]
    public void OnTournamentFinished_SomeoneElseWon_AwardsNothingAndKeepsTheChoice()
    {
        // The player was eliminated earlier (that paid already) or only watched.
        _sut.OnTournamentFinished(winnerIsPlayer: false, "town_A", "main_hero", "gondor");

        _xp.DidNotReceive().AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        _presenter.DidNotReceive().ShowSkillXpGained(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void OnPlayerEliminated_PaysTheRoundsWonBeforeIt()
    {
        _sut.OnPlayerEliminated(roundIndex: 2, "town_A", "main_hero", "gondor");

        _xp.Received(1).AddSkillXp("main_hero", "Polearm", 250f);
        _presenter.Received(1).ShowSkillXpGained("Polearm", 250);
    }

    [TestMethod]
    public void OnPlayerEliminated_InTheFirstRound_SaysNothing()
    {
        _sut.OnPlayerEliminated(0, "town_A", "main_hero", "gondor");

        _presenter.DidNotReceive().ShowSkillXpGained(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void OnTournamentFinished_OnADedicatedServer_DoesNothing()
    {
        // A dedicated server's main hero is an idle world-gen hero, not a player.
        _server.IsDedicatedServer.Returns(true);

        _sut.OnTournamentFinished(true, "town_A", "main_hero", "gondor");
        _sut.OnPlayerEliminated(3, "town_A", "main_hero", "gondor");

        _xp.DidNotReceive().AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnTournamentFinished_NoChoiceRemembered_SaysNothing()
    {
        _sut.OnTournamentFinished(true, "town_B", "main_hero", "gondor");

        _presenter.DidNotReceive().ShowSkillXpGained(Arg.Any<string>(), Arg.Any<int>());
    }
}
