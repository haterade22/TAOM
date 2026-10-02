using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Arena;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// The Join flow (Mike, 2026-10-02): choose one of three prizes, then the skill to train, then the tournament
/// starts. Closing either dialog joins nothing and changes nothing.
/// </summary>
[TestClass]
public class TournamentJoinServiceTests
{
    private ITournamentJoinAdapter _join = null!;
    private ITournamentService _arena = null!;
    private ITournamentChoicePresenter _presenter = null!;
    private TournamentRewardsService _rewards = null!;
    private TournamentJoinService _sut = null!;
    private int _proceeded;

    private Action<string>? _onPrize;
    private Action? _onPrizeCancel;
    private Action<string>? _onSkill;
    private Action? _onSkillCancel;
    private IReadOnlyList<string>? _offeredPrizes;

    private static readonly TournamentJoinSnapshot Tournament = new("town_A", "gondor", "town_A:12", "prize_a");

    [TestInitialize]
    public void Setup()
    {
        _join = Substitute.For<ITournamentJoinAdapter>();
        _join.GetCurrentTournament().Returns(Tournament);
        _join.SetPrize(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _arena = Substitute.For<ITournamentService>();
        _arena.PrizeChoices("gondor", "prize_a", "town_A:12").Returns(new[] { "prize_a", "prize_b", "prize_c" });
        _presenter = Substitute.For<ITournamentChoicePresenter>();
        _presenter.When(p => p.ShowPrizeChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>()))
            .Do(c => { _offeredPrizes = c.ArgAt<IReadOnlyList<string>>(0); _onPrize = c.ArgAt<Action<string>>(1); _onPrizeCancel = c.ArgAt<Action>(2); });
        _presenter.When(p => p.ShowSkillChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>()))
            .Do(c => { _onSkill = c.ArgAt<Action<string>>(1); _onSkillCancel = c.ArgAt<Action>(2); });
        var settings = Substitute.For<ITournamentRewardsSettingsProvider>();
        var config = Substitute.For<ITournamentRewardsConfigProvider>();
        config.GetCatalog().Returns(TournamentRewardsCatalog.AllNeutral());
        _rewards = new TournamentRewardsService(settings, config, Substitute.For<IHeroSkillXpAdapter>());
        _sut = new TournamentJoinService(_join, _arena, _presenter, _rewards);
        _proceeded = 0;
    }

    private void Begin() => _sut.BeginJoin(() => _proceeded++);

    [TestMethod]
    public void BeginJoin_OffersTheThreePrizesFirst()
    {
        Begin();

        CollectionAssert.AreEqual(new[] { "prize_a", "prize_b", "prize_c" }, new List<string>(_offeredPrizes!));
        _presenter.DidNotReceive().ShowSkillChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>());
        Assert.AreEqual(0, _proceeded);
    }

    [TestMethod]
    public void BeginJoin_PrizeThenSkill_SetsBothAndJoins()
    {
        Begin();
        _onPrize!("prize_b");
        _onSkill!("Polearm");

        _join.Received(1).SetPrize("town_A", "prize_b");
        Assert.AreEqual("Polearm", _rewards.ChosenSkill("town_A"));
        Assert.AreEqual(1, _proceeded);
    }

    [TestMethod]
    public void BeginJoin_KeepingTheAdvertisedPrize_DoesNotRewriteIt()
    {
        Begin();
        _onPrize!("prize_a");
        _onSkill!("Bow");

        _join.DidNotReceive().SetPrize(Arg.Any<string>(), Arg.Any<string>());
        Assert.AreEqual(1, _proceeded);
    }

    [TestMethod]
    public void BeginJoin_SkillDialogOffersTheEightCombatSkills()
    {
        IReadOnlyList<string>? offered = null;
        _presenter.When(p => p.ShowSkillChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>()))
            .Do(c => offered = c.ArgAt<IReadOnlyList<string>>(0));

        Begin();
        _onPrize!("prize_a");

        CollectionAssert.AreEqual(new List<string>(TournamentRewardRules.CombatSkillIds), new List<string>(offered!));
    }

    [TestMethod]
    public void BeginJoin_PrizeDialogClosed_JoinsNothingAndChangesNothing()
    {
        Begin();
        _onPrizeCancel!();

        Assert.AreEqual(0, _proceeded);
        _join.DidNotReceive().SetPrize(Arg.Any<string>(), Arg.Any<string>());
        _presenter.DidNotReceive().ShowSkillChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>());
    }

    [TestMethod]
    public void BeginJoin_SkillDialogClosed_JoinsNothing_AndThePrizeIsNotChanged()
    {
        Begin();
        _onPrize!("prize_c");
        _onSkillCancel!();

        Assert.AreEqual(0, _proceeded);
        _join.DidNotReceive().SetPrize(Arg.Any<string>(), Arg.Any<string>());
        Assert.IsNull(_rewards.ChosenSkill("town_A"));
    }

    [TestMethod]
    public void BeginJoin_PickedPrizeNotOffered_Ignored()
    {
        Begin();
        _onPrize!("forged_item");
        _onSkill!("Bow");

        _join.DidNotReceive().SetPrize(Arg.Any<string>(), Arg.Any<string>());
        Assert.AreEqual(1, _proceeded);
    }

    [TestMethod]
    public void BeginJoin_PickedSkillNotOffered_JoinsNothing()
    {
        Begin();
        _onPrize!("prize_b");
        _onSkill!("Smithing");

        Assert.AreEqual(0, _proceeded);
        _join.DidNotReceive().SetPrize(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void BeginJoin_OnlyTheAdvertisedPrize_SkipsThePrizeDialog()
    {
        _arena.PrizeChoices("gondor", "prize_a", "town_A:12").Returns(new[] { "prize_a" });

        Begin();
        _presenter.DidNotReceive().ShowPrizeChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>());
        _onSkill!("Riding");

        Assert.AreEqual(1, _proceeded);
    }

    [TestMethod]
    public void BeginJoin_NoTournamentReadable_JoinsAsVanillaWithoutDialogs()
    {
        _join.GetCurrentTournament().Returns((TournamentJoinSnapshot?)null);

        Begin();

        Assert.AreEqual(1, _proceeded);
        _presenter.DidNotReceive().ShowPrizeChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>());
        _presenter.DidNotReceive().ShowSkillChoice(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>(), Arg.Any<Action>());
    }

    [TestMethod]
    public void BeginJoin_PrizeWriteFails_StillJoinsWithTheAdvertisedPrize()
    {
        _join.SetPrize(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        Begin();
        _onPrize!("prize_b");
        _onSkill!("Bow");

        Assert.AreEqual(1, _proceeded);
    }

    [TestMethod]
    public void BeginJoin_NoPrize_SkipsThePrizeDialog()
    {
        _join.GetCurrentTournament().Returns(new TournamentJoinSnapshot("town_A", "gondor", "town_A:12", null));

        Begin();
        _onSkill!("Bow");

        _arena.DidNotReceive().PrizeChoices(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.AreEqual(1, _proceeded);
    }
}
