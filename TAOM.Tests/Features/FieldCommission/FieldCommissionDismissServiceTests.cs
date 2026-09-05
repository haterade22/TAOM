using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCommission;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class FieldCommissionDismissServiceTests
{
    private const string HeroId = "hero_promoted_1";
    private const string HeroName = "Beregond";
    private const string TroopId = "taom_gondor_soldier";
    private const string TroopName = "Gondor Soldier";

    private IFieldCommissionMeritService _merit = null!;
    private IHeroCommissionAdapter _heroCommission = null!;
    private ITroopRosterQueryAdapter _roster = null!;
    private IInquiryPresenterAdapter _presenter = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private IModLogger _logger = null!;
    private FieldCommissionDismissService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _merit = Substitute.For<IFieldCommissionMeritService>();
        _heroCommission = Substitute.For<IHeroCommissionAdapter>();
        _roster = Substitute.For<ITroopRosterQueryAdapter>();
        _presenter = Substitute.For<IInquiryPresenterAdapter>();
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _logger = Substitute.For<IModLogger>();

        // The happy path: a healthy promoted companion riding in the main party, whose origin
        // troop still resolves. Every refusal test moves exactly one of these.
        _merit.GetPromotedHeroIds().Returns(new List<string> { HeroId });
        _enlistment.IsEnlisted.Returns(false);
        _roster.HasMainParty.Returns(true);
        _roster.GetTroopInfo(TroopId).Returns(new TroopInfo(TroopId, TroopName, false, false, 0, 10));
        _roster.AddOneToRoster(Arg.Any<string>(), Arg.Any<bool>()).Returns(true);
        _heroCommission.RemoveCompanionFromGame(HeroId).Returns(true);
        SetupHero();

        _sut = new FieldCommissionDismissService(_merit, _heroCommission, _roster, _presenter, _enlistment, _logger);
    }

    private void SetupHero(
        string heroId = HeroId,
        bool isPlayerCompanion = true,
        bool isInMainParty = true,
        bool isPartyInBattle = false,
        bool isWounded = false,
        string originTroopId = TroopId,
        string name = HeroName)
    {
        _heroCommission.GetPromotedHeroSnapshot(heroId).Returns(
            new PromotedHeroSnapshot(name, originTroopId, isPlayerCompanion, isInMainParty, isPartyInBattle, isWounded));
    }

    /// <summary>Puts the fixture into the one state that yields <paramref name="outcome"/>.</summary>
    private void ArrangeRefusal(DismissOutcome outcome)
    {
        switch (outcome)
        {
            case DismissOutcome.NotPromoted:
                _merit.GetPromotedHeroIds().Returns(new List<string>());
                break;
            case DismissOutcome.PlayerEnlisted:
                _enlistment.IsEnlisted.Returns(true);
                break;
            case DismissOutcome.HeroGone:
                _heroCommission.GetPromotedHeroSnapshot(HeroId).Returns(PromotedHeroSnapshot.Missing);
                break;
            case DismissOutcome.NotACompanion:
                SetupHero(isPlayerCompanion: false);
                break;
            case DismissOutcome.NotInMainParty:
                SetupHero(isInMainParty: false);
                break;
            case DismissOutcome.PartyInBattle:
                SetupHero(isPartyInBattle: true);
                break;
            case DismissOutcome.TroopUnresolved:
                SetupHero(originTroopId: "troop_removed_from_xml");
                _roster.GetTroopInfo("troop_removed_from_xml").Returns(TroopInfo.Missing);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "not an Evaluate verdict");
        }
    }

    // Every mutator on every injected mock, not only the ones a dismissal is meant to call: the
    // promotion-side calls (a roster DEcrement, a hero creation) share these adapters, and a
    // refusal path that reached one of them would otherwise pass every test in this file.
    private void AssertNothingMutated()
    {
        _heroCommission.DidNotReceiveWithAnyArgs().RemoveCompanionFromGame(null);
        _heroCommission.DidNotReceiveWithAnyArgs().CreateCompanionFromTroop(null, null, null);
        _roster.DidNotReceiveWithAnyArgs().AddOneToRoster(null, false);
        _roster.DidNotReceiveWithAnyArgs().RemoveOneFromRoster(null);
        _merit.DidNotReceiveWithAnyArgs().ForgetPromotedHero(null);
        _merit.DidNotReceiveWithAnyArgs().RecordPromotedHero(null);
        _merit.DidNotReceiveWithAnyArgs().CompleteOffer(null);
        _merit.DidNotReceiveWithAnyArgs().GrantMerit(null, 0);
        _merit.DidNotReceiveWithAnyArgs().RecordDeclinedOffer(null);
        _merit.DidNotReceiveWithAnyArgs().BeginBattle(false);
        _merit.DidNotReceiveWithAnyArgs().RegisterKill(null);
        _merit.DidNotReceiveWithAnyArgs().EndBattle(false);
        _merit.DidNotReceiveWithAnyArgs().ClearPendingOffers();
        _merit.DidNotReceiveWithAnyArgs().PruneDeadPromotedHeroes(null);
        _merit.DidNotReceiveWithAnyArgs().ImportMerits(null);
        _merit.DidNotReceiveWithAnyArgs().ImportPromotedHeroIds(null);
        _merit.DidNotReceiveWithAnyArgs().ImportDeclinedMarks(null);
    }

    // --- Evaluate: one test per guard, in guard order ---

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Evaluate_NullOrEmptyId_ReturnsNotPromoted(string heroId)
    {
        var result = _sut.Evaluate(heroId);

        Assert.AreEqual(DismissOutcome.NotPromoted, result.Outcome);
        Assert.IsFalse(result.IsDismissable);
    }

    [TestMethod]
    public void Evaluate_IdNotInPromotedList_ReturnsNotPromoted()
    {
        // An ordinary tavern companion: not ours to dismiss, the vanilla fire line is their path.
        var result = _sut.Evaluate("hero_tavern_wanderer");

        Assert.AreEqual(DismissOutcome.NotPromoted, result.Outcome);
        _heroCommission.DidNotReceiveWithAnyArgs().GetPromotedHeroSnapshot(null);
    }

    [TestMethod]
    public void Evaluate_PlayerEnlisted_ReturnsPlayerEnlisted()
    {
        _enlistment.IsEnlisted.Returns(true);

        Assert.AreEqual(DismissOutcome.PlayerEnlisted, _sut.Evaluate(HeroId).Outcome);
    }

    [DataTestMethod]
    [DataRow("dead")]
    [DataRow("disabled")]
    public void Evaluate_PromotedHeroMissingFromAliveList_ReturnsHeroGone(string state)
    {
        // Dead and disabled heroes both leave Hero.AllAliveHeroes, which is the only registry a
        // runtime-created hero is in, so the adapter answers Missing for either.
        _heroCommission.GetPromotedHeroSnapshot(HeroId).Returns(PromotedHeroSnapshot.Missing);

        var result = _sut.Evaluate(HeroId);

        Assert.AreEqual(DismissOutcome.HeroGone, result.Outcome, state);
        Assert.AreEqual(HeroId, result.HeroId);
    }

    [TestMethod]
    public void Evaluate_PromotedHeroNoLongerCompanion_ReturnsNotACompanion()
    {
        SetupHero(isPlayerCompanion: false);

        var result = _sut.Evaluate(HeroId);

        Assert.AreEqual(DismissOutcome.NotACompanion, result.Outcome);
        Assert.AreEqual(HeroName, result.HeroName, "the name is still known and the prompt should use it");
    }

    [DataTestMethod]
    [DataRow("governor of a fief")]
    [DataRow("leading their own party")]
    [DataRow("leading a caravan")]
    [DataRow("refuge warden")]
    [DataRow("prisoner of an enemy")]
    [DataRow("prisoner in the main party")]
    [DataRow("fugitive")]
    public void Evaluate_HeroOutsideMainParty_ReturnsNotInMainParty(string state)
    {
        // Every one of these states has PartyBelongedTo either null or some other party, and
        // that is the whole test: the adapter folds them into one flag.
        SetupHero(isInMainParty: false);

        Assert.AreEqual(DismissOutcome.NotInMainParty, _sut.Evaluate(HeroId).Outcome, state);
    }

    [TestMethod]
    public void Evaluate_NoMainPartyRoster_ReturnsNotInMainParty()
    {
        _roster.HasMainParty.Returns(false);

        Assert.AreEqual(DismissOutcome.NotInMainParty, _sut.Evaluate(HeroId).Outcome);
    }

    [TestMethod]
    public void Evaluate_MainPartyInBattle_ReturnsPartyInBattle()
    {
        // KillCharacterAction only marks the hero and defers while the party has a MapEvent or a
        // SiegeEvent; a refund against a deferred removal would be a soldier from nowhere.
        SetupHero(isPartyInBattle: true);

        Assert.AreEqual(DismissOutcome.PartyInBattle, _sut.Evaluate(HeroId).Outcome);
    }

    [TestMethod]
    public void Evaluate_OriginTroopIdNull_ReturnsTroopUnresolved()
    {
        SetupHero(originTroopId: null);

        Assert.AreEqual(DismissOutcome.TroopUnresolved, _sut.Evaluate(HeroId).Outcome);
        _roster.DidNotReceiveWithAnyArgs().GetTroopInfo(null);
    }

    [TestMethod]
    public void Evaluate_OriginTroopMissing_ReturnsTroopUnresolved()
    {
        SetupHero(originTroopId: "troop_removed_from_xml");
        _roster.GetTroopInfo("troop_removed_from_xml").Returns(TroopInfo.Missing);

        Assert.AreEqual(DismissOutcome.TroopUnresolved, _sut.Evaluate(HeroId).Outcome);
    }

    [TestMethod]
    public void Evaluate_OriginTroopIsHero_ReturnsTroopUnresolved()
    {
        // A hero template must never be added to a roster by count; refuse rather than refund.
        _roster.GetTroopInfo(TroopId).Returns(new TroopInfo(TroopId, TroopName, true, false, 0, 10));

        Assert.AreEqual(DismissOutcome.TroopUnresolved, _sut.Evaluate(HeroId).Outcome);
    }

    [TestMethod]
    public void Evaluate_HealthyCompanionInMainParty_ReturnsOkWithNames()
    {
        var result = _sut.Evaluate(HeroId);

        Assert.IsTrue(result.IsDismissable);
        Assert.AreEqual(HeroId, result.HeroId);
        Assert.AreEqual(HeroName, result.HeroName);
        Assert.AreEqual(TroopId, result.TroopId);
        Assert.AreEqual(TroopName, result.TroopName);
    }

    [TestMethod]
    public void Evaluate_WoundedCompanionInMainParty_ReturnsOk()
    {
        SetupHero(isWounded: true);

        Assert.IsTrue(_sut.Evaluate(HeroId).IsDismissable);
    }

    [TestMethod]
    public void Evaluate_EnlistedAndHeroGoneAtOnce_ReportsPlayerEnlisted()
    {
        // The one guard order a player can trigger simultaneously. The enlistment gate runs before
        // the hero lookup, so the log names the temporary blocker and no hero scan is spent.
        _enlistment.IsEnlisted.Returns(true);
        _heroCommission.GetPromotedHeroSnapshot(HeroId).Returns(PromotedHeroSnapshot.Missing);

        Assert.AreEqual(DismissOutcome.PlayerEnlisted, _sut.Evaluate(HeroId).Outcome);
        _heroCommission.DidNotReceiveWithAnyArgs().GetPromotedHeroSnapshot(null);
    }

    [TestMethod]
    public void Evaluate_AnyVerdict_NeverMutates()
    {
        _sut.Evaluate(HeroId);
        AssertNothingMutated();

        foreach (var outcome in new[]
                 {
                     DismissOutcome.NotPromoted, DismissOutcome.PlayerEnlisted, DismissOutcome.HeroGone,
                     DismissOutcome.NotACompanion, DismissOutcome.NotInMainParty, DismissOutcome.PartyInBattle,
                     DismissOutcome.TroopUnresolved,
                 })
        {
            // A fresh fixture per verdict, and the verdict asserted: the first version of this loop
            // let each arrangement leak into the next, so after the first iteration every call
            // answered NotPromoted and the test was exercising one verdict while naming seven.
            Setup();
            ArrangeRefusal(outcome);
            Assert.AreEqual(outcome, _sut.Evaluate(HeroId).Outcome, outcome.ToString());
            AssertNothingMutated();
        }
    }

    // --- GetDismissableCompanions ---

    [TestMethod]
    public void GetDismissableCompanions_NoPromotedIds_ReturnsEmpty()
    {
        _merit.GetPromotedHeroIds().Returns(new List<string>());

        Assert.AreEqual(0, _sut.GetDismissableCompanions().Count);
    }

    [TestMethod]
    public void GetDismissableCompanions_PromotedIdsNull_ReturnsEmpty()
    {
        _merit.GetPromotedHeroIds().Returns((IReadOnlyList<string>)null);

        Assert.AreEqual(0, _sut.GetDismissableCompanions().Count);
    }

    [TestMethod]
    public void GetDismissableCompanions_MixedStates_ReturnsOnlyOkInPromotionOrder()
    {
        _merit.GetPromotedHeroIds().Returns(new List<string> { "hero_a", "hero_b", "hero_c" });
        SetupHero(heroId: "hero_a", name: "A");
        SetupHero(heroId: "hero_b", name: "B", isInMainParty: false);
        SetupHero(heroId: "hero_c", name: "C");

        var result = _sut.GetDismissableCompanions();

        CollectionAssert.AreEqual(new[] { "hero_a", "hero_c" }, result.Select(c => c.HeroId).ToList());
        Assert.IsTrue(result.All(c => c.IsDismissable));
    }

    [TestMethod]
    public void GetDismissableCompanions_Called_DoesNotMutate()
    {
        _sut.GetDismissableCompanions();

        AssertNothingMutated();
    }

    // --- Dismiss: nothing is ever partially applied ---

    [DataTestMethod]
    [DataRow(DismissOutcome.NotPromoted)]
    [DataRow(DismissOutcome.PlayerEnlisted)]
    [DataRow(DismissOutcome.HeroGone)]
    [DataRow(DismissOutcome.NotACompanion)]
    [DataRow(DismissOutcome.NotInMainParty)]
    [DataRow(DismissOutcome.PartyInBattle)]
    [DataRow(DismissOutcome.TroopUnresolved)]
    public void Dismiss_VerdictNotOk_ReturnsVerdictAndTouchesNothing(DismissOutcome outcome)
    {
        ArrangeRefusal(outcome);

        var result = _sut.Dismiss(HeroId);

        Assert.AreEqual(outcome, result);
        AssertNothingMutated();
    }

    [TestMethod]
    public void Dismiss_RemovalReturnsFalse_ReturnsRemovalFailedWithoutRefundOrForget()
    {
        // The engine declined (deferred behind a DeathMark, or threw inside the adapter). The hero
        // is still there, so no soldier may appear and the id must stay on the list.
        _heroCommission.RemoveCompanionFromGame(HeroId).Returns(false);

        var result = _sut.Dismiss(HeroId);

        Assert.AreEqual(DismissOutcome.RemovalFailed, result);
        _roster.DidNotReceiveWithAnyArgs().AddOneToRoster(null, false);
        _merit.DidNotReceiveWithAnyArgs().ForgetPromotedHero(null);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(HeroId)));
    }

    [TestMethod]
    public void Dismiss_Ok_RemovesHeroBeforeRefundingTroop()
    {
        _sut.Dismiss(HeroId);

        Received.InOrder(() =>
        {
            _heroCommission.RemoveCompanionFromGame(HeroId);
            _roster.AddOneToRoster(TroopId, false);
            _merit.ForgetPromotedHero(HeroId);
        });
    }

    [TestMethod]
    public void Dismiss_HealthyCompanion_RefundsExactlyOneHealthyOriginSoldier()
    {
        var result = _sut.Dismiss(HeroId);

        Assert.AreEqual(DismissOutcome.Ok, result);
        _roster.Received(1).AddOneToRoster(TroopId, false);
        _roster.DidNotReceive().AddOneToRoster(Arg.Is<string>(id => id != TroopId), Arg.Any<bool>());
    }

    [TestMethod]
    public void Dismiss_WoundedCompanion_RefundsAWoundedSoldier()
    {
        // A dismissal is not a free heal: the soldier comes back in the state the officer was in.
        SetupHero(isWounded: true);

        _sut.Dismiss(HeroId);

        _roster.Received(1).AddOneToRoster(TroopId, true);
    }

    [TestMethod]
    public void Dismiss_Ok_ForgetsPromotedIdAndLeavesMeritUntouched()
    {
        _sut.Dismiss(HeroId);

        _merit.Received(1).ForgetPromotedHero(HeroId);
        _merit.DidNotReceiveWithAnyArgs().CompleteOffer(null);
        _merit.DidNotReceiveWithAnyArgs().GrantMerit(null, 0);
        _merit.DidNotReceiveWithAnyArgs().RecordDeclinedOffer(null);
        _merit.DidNotReceiveWithAnyArgs().ImportMerits(null);
        _merit.DidNotReceiveWithAnyArgs().ImportPromotedHeroIds(null);
    }

    [TestMethod]
    public void Dismiss_RefundFails_LogsWarningStillForgetsIdAndReturnsOk()
    {
        // The hero is already gone by the time the refund can fail, so there is nothing to roll
        // back; the honest outcome is Ok with a loud warning, never a silent short party.
        _roster.AddOneToRoster(TroopId, false).Returns(false);

        var result = _sut.Dismiss(HeroId);

        Assert.AreEqual(DismissOutcome.Ok, result);
        _merit.Received(1).ForgetPromotedHero(HeroId);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(TroopId)));
    }

    // --- DismissAndReport ---

    [TestMethod]
    public void DismissAndReport_Ok_ShowsDismissedWithHeroAndTroopNames()
    {
        var result = _sut.DismissAndReport(HeroId);

        Assert.AreEqual(DismissOutcome.Ok, result);
        _presenter.Received(1).ShowDismissed(HeroName, TroopName);
        _presenter.DidNotReceiveWithAnyArgs().ShowDismissFailed(null);
    }

    [TestMethod]
    public void DismissAndReport_Refused_ShowsFailedWithHeroNameAndLogs()
    {
        SetupHero(isPartyInBattle: true);

        var result = _sut.DismissAndReport(HeroId);

        Assert.AreEqual(DismissOutcome.PartyInBattle, result);
        _presenter.Received(1).ShowDismissFailed(HeroName);
        _presenter.DidNotReceiveWithAnyArgs().ShowDismissed(null, null);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("PartyInBattle")));
        AssertNothingMutated();
    }

    [TestMethod]
    public void DismissAndReport_EngineDeclined_ShowsFailed()
    {
        _heroCommission.RemoveCompanionFromGame(HeroId).Returns(false);

        Assert.AreEqual(DismissOutcome.RemovalFailed, _sut.DismissAndReport(HeroId));
        _presenter.Received(1).ShowDismissFailed(HeroName);
    }

    // --- OpenDismissPicker: the settlement-menu chain ---

    private Action<string> CapturePickerChoice()
    {
        Action<string> onChosen = null;
        _presenter.ShowDismissPicker(Arg.Any<IReadOnlyList<DismissCandidate>>(), Arg.Do<Action<string>>(a => onChosen = a));
        _sut.OpenDismissPicker();
        Assert.IsNotNull(onChosen, "the picker was not shown");
        return onChosen;
    }

    [TestMethod]
    public void OpenDismissPicker_NoCandidates_ShowsNothing()
    {
        SetupHero(isInMainParty: false);

        _sut.OpenDismissPicker();

        _presenter.DidNotReceiveWithAnyArgs().ShowDismissPicker(null, null);
    }

    [TestMethod]
    public void OpenDismissPicker_Candidates_ShowsOnlyTheDismissableOnes()
    {
        _merit.GetPromotedHeroIds().Returns(new List<string> { "hero_a", "hero_b" });
        SetupHero(heroId: "hero_a", name: "A");
        SetupHero(heroId: "hero_b", name: "B", isPlayerCompanion: false);

        _sut.OpenDismissPicker();

        _presenter.Received(1).ShowDismissPicker(
            Arg.Is<IReadOnlyList<DismissCandidate>>(l => l.Count == 1 && l[0].HeroId == "hero_a"),
            Arg.Any<Action<string>>());
    }

    [TestMethod]
    public void OpenDismissPicker_PickedThenConfirmed_DismissesAndReports()
    {
        var onChosen = CapturePickerChoice();
        Action onConfirm = null;
        _presenter.ShowDismissConfirm(HeroName, TroopName, Arg.Do<Action>(a => onConfirm = a), Arg.Any<Action>());

        onChosen(HeroId);
        Assert.IsNotNull(onConfirm, "the confirm inquiry was not shown");
        onConfirm();

        _heroCommission.Received(1).RemoveCompanionFromGame(HeroId);
        _roster.Received(1).AddOneToRoster(TroopId, false);
        _presenter.Received(1).ShowDismissed(HeroName, TroopName);
    }

    [TestMethod]
    public void OpenDismissPicker_PickedThenCancelled_TouchesNothing()
    {
        var onChosen = CapturePickerChoice();
        Action onCancel = null;
        _presenter.ShowDismissConfirm(HeroName, TroopName, Arg.Any<Action>(), Arg.Do<Action>(a => onCancel = a));

        onChosen(HeroId);
        Assert.IsNotNull(onCancel);
        onCancel();

        AssertNothingMutated();
        _presenter.DidNotReceiveWithAnyArgs().ShowDismissed(null, null);
    }

    [TestMethod]
    public void OpenDismissPicker_StateChangedWhilePickerOpen_ShowsFailedWithoutConfirm()
    {
        // The list is built when the picker opens; the verdict is re-read when a name is picked.
        var onChosen = CapturePickerChoice();
        SetupHero(isPartyInBattle: true);

        onChosen(HeroId);

        _presenter.DidNotReceiveWithAnyArgs().ShowDismissConfirm(null, null, null, null);
        _presenter.Received(1).ShowDismissFailed(HeroName);
        AssertNothingMutated();
    }
}
